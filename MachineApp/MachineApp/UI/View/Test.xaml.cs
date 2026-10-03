// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：Test。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core.SQL、DataManager.Runtime.SQL、Prism.Ioc、System、System.Collections.Generic、System.Linq。导入不代表所有类型都会在运行时加载。
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;

namespace MachineApp.UI.View
{
    /// <summary>
    /// Test.xaml 的交互逻辑
    /// </summary>
    public partial class Test : UserControl
    {
        IContainerProvider containerProvider;
        /// <summary>
        /// 初始化 Test 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public Test(IContainerProvider  container)
        {
            InitializeComponent();
            containerProvider = container;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Button_Click。
        /// 正文中的关键协作调用包括 Resolve、SaveStatistics；具体条件和先后次序见方法体。
        /// </summary>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var d1 = containerProvider.Resolve<CapacityStatisticsService>();
            d1.SaveStatistics(DateTime.Now, true);
        }
    }
}
