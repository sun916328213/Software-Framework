// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：HeaderRenameInfo、HeaderRenameEventArgs、EditTabControl。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System.Collections.Generic、System.Windows、System.Windows.Controls、System.Windows.Controls.Primitives、System.Windows.Input。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;

namespace WPFCustomControls.Controls
{
    /// <summary>
    /// HeaderRenameInfo 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
    /// </summary>
    public class HeaderRenameInfo
    {
        public object? DataItem { get; set; }
        public string OldText { get; set; } = string.Empty;
        public string NewText { get; set; } = string.Empty;
        public bool Cancel { get; set; }
    }

    /// <summary>
    /// HeaderRenameEventArgs 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
    /// 继承或实现的契约：RoutedEventArgs。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class HeaderRenameEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// 初始化 HeaderRenameEventArgs 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public HeaderRenameEventArgs(RoutedEvent routedEvent, object source, HeaderRenameInfo renameInfo)
            : base(routedEvent, source) => RenameInfo = renameInfo;
        public HeaderRenameInfo RenameInfo { get; }
    }

    /// <summary>
    /// EditTabControl 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
    /// 继承或实现的契约：TabControl。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public partial class EditTabControl : TabControl
    {
        /// <summary>
        /// 初始化 EditTabControl 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public EditTabControl() => InitializeComponent();

        public static readonly RoutedEvent HeaderRenamedEvent =
            EventManager.RegisterRoutedEvent("HeaderRenamed", RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(EditTabControl));

        public event RoutedEventHandler HeaderRenamed
        {
            add => AddHandler(HeaderRenamedEvent, value);
            remove => RemoveHandler(HeaderRenamedEvent, value);
        }

        public static readonly DependencyProperty HeaderMemberPathProperty =
            DependencyProperty.Register("HeaderMemberPath", typeof(string), typeof(EditTabControl),
                new PropertyMetadata("Title", OnTemplateParamChanged));
        public string HeaderMemberPath { get => (string)GetValue(HeaderMemberPathProperty); set => SetValue(HeaderMemberPathProperty, value); }

        public static readonly DependencyProperty ShowCloseButtonProperty =
            DependencyProperty.Register("ShowCloseButton", typeof(bool), typeof(EditTabControl),
                new PropertyMetadata(false, OnTemplateParamChanged));
        public bool ShowCloseButton { get => (bool)GetValue(ShowCloseButtonProperty); set => SetValue(ShowCloseButtonProperty, value); }

        public static readonly DependencyProperty CloseTabCommandProperty =
            DependencyProperty.Register("CloseTabCommand", typeof(ICommand), typeof(EditTabControl));
        public ICommand CloseTabCommand { get => (ICommand)GetValue(CloseTabCommandProperty); set => SetValue(CloseTabCommandProperty, value); }

        public static readonly DependencyProperty RenameCommandProperty =
            DependencyProperty.Register("RenameCommand", typeof(ICommand), typeof(EditTabControl));
        public ICommand RenameCommand { get => (ICommand)GetValue(RenameCommandProperty); set => SetValue(RenameCommandProperty, value); }

        public static readonly DependencyProperty HeaderTextProperty =
            DependencyProperty.Register("HeaderText", typeof(string), typeof(EditTabControl),
                new PropertyMetadata(string.Empty));
        public string HeaderText { get => (string)GetValue(HeaderTextProperty); set => SetValue(HeaderTextProperty, value); }

        public static readonly DependencyProperty IsHeaderEditableProperty =
            DependencyProperty.RegisterAttached("IsHeaderEditable", typeof(bool), typeof(EditTabControl),
                new PropertyMetadata(false, OnIsHeaderEditableChanged));
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetIsHeaderEditable。
        /// 正文中的关键协作调用包括 GetValue；具体条件和先后次序见方法体。
        /// </summary>
        public static bool GetIsHeaderEditable(DependencyObject obj) => (bool)obj.GetValue(IsHeaderEditableProperty);
        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetIsHeaderEditable。
        /// 正文中的关键协作调用包括 SetValue；具体条件和先后次序见方法体。
        /// </summary>
        public static void SetIsHeaderEditable(DependencyObject obj, bool value) => obj.SetValue(IsHeaderEditableProperty, value);

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnIsHeaderEditableChanged。
        /// </summary>
        private static void OnIsHeaderEditableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DockPanel dock || !(bool)e.NewValue) return;
            dock.Loaded += OnDockLoaded;
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnDockLoaded。
        /// 正文中的关键协作调用包括 FindVisualAncestor、BeginInvoke、Focus、SelectAll、ExitEdit；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnDockLoaded(object sender, RoutedEventArgs e)
        {
            var dock = (DockPanel)sender;
            dock.Loaded -= OnDockLoaded;
            if (dock.Children[^1] is not Grid grid) return;
            if (grid.Children[0] is not TextBlock textBlock) return;
            if (grid.Children[1] is not TextBox editBox) return;
            var owner = FindVisualAncestor<EditTabControl>(dock);

            dock.PreviewMouseDown += (_, args) =>
            {
                if (args.ClickCount != 2 || editBox.Visibility == Visibility.Visible) return;
                editBox.Tag = editBox.Text;
                textBlock.Visibility = Visibility.Collapsed;
                editBox.Visibility = Visibility.Visible;
                dock.Dispatcher.BeginInvoke(() => { editBox.Focus(); editBox.SelectAll(); },
                    System.Windows.Threading.DispatcherPriority.Input);
            };

            editBox.LostFocus += (_, _) =>
            {
                if (editBox.Visibility != Visibility.Visible) return;
                ExitEdit(dock, textBlock, editBox, true, owner);
            };

            editBox.KeyDown += (_, args) =>
            {
                if (editBox.Visibility != Visibility.Visible) return;
                if (args.Key == Key.Escape) ExitEdit(dock, textBlock, editBox, false, owner);
                else if (args.Key == Key.Enter) ExitEdit(dock, textBlock, editBox, true, owner);
            };
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ExitEdit。
        /// 正文中的关键协作调用包括 RaiseHeaderRenamed；具体条件和先后次序见方法体。
        /// </summary>
        private static void ExitEdit(DockPanel dock, TextBlock tb, TextBox tx, bool accept, EditTabControl? owner)
        {
            if (!accept) { if (tx.Tag is string orig) tx.Text = orig; }
            else if (owner != null)
            {
                var oldText = (tx.Tag as string) ?? tb.Text;
                if (oldText != tx.Text) owner.RaiseHeaderRenamed(dock.DataContext, oldText, tx.Text);
            }
            tx.Visibility = Visibility.Collapsed;
            tb.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 FindVisualAncestor。
        /// 正文中的关键协作调用包括 GetParent；具体条件和先后次序见方法体。
        /// </summary>
        private static T? FindVisualAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null) { if (current is T found) return found; current = VisualTreeHelper.GetParent(current); }
            return null;
        }

        private static readonly Dictionary<string, string> PathToKeySuffix = new()
        {
            ["Title"] = "", ["Model.DisplayTitle"] = "_DisplayTitle", ["Model.TabTitle"] = "_TabTitle",
        };

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnTemplateParamChanged。
        /// 正文中的关键协作调用包括 RefreshExistingContainers；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnTemplateParamChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((EditTabControl)d).RefreshExistingContainers();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 PrepareContainerForItemOverride。
        /// 正文中的关键协作调用包括 PrepareContainerForItemOverride、ReadLocalValue、SetBinding、TryGetValue、TryFindResource；具体条件和先后次序见方法体。
        /// </summary>
        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            if (element is not TabItem tabItem) return;
            if (tabItem.ReadLocalValue(HeaderedContentControl.HeaderProperty) == DependencyProperty.UnsetValue)
                tabItem.SetBinding(HeaderedContentControl.HeaderProperty, new Binding());
            var suffix = PathToKeySuffix.TryGetValue(HeaderMemberPath, out var s) ? s : "";
            var key = ShowCloseButton ? $"EditableHeaderWithClose{suffix}" : $"EditableHeader{suffix}";
            if (TryFindResource(key) is DataTemplate template) tabItem.HeaderTemplate = template;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 RefreshExistingContainers。
        /// 正文中的关键协作调用包括 ContainerFromIndex、PrepareContainerForItemOverride；具体条件和先后次序见方法体。
        /// </summary>
        private void RefreshExistingContainers()
        {
            for (int i = 0; i < Items.Count; i++)
                if (ItemContainerGenerator.ContainerFromIndex(i) is TabItem tabItem)
                    PrepareContainerForItemOverride(tabItem, Items[i]);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 RaiseHeaderRenamed。
        /// 正文中的关键协作调用包括 CanExecute、Execute、RaiseEvent；具体条件和先后次序见方法体。
        /// </summary>
        internal void RaiseHeaderRenamed(object dataItem, string oldText, string newText)
        {
            var info = new HeaderRenameInfo { DataItem = dataItem, OldText = oldText, NewText = newText };
            var cmd = RenameCommand;
            if (cmd != null && cmd.CanExecute(info)) cmd.Execute(info);
            RaiseEvent(new HeaderRenameEventArgs(HeaderRenamedEvent, this, info));
            if (!info.Cancel) HeaderText = newText;
        }
    }
}
