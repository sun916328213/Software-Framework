// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：PasswordBoxEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using System.Windows;
using System.Windows.Controls;

namespace Extension.UI.Controls
{
    /// <summary>
    /// PasswordBoxEx 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// </summary>
    public class PasswordBoxEx
    {
        public static readonly DependencyProperty PasswordProperty =
           DependencyProperty.RegisterAttached("Password",
           typeof(string), typeof(PasswordBoxEx),
           new FrameworkPropertyMetadata(string.Empty, OnPasswordPropertyChanged));

        public static readonly DependencyProperty AttachProperty =
            DependencyProperty.RegisterAttached("Attach",
            typeof(bool), typeof(PasswordBoxEx), new FrameworkPropertyMetadata(false, Attach));

        private static readonly DependencyProperty IsUpdatingProperty =
           DependencyProperty.RegisterAttached("IsUpdating", typeof(bool),
           typeof(PasswordBoxEx));


        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetAttach。
        /// 正文中的关键协作调用包括 SetValue；具体条件和先后次序见方法体。
        /// </summary>
        public static void SetAttach(DependencyObject dp, bool value)
        {
            dp.SetValue(AttachProperty, value);
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetAttach。
        /// 正文中的关键协作调用包括 GetValue；具体条件和先后次序见方法体。
        /// </summary>
        public static bool GetAttach(DependencyObject dp)
        {
            return (bool)dp.GetValue(AttachProperty);
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetPassword。
        /// 正文中的关键协作调用包括 GetValue；具体条件和先后次序见方法体。
        /// </summary>
        public static string GetPassword(DependencyObject dp)
        {
            return (string)dp.GetValue(PasswordProperty);
        }

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetPassword。
        /// 正文中的关键协作调用包括 SetValue；具体条件和先后次序见方法体。
        /// </summary>
        public static void SetPassword(DependencyObject dp, string value)
        {
            dp.SetValue(PasswordProperty, value);
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetIsUpdating。
        /// 正文中的关键协作调用包括 GetValue；具体条件和先后次序见方法体。
        /// </summary>
        private static bool GetIsUpdating(DependencyObject dp)
        {
            return (bool)dp.GetValue(IsUpdatingProperty);
        }

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetIsUpdating。
        /// 正文中的关键协作调用包括 SetValue；具体条件和先后次序见方法体。
        /// </summary>
        private static void SetIsUpdating(DependencyObject dp, bool value)
        {
            dp.SetValue(IsUpdatingProperty, value);
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnPasswordPropertyChanged。
        /// 正文中的关键协作调用包括 GetIsUpdating；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnPasswordPropertyChanged(DependencyObject sender,
            DependencyPropertyChangedEventArgs e)
        {
            PasswordBox passwordBox = sender as PasswordBox;
            passwordBox.PasswordChanged -= PasswordChanged;

            if (!(bool)GetIsUpdating(passwordBox))
            {
                passwordBox.Password = (string)e.NewValue;
            }
            passwordBox.PasswordChanged += PasswordChanged;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Attach。
        /// </summary>
        private static void Attach(DependencyObject sender,
            DependencyPropertyChangedEventArgs e)
        {
            PasswordBox passwordBox = sender as PasswordBox;

            if (passwordBox == null)
                return;

            if ((bool)e.OldValue)
            {
                passwordBox.PasswordChanged -= PasswordChanged;
            }

            if ((bool)e.NewValue)
            {
                passwordBox.PasswordChanged += PasswordChanged;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 PasswordChanged。
        /// 正文中的关键协作调用包括 SetIsUpdating、SetPassword；具体条件和先后次序见方法体。
        /// </summary>
        private static void PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordBox passwordBox = sender as PasswordBox;
            SetIsUpdating(passwordBox, true);
            SetPassword(passwordBox, passwordBox.Password);
            SetIsUpdating(passwordBox, false);
        }
    }
}
