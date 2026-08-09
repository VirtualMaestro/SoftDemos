using System.Runtime.CompilerServices;
using UnityEngine;

namespace Game.Adapters.Bindings
{
    internal sealed class Viewport
    {
        private readonly RectTransform _viewport;
        private readonly RectTransform _content;
        private Rect _itemSize;
        private Rect _viewportRect;

        private int _topRowIndex;
        private int _bottomRowIndex;
        private int _lastIndex;

        private float _contentPosY;
        private int _contentHeight;
        private int _numContentItems;
        private int _numColumns;
        private float _hGap;
        private float _vGap;
        private bool _isAutoGap;
        private bool _isItemFullWidth;

        public float HGap => _hGap;
        public float VGap => _vGap;
        public int NumColumns => _numColumns;
        public float ItemWidth => _itemSize.width;
        public float ItemHeight => _itemSize.height;
        public int TopRowIndex => _topRowIndex;
        public int BottomRowIndex => _bottomRowIndex;
        public int LastIndex => _lastIndex;

        public Viewport(RectTransform viewport, RectTransform content, Rect itemSize)
        {
            _viewport = viewport;
            _content = content;
            _itemSize = itemSize;

            _content.localPosition = Vector3.zero;
            
            _RecalculateColumns();
            _UpdateIndices();
        }

        /// <summary>
        /// Item width follows the viewport, so the list stays one column wide at any screen size.
        /// </summary>
        public bool IsItemFullWidth
        {
            get => _isItemFullWidth;
            set
            {
                if (_isItemFullWidth == value) return;

                _isItemFullWidth = value;
                UpdateViewportSize();
            }
        }

        public bool IsAutoGap
        {
            get => _isAutoGap;
            set
            {
                if (_isAutoGap == value) return;
                
                _isAutoGap = value;
                
                if (_isAutoGap)
                    _CalculateGaps();
            }
        }

        public void SetGaps(float hGap, float vGap)
        {
            _hGap = hGap;
            _vGap = vGap;
        }

        public void UpdatePosition()
        {
            _contentPosY = _content.localPosition.y;
            _UpdateIndices();
        }

        public void UpdateContentSize(int numContentItems)
        {
            if (_numContentItems == numContentItems) return;
            _numContentItems = numContentItems;

            _RecalculateContentHeight();
        }

        public void UpdateViewportSize()
        {
            if (_isAutoGap)
                _CalculateGaps();
            
            _RecalculateColumns();
            _RecalculateContentHeight();
            UpdatePosition();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _CalculateGaps()
        {
            var numItemsInRow = Mathf.Max(1, (int)(_viewportRect.width / ItemWidth));
            var gapLength = _viewportRect.width - numItemsInRow * ItemWidth;

            _hGap = gapLength / numItemsInRow;
            _vGap = _hGap;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _RecalculateContentHeight()
        {
            _contentHeight = _GetContentHeight(_numContentItems);
            // The content spans the viewport: this is a vertical list, so its width is never
            // authored — keeping the previous sizeDelta.x would freeze whichever screen size the
            // scene happened to be saved at.
            _content.sizeDelta = new Vector2(_viewportRect.width, _contentHeight);

            ListUtil.SetTopLeftAnchor(_content);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsIndexVisible(int itemIndex)
        {
            return itemIndex >= _topRowIndex && itemIndex <= _lastIndex;
        }

        /// <summary>
        /// Checks if given range overlaps current range.
        /// Returns:
        ///   -1 if not overlaps at all.
        ///    0 if overlaps partially.
        ///    1 if has the same size.
        /// 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IsOverlapping(int topRowIndex, int bottomRowIndex)
        {
            if (_topRowIndex == topRowIndex && _bottomRowIndex == bottomRowIndex) return 1;
            if (_topRowIndex > bottomRowIndex || _bottomRowIndex < topRowIndex) return -1;

            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _RecalculateColumns()
        {
            _viewportRect = _viewport.rect;

            if (_isItemFullWidth)
                _itemSize.width = _viewportRect.width;

            _numColumns = ListUtil.CalculateColumns(_viewportRect.width, _itemSize.width, _hGap);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _UpdateIndices()
        {
            _CalculateVisibleRowIndices(out _topRowIndex, out _bottomRowIndex);
            _lastIndex = _bottomRowIndex + _numColumns - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int _GetContentHeight(int numContentItems)
        {
            if (numContentItems <= 0)
                return 0;

            var position = ListUtil.GetCoordinatesByIndex(
                numContentItems - 1,
                NumColumns,
                _itemSize.width, _itemSize.height, _hGap, _vGap);

            position.y = Mathf.Abs(position.y) + _itemSize.height;
            return Mathf.CeilToInt(position.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _CalculateVisibleRowIndices(out int topRowIndex, out int bottomRowIndex)
        {
            topRowIndex =
                ListUtil.GetIndexByCoordinates(0, _contentPosY, _numColumns, _itemSize.width, _itemSize.height, _hGap, _vGap);
            bottomRowIndex = ListUtil.GetIndexByCoordinates(0, _contentPosY + _viewportRect.height, _numColumns,
                _itemSize.width, _itemSize.height, _hGap, _vGap);
        }
    }
}