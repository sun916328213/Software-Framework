// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：AddLabelInfo。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Linq、System.Text、System.Threading.Tasks、System.Windows。导入不代表所有类型都会在运行时加载。
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
using System.Windows.Shapes;

namespace MachineApp.UI.WindowUI
{
    /// <summary>
    /// AddLabelInfo.xaml 的交互逻辑
    /// </summary>
    public partial class AddLabelInfo : Window
    {
        /// <summary>
        /// 初始化 AddLabelInfo 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public AddLabelInfo()
        {
            InitializeComponent();
        }
    }
}
