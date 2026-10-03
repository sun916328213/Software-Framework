// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：ScrollBarEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Linq、System.Text、System.Threading.Tasks、System.Windows。导入不代表所有类型都会在运行时加载。
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
    /// <summary>
    /// ScrollBarEx 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// </summary>
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
