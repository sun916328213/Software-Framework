// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：IconView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、System、System.Collections.Generic、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using WPFControls;
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

namespace WPFControls.ProgramControl.Views
{
    /// <summary>
    /// IconView.xaml 的交互逻辑
    /// </summary>
    public partial class IconView : UserControl
    {
        /// <summary>
        /// 初始化 IconView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public IconView()
        {
            InitializeComponent();
        }

    
    }
}