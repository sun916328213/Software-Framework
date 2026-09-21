using System.Windows;
using System.Windows.Controls;

namespace Device.Controls.Views.Shared
{
    /// <summary>
    /// 运行输出卡：输出框 + 耗时 + 清空（绑定走继承来的 DataContext，
    /// 约定 OutputText / LastElapsed / ClearOutputCommand，无 VM 类型耦合）。
    /// </summary>
    public partial class RunOutputView : UserControl
    {
        public static readonly DependencyProperty OutputHeightProperty =
            DependencyProperty.Register(nameof(OutputHeight), typeof(double), typeof(RunOutputView),
                new PropertyMetadata(130.0));

        /// <summary>输出框的**最小**高度；宿主行是 `*` 时会跟着长高。</summary>
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
