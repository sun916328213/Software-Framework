// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：MessagePrompt。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Controls、System.ComponentModel、System.Windows。导入不代表所有类型都会在运行时加载。
using SecsGem.Controls;
using System.ComponentModel;
using System.Windows;

namespace SecsGem.Controls.Terminal
{
    /// <summary>
    /// Window1.xaml 的交互逻辑
    /// </summary>
    public partial class MessagePrompt : Window
    {
        /// <summary>操作员点击确定；关闭窗口或服务释放不触发该事件。</summary>
        public event EventHandler? Confirmed;

        public static new readonly DependencyProperty TitleProperty;
        public static readonly DependencyProperty MessageProperty;
        public static readonly DependencyProperty Visibility_CancelProperty;

        /// <summary>
        /// 标题
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("标题"), DisplayName("标题")]
        public new string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        /// <summary>
        /// 显示信息
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("显示信息"), DisplayName("显示信息")]
        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        /// <summary>
        /// 取消按钮显示/隐藏
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("取消按钮显示/隐藏"), DisplayName("取消按钮显示/隐藏")]
        public Visibility Visibility_Cancel
        {
            get { return (Visibility)GetValue(Visibility_CancelProperty); }
            set { SetValue(Visibility_CancelProperty, value); }
        }


        /// <summary>
        /// 初始化 MessagePrompt 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public MessagePrompt()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 显示对话框
        /// </summary>
        /// <param name="pTitle">标题</param>
        /// <param name="pMessage">显示内容</param>
        /// <param name="pHideCancel">是否显示取消按钮</param>
        public MessagePrompt(string pTitle, string pMessage, Visibility pHideCancel= Visibility.Visible)
        {
           
            InitializeComponent();
            Title = pTitle;
            Message = pMessage;
            Visibility_Cancel  = pHideCancel;
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
        }


        // 用于标记窗口是否以对话框模式显示
        private bool _isDialogMode = false;

        // 重写ShowDialog方法以标记对话框模式
        public new bool? ShowDialog()
        {
            _isDialogMode = true;
            return base.ShowDialog();
        }

        // 重写Show方法以确保不标记为对话框模式
        public new void Show()
        {
            _isDialogMode = false;
            base.Show();
        }
 
        // 新增接受1个参数的Show方法重载，用于直接设置消息内容
        public   void Show(string message)
        {
            _isDialogMode = false;
            this.Message = message;
            base.Show();
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Button_Click。
        /// 正文中的关键协作调用包括 Close；具体条件和先后次序见方法体。
        /// </summary>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Confirmed?.Invoke(this, EventArgs.Empty);
            // 只有当窗口以对话框模式显示时才设置DialogResult
            if (_isDialogMode)
            {
                DialogResult = true;
            }
            this.Close();
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Button_Click_1。
        /// 正文中的关键协作调用包括 Close；具体条件和先后次序见方法体。
        /// </summary>
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // 只有当窗口以对话框模式显示时才设置DialogResult
            if (_isDialogMode)
            {
                DialogResult = false;
            }
            this.Close();
        }
    }
}
