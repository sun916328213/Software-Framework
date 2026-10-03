// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：FindVisualChildEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Linq、System.Text、System.Threading.Tasks、System.Windows。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
 

namespace Extension.UI.Controls
{
    /// <summary>
    /// 查找子控件
    /// </summary>
    public static class FindVisualChildEx
    {
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 FindVisualChild。
        /// 正文中的关键协作调用包括 GetChildrenCount、GetChild、FindVisualChild；具体条件和先后次序见方法体。
        /// </summary>
        public static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
