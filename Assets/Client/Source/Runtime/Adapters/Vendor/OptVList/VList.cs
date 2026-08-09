using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapters.Bindings
{
    /// <summary>
    /// VList uses Unity's version of the ScrollRect.
    /// </summary>
    [RequireComponent(typeof(ScrollRect), typeof(RectMask2D))]
    public class VList : MonoBehaviour
    {
        protected const int CapacityByDefault = 10;
        private static readonly Vector3 DockPoint = new Vector3(-10000, 0); 

        // Prefab from which visual items will be created.
        [SerializeField]
        private RectTransform prototypeItem;
        [SerializeField]
        private Vector2 prototypeSize;
        [SerializeField]
        private bool isAutoGap;
        [SerializeField]
        private Vector2 gap;
        [SerializeField, Tooltip("Viewport size can be changed at runtime. Set 'false' to optimize performance.")]
        private bool isViewportDynamic;
        [SerializeField, Tooltip("Item width follows the viewport instead of the prototype, so the list is always one column wide. For single-column lists (chat logs, feeds) rather than grids.")]
        private bool isItemFullWidth;
        [SerializeField, Tooltip("Minimum vertical scrollbar handle size (if it exists). Range values [0-1]")]
        [Range(0.0f, 1.0f)]
        private float minScrollHandleSize;
        
        private ScrollRect _scrollRect;
        private Rect _prevViewportRect;
        private Viewport _viewport;
        private List<int> _itemIds;

        // Mapping between item index and visual representation
        private Dictionary<int, VisualData> _mapIndexVisualItem;
        // Mapping between itemId and item visual
        private Binder<int, VisualData> _mapItemIdVisualItem;
        // Mapping between itemId and item data
        private Dictionary<int, IItemData> _mapItemIdItemData;

        private Stack<VisualData> _visualsPool;
        private int _itemIdCounter;
        private List<int> _itemsToAdd;
        private List<int> _itemsToRemove;

        /// <summary>
        /// Dispatches when content is moved.
        /// </summary>
        public event Action<Vector2> OnContentMove;
        /// <summary>
        /// Dispatches when IItemVisual is created (not got from pool).
        /// </summary>
        public event Action<IItemVisual> OnVisualCreated;
        
        /// <summary>
        /// Amount of items that can be visible in the list.
        /// </summary>
        public int NumItems => _itemIds.Count;
        public bool IsEmpty => _itemIds.Count <= 0;
        public bool HasItem(int itemId) => _mapItemIdItemData.ContainsKey(itemId);
        public bool IsDisposed => _visualsPool == null;
        public IItemData this[int index] => _mapItemIdItemData[_itemIds[index]];

        public bool IsAutoGap
        {
            get => isAutoGap;
            set
            {
                if (isAutoGap == value) return;
                
                isAutoGap = value;
                _viewport.IsAutoGap = isAutoGap;

                if (isAutoGap)
                {
                    _viewport.UpdateViewportSize();
                    _RefreshVisuals();
                    return;
                }
                
                SetGaps(gap.x, gap.y);
            }
        }

        /// <summary>
        /// Recalculates the viewport metrics and rebuilds the visible items. Awake reads the
        /// viewport RectTransform before the Canvas has laid it out, so a list filled in the same
        /// frame it is created sizes itself against a stale rect until this runs.
        /// </summary>
        public void RefreshViewport()
        {
            _viewport.UpdateViewportSize();
            _RefreshVisuals();
        }

        public void SetGaps(float gapX, float gapY)
        {
            gap.x = gapX;
            gap.y = gapY;

            if (isAutoGap) return;
            
            _viewport.SetGaps(gapX, gapY);
            _viewport.UpdateViewportSize();
            _RefreshVisuals();
        }

        protected virtual void Awake()
        {
            _itemIds = new List<int>(CapacityByDefault);
            _mapIndexVisualItem = new Dictionary<int, VisualData>(CapacityByDefault);
            _mapItemIdVisualItem = new Binder<int, VisualData>(CapacityByDefault);
            _mapItemIdItemData = new Dictionary<int, IItemData>(CapacityByDefault);
            _visualsPool = new Stack<VisualData>(CapacityByDefault);
            _itemsToAdd = new List<int>(CapacityByDefault);
            _itemsToRemove = new List<int>(CapacityByDefault);
            
            _scrollRect = GetComponent<ScrollRect>();
            _scrollRect.onValueChanged.AddListener(_OnContentMoved);
            _prevViewportRect = _scrollRect.viewport.rect;
            
            ListUtil.SetTopLeftAnchor(_scrollRect.content);
            var itemSize = prototypeItem.rect;
            itemSize.width = prototypeSize.x > 0 ? prototypeSize.x : itemSize.width;
            itemSize.height = prototypeSize.y > 0 ? prototypeSize.y : itemSize.height;
            _viewport = new Viewport(_scrollRect.viewport, _scrollRect.content, itemSize);
            _viewport.IsItemFullWidth = isItemFullWidth;
            _viewport.IsAutoGap = isAutoGap;
            
            if (!isAutoGap)
                _viewport.SetGaps(gap.x, gap.y);

            if (minScrollHandleSize > 0)
                minScrollHandleSize = Mathf.Clamp(minScrollHandleSize, 0, 1);
            
            _viewport.UpdateViewportSize();
        }

        public virtual void AddItems<TItemData>(ICollection<TItemData> listItems, bool updateVisuals = true) where TItemData: IItemData 
        {
            foreach (var item in listItems)
                _AddItem(item);

            if (updateVisuals)
                _RefreshVisuals();
        }

        public virtual void AddItem(IItemData itemData, bool updateVisuals = true)
        {
            var itemIndex = _AddItem(itemData);
            if (!updateVisuals) return;
            
            // Check if visual should be added
            if (_viewport.IsIndexVisible(itemIndex))
                _AddVisualItem(itemIndex);

            _viewport.UpdateContentSize(_itemIds.Count);
        }

        /// <summary>
        /// Adds item to the map but not to the index, so no visual refresh.
        /// Returns itemId;
        /// </summary>
        protected int AddItemNoIndex(IItemData itemData)
        {
            return _AddItemToMap(itemData);
        }

        /// <summary>
        /// Removes item by given itemId.
        /// Returns 'true' if removing was successful.
        /// </summary>
        /// <param name="itemId"></param>
        public virtual bool RemoveItem(int itemId)
        {
            if (!_mapItemIdItemData.ContainsKey(itemId)) return false;
                
            _mapItemIdItemData.Remove(itemId);
            _itemIds.RemoveAt(_itemIds.IndexOf(itemId));
                
            _RefreshVisuals();

            return true;
        }

        public void UpdateItem(IItemData itemData)
        {
            if (!_mapItemIdItemData.ContainsKey(itemData.ItemId)) return;

            _mapItemIdItemData[itemData.ItemId] = itemData;

            if (_mapItemIdVisualItem.TryGetValue(itemData.ItemId, out var visualData))
                visualData.Item.OnShow(itemData);
        }

        public void UpdateItemByIndex(IItemData newItemData, int index)
        {
            var oldItem = this[index];
            newItemData.ItemId = oldItem.ItemId;
            
            UpdateItem(newItemData);
        }

        public IItemData GetItem(int itemId)
        {
            return _mapItemIdItemData[itemId];
        }

        public int GetIndex(int itemId)
        {
            return _itemIds.IndexOf(itemId);
        }

        public IItemData FindItem<T>(Func<IItemData, T, bool> finder, T param)
        {
            foreach (var data in _mapItemIdItemData)
            {
                var itemData = data.Value;
                
                if (finder(itemData, param))
                    return itemData;
            }

            return null;
        }

        /// <summary>
        /// Returns a List with given type of items, which match a condition in the filter delegate.
        /// </summary>
        /// <param name="filter">Filtering delegate</param>
        /// <param name="resultList">Provided list for results in order to avoid redundant allocations.</param>
        /// <typeparam name="TItemType"></typeparam>
        /// <returns>Provided resultList</returns>
        public List<TItemType> SelectItems<TItemType>(Func<TItemType, bool> filter, ref List<TItemType> resultList)
        {
            foreach (var data in _mapItemIdItemData)
            {
                var itemData = (TItemType) data.Value;
                if (filter(itemData))
                    resultList.Add(itemData);
            }

            return resultList;
        }

        public int Count<TItemDataType>(Func<TItemDataType, bool> filter)
        {
            var count = 0;
            
            foreach (var data in _mapItemIdItemData)
            {
                var itemData = (TItemDataType) data.Value;

                if (filter(itemData))
                    count++;
            }

            return count;
        }

        public int Count<TItemDataType, TParam>(Func<TItemDataType, TParam, bool> filter, TParam compareParam)
        {
            var count = 0;
            
            foreach (var data in _mapItemIdItemData)
            {
                var itemData = (TItemDataType) data.Value;

                if (filter(itemData, compareParam))
                    count++;
            }

            return count;
        }
        
        public bool Has<TItemDataType>(Func<TItemDataType, bool> filter)
        {
            foreach (var data in _mapItemIdItemData)
            {
                var itemData = (TItemDataType) data.Value;

                if (filter(itemData))
                    return true;
            }

            return false;
        }

        public bool Has<TItemDataType, TParam>(Func<TItemDataType, TParam, bool> filter, TParam param)
        {
            foreach (var data in _mapItemIdItemData)
            {
                var itemData = (TItemDataType) data.Value;

                if (filter(itemData, param))
                    return true;
            }

            return false;
        }

        public void ForEachData(Action<IItemData> forEachDelegate, bool updateVisuals = false)
        {
            foreach (var data in _mapItemIdItemData)
                forEachDelegate(data.Value);
            
            if (updateVisuals)
                UpdateVisuals();
        }

        public void ForEachData(Action<IItemData, int> forEachDelegate, bool updateVisuals = false)
        {
            for (var i = 0; i < NumItems; i++)
            {
                var item = _mapItemIdItemData[_itemIds[i]];
                    forEachDelegate(item, i);
            }
            
            if (updateVisuals)
                UpdateVisuals();
        }

        /// <summary>
        /// Iterates through currently visible IITemVisual.
        /// Provides as first param instance of IItemVisual and the second param related ItemId.
        /// </summary>
        /// <param name="forEachDelegate"></param>
        public void ForEachVisual(Action<IItemVisual, int> forEachDelegate)
        {
            foreach (var data in _mapItemIdVisualItem)
                forEachDelegate(data.Value.Item, data.Key);
        }

        /// <summary>
        /// Refresh currently visible items with their corresponding data.
        /// </summary>
        public void UpdateVisuals()
        {
            foreach (var visualPair in _mapItemIdVisualItem)
            {
                var itemData = _mapItemIdItemData[visualPair.Key];
                var visualData = visualPair.Value;
                visualData.Item.OnShow(itemData);
            }
        }
        
        public void UpdateVisual(int itemId)
        {
            if (_mapItemIdVisualItem.TryGetValue(itemId, out var visualData))
            {
                var itemData = _mapItemIdItemData[itemId];
                visualData.Item.OnShow(itemData);
            }
        }

        public void UpdateVisualByIndex(int itemIndex)
        {
            var itemId = _itemIds[itemIndex];
            UpdateVisual(itemId);
        }

        /// <summary>
        /// Moves item up in the list and returns an index of this item.
        /// Returns -1 if move isn't possible.
        /// </summary>
        /// <param name="moveItemId"></param>
        public int MoveUp(int moveItemId)
        {
            if (!_mapItemIdItemData.ContainsKey(moveItemId)) return -1;
            
            var moveItemIndex = _itemIds.IndexOf(moveItemId);

            if (moveItemIndex == 0) return -1;

            var newIndex = moveItemIndex - 1;
            var oldItemId = _itemIds[newIndex];

            _itemIds[newIndex] = moveItemId;
            _itemIds[moveItemIndex] = oldItemId;

            _RefreshVisuals();
            
            return newIndex;
        }

        /// <summary>
        /// Moves item down in the list and returns an index of this item.
        /// Returns -1 if move isn't possible.
        /// </summary>
        /// <param name="moveItemId"></param>
        public int MoveDown(int moveItemId)
        {
            if (!_mapItemIdItemData.ContainsKey(moveItemId)) return -1;
            
            var moveItemIndex = _itemIds.IndexOf(moveItemId);

            if (moveItemIndex == NumItems - 1) return -1;

            var newIndex = moveItemIndex + 1;
            var oldItemId = _itemIds[newIndex];

            _itemIds[newIndex] = moveItemId;
            _itemIds[moveItemIndex] = oldItemId;

            _RefreshVisuals();
            
            return newIndex;
        }

        public bool Swap(int itemIdA, int itemIdB, out int newItemIdAIndex, out int newItemIdBIndex)
        {
            newItemIdAIndex = -1;
            newItemIdBIndex = -1;
            
            if (itemIdA == itemIdB || 
                !_mapItemIdItemData.ContainsKey(itemIdA) || 
                !_mapItemIdItemData.ContainsKey(itemIdB)) 
                return false;

            var itemIdAIndex = _itemIds.IndexOf(itemIdA);
            var itemIdBIndex = _itemIds.IndexOf(itemIdB);
            
            _itemIds[itemIdAIndex] = itemIdB;
            _itemIds[itemIdBIndex] = itemIdA;

            _RefreshVisuals();

            newItemIdAIndex = itemIdBIndex;
            newItemIdBIndex = itemIdAIndex;
            
            return true;
        }

        public bool Move(int itemId, int newIndexPosition, out int oldIndexPosition)
        {
            oldIndexPosition = -1;
            
            if (newIndexPosition < 0 || newIndexPosition >= _itemIds.Count || !_mapItemIdItemData.ContainsKey(itemId)) return false;
            oldIndexPosition = _itemIds.IndexOf(itemId);

            if (oldIndexPosition == newIndexPosition) return false;

            var dir = oldIndexPosition > newIndexPosition ? -1 : 1;
            var i = oldIndexPosition;

            do
            {
                _itemIds[i] = _itemIds[i + dir];
                i += dir;
            } while (i != newIndexPosition);

            _itemIds[newIndexPosition] = itemId;
                
            _RefreshVisuals();

            return true;
        }

        /// <summary>
        /// It is useful when all the ItemData should be the same but to have ids in diff order.
        /// The visual items will be refreshed accordingly to the new item ids order.
        /// </summary>
        public void Reorder(List<int> itemIds)
        {
            _itemIds.Clear();
            _itemIds.AddRange(itemIds);
            
            _RefreshVisuals();
        }

        /// <summary>
        /// Get a copy of all item ids.
        /// </summary>
        /// <returns></returns>
        public int[] CopyIds()
        {
            return _itemIds.ToArray();
        }

        /// <summary>
        /// Removes all items.
        /// shrinkPoolTo - gives a possibility to shrink pool with visual items to specified number
        /// in order to save memory and remove redundant visual items.
        /// -1 - means pool will not be shrunk.
        /// 0 - all visual items will be destroyed
        /// </summary>
        public virtual void Clear(int shrinkPoolTo = -1)
        {
            _itemIds.Clear();
            _mapItemIdItemData.Clear();
            
            _RemoveAllVisuals(true);
            
            _viewport.UpdateContentSize(_itemIds.Count);

            _ShrinkPoolTo(shrinkPoolTo);
        }

        public void Dispose()
        {
            _itemIds.Clear();
            _itemIds = null;
            
            foreach (var item in _mapIndexVisualItem)
                item.Value.Dispose();

            _mapIndexVisualItem.Clear();
            _mapIndexVisualItem = null;
            
            _mapItemIdVisualItem.Clear();
            _mapItemIdVisualItem = null;
            
            // dispose pool
            foreach (var poolItem in _visualsPool)
                poolItem.Dispose();
            
            _visualsPool.Clear();
            _visualsPool = null;
        }
        
        /// <summary>
        /// Returns index of this item in itemIds;
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int _AddItem(IItemData itemData)
        {
            var itemId = _AddItemToMap(itemData);

            _itemIds.Add(itemId);

            return _itemIds.Count - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int _AddItemToMap(IItemData itemData)
        {
            var itemId = _itemIdCounter++;
            itemData.ItemId = itemId;

            _mapItemIdItemData[itemId] = itemData;
            
            return itemId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _AddVisualItem(int itemIndex)
        {
            var itemId = _itemIds[itemIndex];
            var item = _mapItemIdItemData[itemId];
            var visualData = _GetVisualData(itemIndex);
            visualData.Item.OnShow(item);

            _mapIndexVisualItem.Add(itemIndex, visualData);
            _mapItemIdVisualItem.Add(itemId, visualData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _RefreshVisuals()
        {
            _RemoveAllVisuals(false);

            if (_itemIds.Count == 0) return;

            _viewport.UpdatePosition();
            
            var lastIndex = _itemIds.Count - 1;
            lastIndex = _viewport.LastIndex > lastIndex ? lastIndex : _viewport.LastIndex; 
            
            for (var i = _viewport.TopRowIndex; i <= lastIndex; i++)
                _AddVisualItem(i);
            
            _viewport.UpdateContentSize(_itemIds.Count);
        }
                        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _RemoveAllVisuals(bool isHide)
        {
            foreach (var item in _mapIndexVisualItem)
                _PutToPool(item.Value, isHide);
            
            _mapIndexVisualItem.Clear();
            _mapItemIdVisualItem.Clear();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _ContentMoved()
        {
            var prevTopRowViewportIndex = _viewport.TopRowIndex;
            var prevBottomRowViewportIndex = _viewport.BottomRowIndex;

            _viewport.UpdatePosition();

            var overlappingResult = _viewport.IsOverlapping(prevTopRowViewportIndex, prevBottomRowViewportIndex);

            // the same range
            if (overlappingResult == 1)
                return;

            var lastIndex = _itemIds.Count - 1;

            // completely another range
            if (overlappingResult == -1)
            {
                var endIndex = prevBottomRowViewportIndex + _viewport.NumColumns - 1;
                endIndex = endIndex > lastIndex ? lastIndex : endIndex;
                _RemoveVisualsInRange(prevTopRowViewportIndex, endIndex);

                endIndex = _viewport.LastIndex;
                endIndex = endIndex > lastIndex ? lastIndex : endIndex;
                _AddVisualItems(_viewport.TopRowIndex, endIndex);
            }
            else // partially same range
            {
                var startTopRowIndex = Mathf.Min(prevTopRowViewportIndex, _viewport.TopRowIndex);
                var endTopRowIndex = Mathf.Max(prevBottomRowViewportIndex, _viewport.BottomRowIndex);

                var numColumns = _viewport.NumColumns;
                var rangeEnd = prevBottomRowViewportIndex + numColumns - 1;
                    
                for (var i = startTopRowIndex; i <= endTopRowIndex; i += numColumns)
                {
                    if (i >= prevTopRowViewportIndex && i <= rangeEnd)
                    {
                        if (!_viewport.IsIndexVisible(i))
                            _itemsToRemove.Add(i);
                    }
                    else
                        _itemsToAdd.Add(i);
                }
                    
                foreach (var i in _itemsToRemove)
                {
                    var endIndex = i + numColumns - 1;
                    endIndex = endIndex > lastIndex ? lastIndex : endIndex;
                        
                    _RemoveVisualsInRange(i, endIndex);
                }

                foreach (var i in _itemsToAdd)
                {
                    var endIndex = i + numColumns - 1;
                    endIndex = endIndex > lastIndex ? lastIndex : endIndex;
                        
                    _AddVisualItems(i, endIndex);
                }
                    
                _itemsToAdd.Clear();
                _itemsToRemove.Clear();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _AddVisualItems(int startIndex, int endIndex)
        {
            for (var i = startIndex; i <= endIndex; i++)
                _AddVisualItem(i);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _RemoveVisualsInRange(int startIndex, int endIndex)
        {
            for (var i = startIndex; i <= endIndex; i++)
                _RemoveVisualItem(i);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _RemoveVisualItem(int itemIndex)
        {
            var visualItem = _mapIndexVisualItem[itemIndex];
            _mapIndexVisualItem.Remove(itemIndex);
            _mapItemIdVisualItem.Remove(visualItem);

            _PutToPool(visualItem, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VisualData _GetVisualData(int itemIndex)
        {
            VisualData instance;

            if (_visualsPool.Count > 0)
                instance = _GetFromPool();
            else
            {
                var rectInstance = Instantiate(prototypeItem, _scrollRect.content.transform);
                ListUtil.SetTopLeftAnchor(rectInstance);
                instance = new VisualData(rectInstance);

                OnVisualCreated?.Invoke(instance.Item);
            }

            // SetTopLeftAnchor freezes whatever width the prototype had, so a full-width item has
            // to be resized here — and again on every reuse, because the viewport may have changed
            // while the instance sat in the pool.
            if (isItemFullWidth)
                instance.Rect.sizeDelta = new Vector2(_viewport.ItemWidth, instance.Rect.sizeDelta.y);

            instance.Rect.localPosition = ListUtil.GetCoordinatesByIndex(itemIndex, _viewport.NumColumns,
                _viewport.ItemWidth, _viewport.ItemHeight, _viewport.HGap, _viewport.VGap);

            return instance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _PutToPool(VisualData instance, bool isHide)
        {
            if (isHide || isViewportDynamic)
                instance.Go.SetActive(false);
            else
                instance.Go.transform.localPosition = DockPoint;
            
            instance.Item.OnHide();
            _visualsPool.Push(instance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VisualData _GetFromPool()
        {
            var instance = _visualsPool.Pop();
    
            if (!instance.Go.activeSelf || isViewportDynamic)
                instance.Go.SetActive(true);
            
            return instance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _ShrinkPoolTo(int numItems)
        {
            if (numItems < 0 || numItems >= _visualsPool.Count) return;

            var numShrunkItems = _visualsPool.Count - numItems;
            while (numShrunkItems-- != 0)
                _visualsPool.Pop().Dispose();
        }

        private void _OnContentMoved(Vector2 position)
        {
            if (isViewportDynamic)
            {
                var currentViewportRect = _scrollRect.viewport.rect;
                if (currentViewportRect != _prevViewportRect)
                {
                    // Viewport size has been changed
                    _prevViewportRect = currentViewportRect;
                    _viewport.UpdateViewportSize();
                    _RefreshVisuals();
                }
            }

            if (minScrollHandleSize > 0 && _scrollRect.verticalScrollbar != null)
            {
                var scrollBar = _scrollRect.verticalScrollbar;
                
                if (scrollBar.size < minScrollHandleSize)
                    scrollBar.size = minScrollHandleSize;
            }
            
            _ContentMoved();
            OnContentMove?.Invoke(position);
        }
        
        private sealed class VisualData
        {
            public readonly RectTransform Rect;
            public readonly GameObject Go;
            public IItemVisual Item;
            
            public VisualData(RectTransform rect)
            {
                Rect = rect;
                Go = Rect.gameObject;
                Item = Rect.GetComponent<IItemVisual>();
            }

            public void Dispose()
            {
                Destroy(Go);
                Item = null;
            }
        }
    }
}