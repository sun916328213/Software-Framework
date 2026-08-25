using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WPFControl.CustomControl.Models;
using WPFControl.ProgramControl.Models;

namespace WPFControl.CustomControl.Views.User
{
    /// <summary>
    /// View_CreateUser.xaml 的交互逻辑
    /// </summary>
    public partial class ModifyUserView : UserControl
    {
        public static readonly DependencyProperty UserNameCollectionProperty;
        public static readonly DependencyProperty UserNameProperty;
        public static readonly DependencyProperty PasswordProperty;
        public static readonly DependencyProperty NewPasswordProperty;
        public static readonly DependencyProperty ConfirmPasswordProperty;
        public static readonly DependencyProperty ErrorMessageProperty;
        public static readonly DependencyProperty ModifyCommandProperty;
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
        [Category("自定义属性"), Description("用户旧密码"), DisplayName("用户旧密码")]
        public string Password
        {
            get { return (string)GetValue(PasswordProperty); }
            set { SetValue(PasswordProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("用户新密码"), DisplayName("用户新密码")]
        public string NewPassword
        {
            get { return (string)GetValue(NewPasswordProperty); }
            set { SetValue(NewPasswordProperty, value); }
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
        [Category("自定义属性"), Description("修改用户信息指令"), DisplayName("修改用户信息指令")]
        public ICommand ModifyCommand
        {
            get { return (ICommand)GetValue(ModifyCommandProperty); }
            set { SetValue(ModifyCommandProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("返回指令"), DisplayName("返回指令")]
        public ICommand ReturnCommand
        {
            get { return (ICommand)GetValue(ReturnCommandProperty); }
            set { SetValue(ReturnCommandProperty, value); }
        }

        public ModifyUserView()
        {
            InitializeComponent();
        }

        static ModifyUserView()
        {
            UserNameCollectionProperty = DependencyProperty.Register("UserNameCollection", typeof(ObservableCollection<string>), typeof(ModifyUserView), new FrameworkPropertyMetadata(null));
            UserNameProperty = DependencyProperty.Register("UserName", typeof(string), typeof(ModifyUserView), new FrameworkPropertyMetadata(null));
            PasswordProperty = DependencyProperty.Register("Password", typeof(string), typeof(ModifyUserView), new FrameworkPropertyMetadata(null));
            NewPasswordProperty = DependencyProperty.Register("NewPassword", typeof(string), typeof(ModifyUserView), new FrameworkPropertyMetadata(null));
            ConfirmPasswordProperty = DependencyProperty.Register("ConfirmPassword", typeof(string), typeof(ModifyUserView), new FrameworkPropertyMetadata(null));
            ErrorMessageProperty = DependencyProperty.Register("ErrorMessage", typeof(string), typeof(ModifyUserView), new FrameworkPropertyMetadata(null));
            ModifyCommandProperty = DependencyProperty.Register("ModifyCommand", typeof(ICommand), typeof(ModifyUserView), new FrameworkPropertyMetadata(null));
            ReturnCommandProperty = DependencyProperty.Register("ReturnCommand", typeof(ICommand), typeof(ModifyUserView), new FrameworkPropertyMetadata(null));
        }
    }
}
