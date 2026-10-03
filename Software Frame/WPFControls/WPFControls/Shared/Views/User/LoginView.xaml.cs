// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：LoginView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、WPFControls.Controls.Views、System.Collections.ObjectModel、System、System.ComponentModel、System.Linq。导入不代表所有类型都会在运行时加载。
using WPFControls;
using WPFControls.Controls.Views;
using System.Collections.ObjectModel;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WPFControls.Controls.Models;

namespace WPFControls.Controls.Views.User
{
    /// <summary>
    /// View_Login.xaml 的交互逻辑
    /// </summary>
    public partial class LoginView : UserControl
    {
        public static readonly DependencyProperty UserNameCollectionProperty;
        public static readonly DependencyProperty UserNameProperty;
        public static readonly DependencyProperty PasswordProperty;
        public static readonly DependencyProperty UserLevelProperty;
        public static readonly DependencyProperty ErrorMessageProperty;

        public static readonly DependencyProperty LoginCommandProperty;
        public static readonly DependencyProperty SelectLoginUserCommandProperty;
        public static readonly DependencyProperty AddCommandProperty;
        public static readonly DependencyProperty DeleteCommandProperty;
        public static readonly DependencyProperty ModifyCommandProperty;
        public static readonly DependencyProperty LogoutCommandProperty;
        public static readonly DependencyProperty ExitCommandProperty;

        private bool _isRefreshingUserNames;
        private bool _isSelectingUser;

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
        [Category("自定义属性"), Description("用户等级"), DisplayName("用户等级")]
        public EUserLevel UserLevel
        {
            get { return (EUserLevel)GetValue(UserLevelProperty); }
            set { SetValue(UserLevelProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("错误信息"), DisplayName("错误信息")]
        public string ErrorMessage
        {
            get { return (string)GetValue(ErrorMessageProperty); }
            set { SetValue(ErrorMessageProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("用户登入指令"), DisplayName("用户登入指令")]
        public ICommand LoginCommand
        {
            get { return (ICommand)GetValue(LoginCommandProperty); }
            set { SetValue(LoginCommandProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("选择登录用户指令"), DisplayName("选择登录用户指令")]
        public ICommand SelectLoginUserCommand
        {
            get { return (ICommand)GetValue(SelectLoginUserCommandProperty); }
            set { SetValue(SelectLoginUserCommandProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("添加用户指令"), DisplayName("添加用户指令")]
        public ICommand AddCommand
        {
            get { return (ICommand)GetValue(AddCommandProperty); }
            set { SetValue(AddCommandProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("修改用户指令"), DisplayName("修改用户指令")]
        public ICommand ModifyCommand
        {
            get { return (ICommand)GetValue(ModifyCommandProperty); }
            set { SetValue(ModifyCommandProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("删除用户指令"), DisplayName("删除用户指令")]
        public ICommand DeleteCommand
        {
            get { return (ICommand)GetValue(DeleteCommandProperty); }
            set { SetValue(DeleteCommandProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("注销登录指令"), DisplayName("注销登录指令")]
        public ICommand LogoutCommand
        {
            get { return (ICommand)GetValue(LogoutCommandProperty); }
            set { SetValue(LogoutCommandProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("退出指令"), DisplayName("退出指令")]
        public ICommand ExitCommand
        {
            get { return (ICommand)GetValue(ExitCommandProperty); }
            set { SetValue(ExitCommandProperty, value); }
        }

        /// <summary>
        /// 初始化 LoginView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public LoginView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 初始化 LoginView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Register；具体条件和先后次序见方法体。
        /// </summary>
        static LoginView()
        {
            UserNameCollectionProperty = DependencyProperty.Register("UserNameCollection", typeof(ObservableCollection<string>), typeof(LoginView), new FrameworkPropertyMetadata(null));

            UserNameProperty = DependencyProperty.Register("UserName", typeof(string), typeof(LoginView), new FrameworkPropertyMetadata(null));
            PasswordProperty = DependencyProperty.Register("Password", typeof(string), typeof(LoginView), new FrameworkPropertyMetadata(null));
            UserLevelProperty = DependencyProperty.Register("UserLevel", typeof(EUserLevel), typeof(LoginView), new FrameworkPropertyMetadata(null));

            ErrorMessageProperty = DependencyProperty.Register("ErrorMessage", typeof(string), typeof(LoginView), new FrameworkPropertyMetadata(null));
            LoginCommandProperty = DependencyProperty.Register("LoginCommand", typeof(ICommand), typeof(LoginView), new FrameworkPropertyMetadata(null));
            SelectLoginUserCommandProperty = DependencyProperty.Register("SelectLoginUserCommand", typeof(ICommand), typeof(LoginView), new FrameworkPropertyMetadata(null));
            AddCommandProperty = DependencyProperty.Register("AddCommand", typeof(ICommand), typeof(LoginView), new FrameworkPropertyMetadata(null));
            DeleteCommandProperty = DependencyProperty.Register("DeleteCommand", typeof(ICommand), typeof(LoginView), new FrameworkPropertyMetadata(null));
            ModifyCommandProperty = DependencyProperty.Register("ModifyCommand", typeof(ICommand), typeof(LoginView), new FrameworkPropertyMetadata(null));
            LogoutCommandProperty = DependencyProperty.Register("LogoutCommand", typeof(ICommand), typeof(LoginView), new FrameworkPropertyMetadata(null));
            ExitCommandProperty = DependencyProperty.Register("ExitCommand", typeof(ICommand), typeof(LoginView), new FrameworkPropertyMetadata(null));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 LoginView_PreviewKeyDown。
        /// 正文中的关键协作调用包括 Execute；具体条件和先后次序见方法体。
        /// </summary>
        private void LoginView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None || LoginCommand?.CanExecute(null) != true)
            {
                return;
            }

            e.Handled = true;
            LoginCommand.Execute(null);
        }

    }
}
