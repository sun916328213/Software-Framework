using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Controls;

namespace DataManager.Workbench.Views.Shared
{
    /// <summary>
    /// 测试页页头：图标 + 标题 + 副标题（纯展示，无 VM 耦合，DataContext 穿透）。
    /// </summary>
    public partial class PageHeaderView : UserControl
    {
        public static readonly DependencyProperty IconKindProperty =
            DependencyProperty.Register(nameof(IconKind), typeof(PackIconKind), typeof(PageHeaderView),
                new PropertyMetadata(PackIconKind.FlaskOutline));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(PageHeaderView),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SubTitleProperty =
            DependencyProperty.Register(nameof(SubTitle), typeof(string), typeof(PageHeaderView),
                new PropertyMetadata(string.Empty));

        public PackIconKind IconKind
        {
            get => (PackIconKind)GetValue(IconKindProperty);
            set => SetValue(IconKindProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string SubTitle
        {
            get => (string)GetValue(SubTitleProperty);
            set => SetValue(SubTitleProperty, value);
        }

        public PageHeaderView()
        {
            InitializeComponent();
        }
    }
}
