using Prism.Ioc;
using System;
using System.Threading.Tasks;
using WPFControl.ProgramControl.Services;
using WPFControl.WindowUI;
using WPFCustomControls.Window.Views;

namespace Packing_Machine
{
    /// <summary>
    /// 主窗口
    /// </summary>
    public partial class MainWindow : BaseMainWindow
    {

        public MainWindow(IContainerProvider container)
        {

            Container = container;
            InitializeComponent();
        }

        protected override void OnWindowLoaded()
        {
            base.OnWindowLoaded();
            WindowMenuService?.UpdataTitle("12寸FOSB自动包装机");
        }
    }
}