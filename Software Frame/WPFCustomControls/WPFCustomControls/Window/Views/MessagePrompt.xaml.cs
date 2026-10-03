// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：MessagePrompt。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System.Windows、System.Windows.Media。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System.Windows;
using System.Windows.Media;

namespace WPFCustomControls.Window.Views
{
    /// <summary>
    /// MessagePrompt 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
    /// 继承或实现的契约：System.Windows.Window。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public partial class MessagePrompt : System.Windows.Window
    {
        public static new readonly DependencyProperty TitleProperty;
        public static readonly DependencyProperty MessageProperty;
        public static readonly DependencyProperty Visibility_CancelProperty;
        public static readonly DependencyProperty MessageForegroundProperty;
        public static readonly DependencyProperty TitleForegroundProperty;

        public new string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }
        public Visibility Visibility_Cancel
        {
            get => (Visibility)GetValue(Visibility_CancelProperty);
            set => SetValue(Visibility_CancelProperty, value);
        }
        public Brush MessageForeground
        {
            get => (Brush)GetValue(MessageForegroundProperty);
            set => SetValue(MessageForegroundProperty, value);
        }
        public Brush TitleForeground
        {
            get => (Brush)GetValue(TitleForegroundProperty);
            set => SetValue(TitleForegroundProperty, value);
        }

        /// <summary>
        /// 初始化 MessagePrompt 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public MessagePrompt() => InitializeComponent();

        /// <summary>
        /// 初始化 MessagePrompt 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public MessagePrompt(string pTitle, string pMessage, Visibility pHideCancel = Visibility.Visible)
        {
            InitializeComponent();
            Title = pTitle;
            Message = pMessage;
            Visibility_Cancel = pHideCancel;
        }

        /// <summary>
        /// 初始化 MessagePrompt 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Register；具体条件和先后次序见方法体。
        /// </summary>
        static MessagePrompt()
        {
            TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(MessagePrompt), new PropertyMetadata("提示"));
            MessageProperty = DependencyProperty.Register("Message", typeof(string), typeof(MessagePrompt), new PropertyMetadata(""));
            Visibility_CancelProperty = DependencyProperty.Register("Visibility_Cancel", typeof(Visibility), typeof(MessagePrompt), new PropertyMetadata());
            MessageForegroundProperty = DependencyProperty.Register("MessageForeground", typeof(Brush), typeof(MessagePrompt), new PropertyMetadata(Brushes.Black));
            TitleForegroundProperty = DependencyProperty.Register("TitleForeground", typeof(Brush), typeof(MessagePrompt), new PropertyMetadata(Brushes.Black));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Button_Click。
        /// 正文中的关键协作调用包括 Close；具体条件和先后次序见方法体。
        /// </summary>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Button_Click_1。
        /// 正文中的关键协作调用包括 Close；具体条件和先后次序见方法体。
        /// </summary>
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
