using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Orientation = System.Windows.Controls.Orientation;

namespace Extension.UI.Controls
{
    public static class ScrollBarEx
    {
        // 辅助方法：检查ScrollBar是否实际可见
        public static bool  ScrollBarVisible(this FrameworkElement element, Orientation orientation)
        {
            // 查找ListBox内部的ScrollViewer
            var scrollViewer = FindVisualChildEx.FindVisualChild<ScrollViewer>(element);
            if (scrollViewer == null) return false;

            // 通过比较Extent和Viewport来判断滚动条是否可见
            if (orientation == Orientation.Vertical)
            {
                return scrollViewer.ExtentHeight > scrollViewer.ViewportHeight;
            }
            else
            {
                return scrollViewer.ExtentWidth > scrollViewer.ViewportWidth;
            }
        }

        // 辅助方法：检查ScrollBar控件的实际可见性
        public static bool  ScrollBarControlVisible(this FrameworkElement element, Orientation orientation)
        {
            // 查找ScrollBar控件
            var scrollViewer = FindVisualChildEx.FindVisualChild<ScrollViewer>(element);
            if (scrollViewer == null) return false;

            if (orientation == Orientation.Vertical)
            {
                return scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible;
            }
            else
            {
                return scrollViewer.ComputedHorizontalScrollBarVisibility == Visibility.Visible;
            }
        }
 
    }
}
