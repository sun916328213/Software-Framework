// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：Recipe。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core.SQL、DataManager.Runtime.SQL、MachineApp.Business.Flow、MachineApp.Business.Order、MachineApp.Business.Station、MachineApp.Infra.Tool。导入不代表所有类型都会在运行时加载。
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using MachineApp.Business.Flow;
using MachineApp.Business.Order;
using MachineApp.Business.Station;
using MachineApp.Infra.Tool;
using Prism.Ioc;
using System.Windows;
using System.Windows.Controls;

namespace MachineApp.UI.View
{
    /// <summary>
    /// Recipe.xaml 的交互逻辑
    /// </summary>
    public partial class Recipe : UserControl
    {
        /// <summary>
        /// 初始化 Recipe 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public Recipe()
        {
            InitializeComponent();
        }
    }
}