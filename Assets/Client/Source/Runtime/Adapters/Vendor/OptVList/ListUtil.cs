using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Client.Adapters.Vendor
{
    public static class ListUtil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTopLeftAnchor(RectTransform rectTransform)
        {
            _SetAnchor(rectTransform, 0, 1);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTopAnchor(RectTransform rectTransform)
        {
            _SetAnchor(rectTransform, 0.5f, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void _SetAnchor(RectTransform rectTransform, float x, float y)
        {
            //Saving to reapply after anchoring. Width and height changes if anchoring is change. 
            var rect = rectTransform.rect;
            var width = rect.width;
            var height = rect.height;

            //Setting top anchor 
            rectTransform.anchorMin = new Vector2(x, y);
            rectTransform.anchorMax = new Vector2(x, y);
            rectTransform.pivot = new Vector2(x, y);

            //Reapply size
            rectTransform.sizeDelta = new Vector2(width, height);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 GetGridPositionByIndex(int index, int numColumns)
        {
            var j = index / numColumns;
            var i = index - j * numColumns;

            return new Vector2(i, j);
        }
        
        /// <summary>
        /// Returns coordinates in pixels for visual element by given index in grid.
        /// itemWidth/itemHeight - size of the visual element. 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 GetCoordinatesByIndex(int index, int columns, float itemWidth, float itemHeight, float hGap, float vGap)
        {
            var gridIndex = GetGridPositionByIndex(index, columns);
            var xPos = gridIndex.x * itemWidth + (gridIndex.x == 0 ? 0 : hGap * gridIndex.x);
            var yPos = gridIndex.y * itemHeight + (gridIndex.y == 0 ? 0 : vGap * gridIndex.y);

            return new Vector2(xPos, -yPos);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIndexByCoordinates(float x, float y, int columns, float itemWidth, float itemHeight, float hGap, float vGap)
        {
            itemWidth += hGap;
            itemHeight += vGap;
            var i = Math.Abs((int) (x / itemWidth));
            var j = Math.Abs((int) (y / itemHeight));

            return GetIndexByGridPosition(i, j, columns);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 GetGridPositionByCoordinates(float x, float y, float itemWidth, float itemHeight)
        {
            var i = Math.Abs((int) (x / itemWidth));
            var j = Math.Abs((int) (y / itemHeight));

            return new Vector2(i, j);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIndexByGridPosition(int i, int j, int columns)
        {
            return i + j * columns;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CalculateColumns(float viewportWidth, float itemWidth)
        {
            var numColumns = (int)(viewportWidth / itemWidth);
            return numColumns > 0 ? numColumns : 1;
        } 
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CalculateColumns(float viewportWidth, float itemWidth, float hGap)
        {
            var numColumns = (int)(viewportWidth / itemWidth);
        
            var coef = viewportWidth / (itemWidth * numColumns + hGap * (numColumns - 1));
            numColumns = (int)(numColumns * coef);
        
            return numColumns > 0 ? numColumns : 1;
        }
    }
}