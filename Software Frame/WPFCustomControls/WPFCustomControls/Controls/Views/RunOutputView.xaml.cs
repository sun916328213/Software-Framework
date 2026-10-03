// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：RunOutputView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System.Windows、System.Windows.Controls、System.Windows.Input。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WPFCustomControls.Controls
{
    /// <summary>
    /// 运行输出卡：输出框 + 耗时 + 导出/清空。
    /// 宿主通过 <see cref="Text"/> / <see cref="Elapsed"/> / <see cref="ClearCommand"/> / <see cref="ExportCommand"/> 显式绑定，控件不依赖任何 VM 约定。
    /// </summary>
    public partial class RunOutputView : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(RunOutputView),
                new PropertyMetadata("运行输出"));

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(RunOutputView),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ElapsedProperty =
            DependencyProperty.Register(nameof(Elapsed), typeof(string), typeof(RunOutputView),
                new PropertyMetadata("-"));

        public static readonly DependencyProperty ClearCommandProperty =
            DependencyProperty.Register(nameof(ClearCommand), typeof(ICommand), typeof(RunOutputView),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ExportCommandProperty =
            DependencyProperty.Register(nameof(ExportCommand), typeof(ICommand), typeof(RunOutputView),
                new PropertyMetadata(null));

        public static readonly DependencyProperty OutputHeightProperty =
            DependencyProperty.Register(nameof(OutputHeight), typeof(double), typeof(RunOutputView),
                new PropertyMetadata(130.0));

        /// <summary>卡片标题，默认「运行输出」。</summary>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>输出框内容（只读展示）。</summary>
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        /// <summary>耗时文本，显示为「耗时：{值}」。</summary>
        public string Elapsed
        {
            get => (string)GetValue(ElapsedProperty);
            set => SetValue(ElapsedProperty, value);
        }

        /// <summary>「清空」按钮命令。</summary>
        public ICommand ClearCommand
        {
            get => (ICommand)GetValue(ClearCommandProperty);
            set => SetValue(ClearCommandProperty, value);
        }

        /// <summary>「导出」按钮命令；不设置时按钮自动隐藏。</summary>
        public ICommand ExportCommand
        {
            get => (ICommand)GetValue(ExportCommandProperty);
            set => SetValue(ExportCommandProperty, value);
        }

        /// <summary>输出框**最小**高度；宿主行是 `*` 时会跟着长高，否则等于固定高度。</summary>
        public double OutputHeight
        {
            get => (double)GetValue(OutputHeightProperty);
            set => SetValue(OutputHeightProperty, value);
        }

        /// <summary>
        /// 初始化 RunOutputView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public RunOutputView()
        {
            InitializeComponent();
        }
    }
}
