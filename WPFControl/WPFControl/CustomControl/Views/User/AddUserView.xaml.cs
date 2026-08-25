using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace WPFControl.CustomControl.Views.User
{
    /// <summary>
    /// View_CreateUser.xaml 的交互逻辑
    /// </summary>
    public partial class AddUserView : UserControl
    {
        public static readonly DependencyProperty UserNameProperty;
        public static readonly DependencyProperty PasswordProperty;
        public static readonly DependencyProperty ConfirmPasswordProperty;
        public static readonly DependencyProperty UserLevelCollectionProperty;
        public static readonly DependencyProperty UserLevelProperty;
        public static readonly DependencyProperty ErrorMessageProperty;
        public static readonly DependencyProperty AddCommandProperty;
        public static readonly DependencyProperty ReturnCommandProperty;

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
        [Category("自定义属性"), Description("用户等级集合"), DisplayName("用户等级集合")]
        public ObservableCollection<string> UserLevelCollection
        {
            get { return (ObservableCollection<string>)GetValue(UserLevelCollectionProperty); }
            set { SetValue(UserLevelCollectionProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("用户等级"), DisplayName("用户等级")]
        public string UserLevel
        {
            get { return (string)GetValue(UserLevelProperty); }
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
        [Category("自定义属性"), Description("添加用户信息指令"), DisplayName("添加用户信息指令")]
        public ICommand AddCommand
        {
            get { return (ICommand)GetValue(AddCommandProperty); }
            set { SetValue(AddCommandProperty, value); }
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("返回指令"), DisplayName("返回指令")]
        public ICommand ReturnCommand
        {
            get { return (ICommand)GetValue(ReturnCommandProperty); }
            set { SetValue(ReturnCommandProperty, value); }
        }

        public AddUserView()
        {
            InitializeComponent();
        }

        static AddUserView()
        {
            UserNameProperty = DependencyProperty.Register("UserName", typeof(string), typeof(AddUserView), new FrameworkPropertyMetadata(null));
            PasswordProperty = DependencyProperty.Register("Password", typeof(string), typeof(AddUserView), new FrameworkPropertyMetadata(null));
            ConfirmPasswordProperty = DependencyProperty.Register("ConfirmPassword", typeof(string), typeof(AddUserView), new FrameworkPropertyMetadata(null));
            UserLevelCollectionProperty = DependencyProperty.Register("UserLevelCollection", typeof(ObservableCollection<string>), typeof(AddUserView), new FrameworkPropertyMetadata(null));
            UserLevelProperty = DependencyProperty.Register("UserLevel", typeof(string), typeof(AddUserView), new FrameworkPropertyMetadata(null));
            ErrorMessageProperty = DependencyProperty.Register("ErrorMessage", typeof(string), typeof(AddUserView), new FrameworkPropertyMetadata(null));
            AddCommandProperty = DependencyProperty.Register("AddCommand", typeof(ICommand), typeof(AddUserView), new FrameworkPropertyMetadata(null));
            ReturnCommandProperty = DependencyProperty.Register("ReturnCommand", typeof(ICommand), typeof(AddUserView), new FrameworkPropertyMetadata(null));
        }
    }
}
