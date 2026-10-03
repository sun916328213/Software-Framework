// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：DeletUserView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、WPFControls.Controls.Views、System.Collections.ObjectModel、System.ComponentModel、System.Windows、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using WPFControls;
using WPFControls.Controls.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WPFControls.Controls.Views.User
{
    /// <summary>
    /// DeletUserView.xaml 的交互逻辑
    /// </summary>
    public partial class DeletUserView : UserControl
    {

        public static readonly DependencyProperty UserNameCollectionProperty;
        public static readonly DependencyProperty UserNameProperty;
        public static readonly DependencyProperty PasswordProperty;
        public static readonly DependencyProperty ConfirmPasswordProperty;
        public static readonly DependencyProperty ErrorMessageProperty;
        public static readonly DependencyProperty DeleteCommandProperty;
        public static readonly DependencyProperty ReturnCommandProperty;

        [Bindable(true)]
        [Category("自定义属性"), Description("用户名称集合"), DisplayName("用户名称集合")]
        public ObservableCollection<string> UserNameCollection
        {
            get { return (ObservableCollection<string>)GetValue(UserNameCollectionProperty); }
            set { SetValue(UserNameCollectionProperty, value); }
        }
 
        [Bindable(true)]
        [Category("自定义属性"), Description("用户名称"), DisplayName("用户名称")]
        public string UserName
        {
            get { return (string)GetValue(UserNameProperty); }
            set { SetValue(UserNameProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("用户密码"), DisplayName("用户密码")]
        public string Password
        {
            get { return (string)GetValue(PasswordProperty); }
            set { SetValue(PasswordProperty, value); }
        }
 
        [Bindable(true)]
        [Category("自定义属性"), Description("用户密码确认"), DisplayName("用户密码确认")]
        public string ConfirmPassword
        {
            get { return (string)GetValue(ConfirmPasswordProperty); }
            set { SetValue(ConfirmPasswordProperty, value); }
        }
 
        [Bindable(true)]
        [Category("自定义属性"), Description("错误信息"), DisplayName("错误信息")]
        public string ErrorMessage
        {
            get { return (string)GetValue(ErrorMessageProperty); }
            set { SetValue(ErrorMessageProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("删除用户信息指令"), DisplayName("删除用户信息指令")]
        public ICommand DeleteCommand
        {
            get { return (ICommand)GetValue(DeleteCommandProperty); }
            set { SetValue(DeleteCommandProperty, value); }
        }
 
        [Bindable(true)]
        [Category("自定义属性"), Description("返回指令"), DisplayName("返回指令")]
        public ICommand ReturnCommand
        {
            get { return (ICommand)GetValue(ReturnCommandProperty); }
            set { SetValue(ReturnCommandProperty, value); }
        }
 
        /// <summary>
        /// 初始化 DeletUserView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public DeletUserView()
        {
            InitializeComponent();
        }
 
        /// <summary>
        /// 初始化 DeletUserView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Register；具体条件和先后次序见方法体。
        /// </summary>
        static DeletUserView()
        {
            UserNameCollectionProperty = DependencyProperty.Register("UserNameCollection", typeof(ObservableCollection<string>), typeof(DeletUserView), new FrameworkPropertyMetadata(null));
            UserNameProperty = DependencyProperty.Register("UserName", typeof(string), typeof(DeletUserView), new FrameworkPropertyMetadata(null));
            PasswordProperty = DependencyProperty.Register("Password", typeof(string), typeof(DeletUserView), new FrameworkPropertyMetadata(null));
            ConfirmPasswordProperty = DependencyProperty.Register("ConfirmPassword", typeof(string), typeof(DeletUserView), new FrameworkPropertyMetadata(null));
            ErrorMessageProperty = DependencyProperty.Register("ErrorMessage", typeof(string), typeof(DeletUserView), new FrameworkPropertyMetadata(null));
            DeleteCommandProperty = DependencyProperty.Register("DeleteCommand", typeof(ICommand), typeof(DeletUserView), new FrameworkPropertyMetadata(null));
            ReturnCommandProperty = DependencyProperty.Register("ReturnCommand", typeof(ICommand), typeof(DeletUserView), new FrameworkPropertyMetadata(null));
        }

    }
}
