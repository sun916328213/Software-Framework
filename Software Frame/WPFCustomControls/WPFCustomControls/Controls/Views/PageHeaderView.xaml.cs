// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：PageHeaderView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、MaterialDesignThemes.Wpf、System.Windows、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Controls;

namespace WPFCustomControls.Controls
{
    /// <summary>
    /// 页面页头：图标 + 标题 + 副标题（纯展示，无 VM 耦合）。
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

        /// <summary>
        /// 初始化 PageHeaderView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public PageHeaderView()
        {
            InitializeComponent();
        }
    }
}
