using Prism.Ioc;

namespace WPFControl.WindowUI
{
    /// <summary>
    /// 通用主窗口模板
    /// </summary>
    public partial class MainWindow : BaseMainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(IContainerProvider container) : this()
        {
            Container = container;
        }
    }
}
