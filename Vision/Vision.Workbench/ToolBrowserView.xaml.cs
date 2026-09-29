using System.Windows;
using System.Windows.Controls;

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
        }
    }
}
