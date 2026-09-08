using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;

namespace WPFCustomControls.Controls
{
    public class HeaderRenameInfo
    {
        public object? DataItem { get; set; }
        public string OldText { get; set; } = string.Empty;
        public string NewText { get; set; } = string.Empty;
        public bool Cancel { get; set; }
    }

    public class HeaderRenameEventArgs : RoutedEventArgs
    {
        public HeaderRenameEventArgs(RoutedEvent routedEvent, object source, HeaderRenameInfo renameInfo)
            : base(routedEvent, source) => RenameInfo = renameInfo;
        public HeaderRenameInfo RenameInfo { get; }
    }

    public partial class EditTabControl : TabControl
    {
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
        public static bool GetIsHeaderEditable(DependencyObject obj) => (bool)obj.GetValue(IsHeaderEditableProperty);
        public static void SetIsHeaderEditable(DependencyObject obj, bool value) => obj.SetValue(IsHeaderEditableProperty, value);

        private static void OnIsHeaderEditableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DockPanel dock || !(bool)e.NewValue) return;
            dock.Loaded += OnDockLoaded;
        }

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

        private static T? FindVisualAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null) { if (current is T found) return found; current = VisualTreeHelper.GetParent(current); }
            return null;
        }

        private static readonly Dictionary<string, string> PathToKeySuffix = new()
        {
            ["Title"] = "", ["Model.DisplayTitle"] = "_DisplayTitle", ["Model.TabTitle"] = "_TabTitle",
        };

        private static void OnTemplateParamChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((EditTabControl)d).RefreshExistingContainers();

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

        private void RefreshExistingContainers()
        {
            for (int i = 0; i < Items.Count; i++)
                if (ItemContainerGenerator.ContainerFromIndex(i) is TabItem tabItem)
                    PrepareContainerForItemOverride(tabItem, Items[i]);
        }

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
