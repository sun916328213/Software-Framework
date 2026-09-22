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

        public RunOutputView()
        {
            InitializeComponent();
        }
    }
}
