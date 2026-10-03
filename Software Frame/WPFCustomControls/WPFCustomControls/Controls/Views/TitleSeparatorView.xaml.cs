// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：TitleSeparatorView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System、System.Collections.Generic、System.ComponentModel、System.Linq、System.Text。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace WPFCustomControls.Controls
{
    /// <summary>
    /// TitleView.xaml 的交互逻辑
    /// </summary>
    public partial class TitleSeparatorView : UserControl
    {

        public static readonly DependencyProperty TitleProperty;

        [Bindable(true)]
        [Category("自定义属性"), Description("标题"), DisplayName("标题")]
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        /// <summary>
        /// 初始化 TitleSeparatorView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public TitleSeparatorView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 初始化 TitleSeparatorView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Register；具体条件和先后次序见方法体。
        /// </summary>
        static TitleSeparatorView()
        {
            TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(TitleSeparatorView), new PropertyMetadata(null));
        }
    }
}
