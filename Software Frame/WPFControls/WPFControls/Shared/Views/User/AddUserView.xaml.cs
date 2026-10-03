// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：AddUserView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、WPFControls.Controls.Views、System、System.Collections.Generic、System.Collections.ObjectModel、System.ComponentModel。导入不代表所有类型都会在运行时加载。
using WPFControls;
using WPFControls.Controls.Views;
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

namespace WPFControls.Controls.Views.User
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

        /// <summary>
        /// 初始化 AddUserView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public AddUserView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 初始化 AddUserView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Register；具体条件和先后次序见方法体。
        /// </summary>
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
