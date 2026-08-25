using System.Collections.ObjectModel;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WPFControl.CustomControl.Models;

namespace WPFControl.CustomControl.Views.User
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

        public LoginView()
        {
            InitializeComponent();
        }

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
