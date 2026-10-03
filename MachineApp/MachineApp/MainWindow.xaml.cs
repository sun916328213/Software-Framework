// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：MainWindow。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MachineApp.Business.Flow、Prism.Ioc、System、System.Threading.Tasks、WPFControls.ProgramControl.Services、WPFControls.WindowUI。导入不代表所有类型都会在运行时加载。
using MachineApp.Business.Flow;
using Prism.Ioc;
using System;
using System.Threading.Tasks;
using WPFControls.ProgramControl.Services;
using WPFControls.WindowUI;
using WPFCustomControls.Window.Views;

namespace MachineApp
{
    /// <summary>
    /// 主窗口
    /// </summary>
    public partial class MainWindow : BaseMainWindow
    {

        /// <summary>
        /// 初始化 MainWindow 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public MainWindow(IContainerProvider container)
        {

            Container = container;
            InitializeComponent();
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnWindowLoaded。
        /// 正文中的关键协作调用包括 OnWindowLoaded；具体条件和先后次序见方法体。
        /// </summary>
        protected override void OnWindowLoaded()
        {
            base.OnWindowLoaded();
            WindowMenuService?.UpdataTitle("12寸FOSB自动包装机");
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 DockPanel_PreviewMouseLeftButtonDown。
        /// 正文中的关键协作调用包括 Resolve、InitControl；具体条件和先后次序见方法体。
        /// </summary>
        private void DockPanel_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != System.Windows.Input.MouseButton.Right) return;
            var d = Container.Resolve<Init>();
            d.InitControl();
        }
    }
}