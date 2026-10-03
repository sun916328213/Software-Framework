// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：ToolBrowserView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、System.Windows.Controls、System.Windows.Data。导入不代表所有类型都会在运行时加载。
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Vision.Workbench
{
    /// <summary>
    /// ToolBrowserView.xaml 的交互逻辑（P9 新 Tool 标准入口视图；只做 DP 透传）。
    /// </summary>
    public partial class ToolBrowserView : UserControl
    {
        /// <summary>绑定的浏览器 VM。</summary>
        public static readonly DependencyProperty BrowserProperty =
            DependencyProperty.Register(nameof(Browser), typeof(ToolBrowserViewModel), typeof(ToolBrowserView), new PropertyMetadata(null));

        /// <summary>绑定的浏览器 VM。</summary>
        public ToolBrowserViewModel? Browser
        {
            get => (ToolBrowserViewModel?)GetValue(BrowserProperty);
            set => SetValue(BrowserProperty, value);
        }

        /// <summary>构造。</summary>
        public ToolBrowserView()
        {
            InitializeComponent();
            SetBinding(BrowserProperty, new Binding());
        }
    }
}
