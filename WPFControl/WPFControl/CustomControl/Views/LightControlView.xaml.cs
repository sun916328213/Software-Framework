using System.Windows;
using System.Windows.Controls;
using WPFCustomControls.Controls;

namespace WPFControl.CustomControl.Views
{
    /// <summary>
    /// LightControlView.xaml 的交互逻辑
    /// </summary>
    public partial class LightControlView : UserControl
    {
        public LightControlView()
        {
            InitializeComponent();
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