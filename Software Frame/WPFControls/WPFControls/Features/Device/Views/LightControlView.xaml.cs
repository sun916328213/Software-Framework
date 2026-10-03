// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：LightControlView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、System.Windows、System.Windows.Controls、WPFCustomControls.Controls。导入不代表所有类型都会在运行时加载。
using WPFControls;
using System.Windows;
using System.Windows.Controls;
using WPFCustomControls.Controls;

namespace WPFControls.ProgramControl.Views
{
    /// <summary>
    /// LightControlView.xaml 的交互逻辑
    /// </summary>
    public partial class LightControlView : UserControl
    {
        /// <summary>
        /// 初始化 LightControlView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public LightControlView(Device.Controls.Services.LightWorkspace workspace)
        {
            InitializeComponent();
            DevicePage.DataContext = new Device.Controls.ViewModels.LightViewModel(workspace);
        }

        public static readonly DependencyProperty LightControlItemSourceProperty =
          DependencyProperty.Register("LightControlItemSource", typeof(IEnumerable<string>), typeof(LightControlView));

        /// <summary>
        /// 光源控制器名称集合
        /// </summary>
        public IEnumerable<string> LightControlItemSource
        {
            get { return (IEnumerable<string>)GetValue(LightControlItemSourceProperty); }
            set { SetValue(LightControlItemSourceProperty, value); }
        }


        public static readonly DependencyProperty LightControlSelectItemProperty =
          DependencyProperty.Register("LightControlSelectItem", typeof(string), typeof(LightControlView));

        /// <summary>
        /// 光源控制器选中名称
        /// </summary>
        public string LightControlSelectItem
        {
            get { return (string)GetValue(LightControlSelectItemProperty); }
            set { SetValue(LightControlSelectItemProperty, value); }
        }

        public static readonly DependencyProperty LightControlChannelSourceItemProperty =
         DependencyProperty.Register("LightControlChannelSourceItem", typeof(IEnumerable<string>), typeof(LightControlView));

        /// <summary>
        /// 光源控制器通道名称集合
        /// </summary>
        public IEnumerable<string> LightControlChannelSourceItem
        {
            get { return (IEnumerable<string>)GetValue(LightControlChannelSourceItemProperty); }
            set { SetValue(LightControlChannelSourceItemProperty, value); }
        }
    }
}