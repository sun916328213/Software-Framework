// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：RfidView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using Device.Controls;
using System.Windows.Controls;

namespace Device.Controls.Views
{
    /// <summary>RFID 测试页视图：纯 XAML，无 code-behind 逻辑。</summary>
    public partial class RfidView : UserControl
    {
        /// <summary>
        /// 初始化 RfidView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public RfidView()
        {
            InitializeComponent();
        }
    }
}
