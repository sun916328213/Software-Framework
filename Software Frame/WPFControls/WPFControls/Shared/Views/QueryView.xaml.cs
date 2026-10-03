// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：QueryView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、System、System.Collections、System.Collections.Generic、System.ComponentModel、System.Linq。导入不代表所有类型都会在运行时加载。
using WPFControls;
using System;
using System.Collections;
using System.Collections.Generic;
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
using CommunityToolkit.Mvvm.Input;
using WPFControls.Controls.Converters;

namespace WPFControls.Controls.Views
{
    /// <summary>
    /// QueryView.xaml 的交互逻辑
    /// </summary>
    public partial class QueryView : UserControl
    {
        /// <summary>
        /// 第一行 时间查询 依赖属性
        /// </summary>
        public static readonly DependencyProperty StartTimeProperty = DependencyProperty.Register
            ("StartTime", typeof(DateTime?), typeof(QueryView), new PropertyMetadata(null));

        [Bindable(true)]
        [Category("自定义属性"), Description("开始时间"), DisplayName("开始时间")]
        public DateTime StartTime
        {
            get { return (DateTime)GetValue(StartTimeProperty); }
            set { SetValue(StartTimeProperty, value); }
        }


        public static readonly DependencyProperty EndTimeProperty = DependencyProperty.Register
            ("EndTime", typeof(DateTime?), typeof(QueryView), new PropertyMetadata(null));

        [Bindable(true)]
        [Category("自定义属性"), Description("结束时间"), DisplayName("结束时间")]
        public DateTime EndTime
        {
            get { return (DateTime)GetValue(EndTimeProperty); }
            set { SetValue(EndTimeProperty, value); }
        }


        public static readonly DependencyProperty QueryCommandProperty = DependencyProperty.Register
            ("QueryCommand", typeof(ICommand), typeof(QueryView), new PropertyMetadata(null));

        [Bindable(true)]
        [Category("自定义属性"), Description("查询指令"), DisplayName("查询指令")]
        public ICommand QueryCommand
        {
            get { return (ICommand)GetValue(QueryCommandProperty); }
            set { SetValue(QueryCommandProperty, value); }
        }


        public static readonly DependencyProperty SaveCommandProperty = DependencyProperty.Register
            ("SaveCommand", typeof(ICommand), typeof(QueryView), new PropertyMetadata(null));

        [Bindable(true)]
        [Category("自定义属性"), Description("保存指令"), DisplayName("保存指令")]
        public ICommand SaveCommand
        {
            get { return (ICommand)GetValue(SaveCommandProperty); }
            set { SetValue(SaveCommandProperty, value); }
        }


        public static readonly DependencyProperty SaveVisibilityProperty = DependencyProperty.Register
            ("SaveVisibility", typeof(Visibility), typeof(QueryView), new PropertyMetadata(Visibility.Visible));

        [Bindable(true)]
        [Category("自定义属性"), Description("是否显示保存按钮"), DisplayName("是否显示保存按钮")]
        public Visibility SaveVisibility
        {
            get { return (Visibility)GetValue(SaveVisibilityProperty); }
            set { SetValue(SaveVisibilityProperty, value); }
        }


        /// <summary>
        /// 第二行 字段筛查 依赖属性
        /// </summary>
        public static readonly DependencyProperty IsShowFilterProperty = DependencyProperty.Register
            ("IsShowFilter", typeof(bool), typeof(QueryView), new PropertyMetadata(false));

        [Bindable(true)]
        [Category("自定义属性"), Description("是否显示字段筛查"), DisplayName("是否显示字段筛查")]
        public bool IsShowFilter
        {
            get { return (bool)GetValue(IsShowFilterProperty); }
            set { SetValue(IsShowFilterProperty, value); }
        }


        public static readonly DependencyProperty FilterValueProperty = DependencyProperty.Register
            ("FilterValue", typeof(string), typeof(QueryView), new PropertyMetadata(string.Empty));

        [Bindable(true)]
        [Category("自定义属性"), Description("筛查值"), DisplayName("筛查值")]
        public string FilterValue
        {
            get { return (string)GetValue(FilterValueProperty); }
            set { SetValue(FilterValueProperty, value); }
        }


        public static readonly DependencyProperty FilterItemSourceProperty = DependencyProperty.Register
            ("FilterItemSource", typeof(IEnumerable), typeof(QueryView), new PropertyMetadata(null));

        [Bindable(true)]
        [Category("自定义属性"), Description("筛查字段集合"), DisplayName("筛查字段集合")]
        public IEnumerable FilterItemSource
        {
            get { return (IEnumerable)GetValue(FilterItemSourceProperty); }
            set { SetValue(FilterItemSourceProperty, value); }
        }


        public static readonly DependencyProperty FilterItemProperty = DependencyProperty.Register
            ("FilterItem", typeof(string), typeof(QueryView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        [Bindable(true)]
        [Category("自定义属性"), Description("筛查字段"), DisplayName("筛查字段")]
        public string FilterItem
        {
            get { return (string)GetValue(FilterItemProperty); }
            set { SetValue(FilterItemProperty, value); }
        }


        public static readonly DependencyProperty FilterCommandProperty = DependencyProperty.Register
            ("FilterCommand", typeof(ICommand), typeof(QueryView), new PropertyMetadata(null));

        [Bindable(true)]
        [Category("自定义属性"), Description("字段筛查指令"), DisplayName("字段筛查指令")]
        public ICommand FilterCommand
        {
            get { return (ICommand)GetValue(FilterCommandProperty); }
            set { SetValue(FilterCommandProperty, value); }
        }


        public static readonly DependencyProperty FilterResetCommandProperty = DependencyProperty.Register
            ("FilterResetCommand", typeof(ICommand), typeof(QueryView), new PropertyMetadata(null));

        [Bindable(true)]
        [Category("自定义属性"), Description("字段筛查重置指令"), DisplayName("字段筛查重置指令")]
        public ICommand FilterResetCommand
        {
            get { return (ICommand)GetValue(FilterResetCommandProperty); }
            set { SetValue(FilterResetCommandProperty, value); }
        }


        /// <summary>
        /// 第三行 等级筛查 依赖属性
        /// </summary>
        public static readonly DependencyProperty IsShowSelectProperty = DependencyProperty.Register
            ("IsShowSelect", typeof(bool), typeof(QueryView), new PropertyMetadata(false));

        [Bindable(true)]
        [Category("自定义属性"), Description("是否显示等级筛查"), DisplayName("是否显示等级筛查")]
        public bool IsShowSelect
        {
            get { return (bool)GetValue(IsShowSelectProperty); }
            set { SetValue(IsShowSelectProperty, value); }
        }


        public static readonly DependencyProperty SelectItemSourceProperty = DependencyProperty.Register
            ("SelectItemSource", typeof(IEnumerable), typeof(QueryView), new PropertyMetadata(null));

        [Bindable(true)]
        [Category("自定义属性"), Description("等级选择集合"), DisplayName("等级选择集合")]
        public IEnumerable SelectItemSource
        {
            get { return (IEnumerable)GetValue(SelectItemSourceProperty); }
            set { SetValue(SelectItemSourceProperty, value); }
        }


        public static readonly DependencyProperty SelectItemProperty = DependencyProperty.Register
            ("SelectItem", typeof(string), typeof(QueryView), new PropertyMetadata(null));

        [Bindable(true)]
        [Category("自定义属性"), Description("等级选择项"), DisplayName("等级选择项")]
        public string SelectItem
        {
            get { return (string)GetValue(SelectItemProperty); }
            set { SetValue(SelectItemProperty, value); }
        }


        public static readonly DependencyProperty SelectFilterCommandProperty = DependencyProperty.Register
            ("SelectFilterCommand", typeof(ICommand), typeof(QueryView), new PropertyMetadata(null));

        [Bindable(true)]
        [Category("自定义属性"), Description("等级筛查指令"), DisplayName("等级筛查指令")]
        public ICommand SelectFilterCommand
        {
            get { return (ICommand)GetValue(SelectFilterCommandProperty); }
            set { SetValue(SelectFilterCommandProperty, value); }
        }


        public static readonly DependencyProperty SelectResetCommandProperty = DependencyProperty.Register
            ("SelectResetCommand", typeof(ICommand), typeof(QueryView), new PropertyMetadata(null));

        [Bindable(true)]
        [Category("自定义属性"), Description("等级筛查重置指令"), DisplayName("等级筛查重置指令")]
        public ICommand SelectResetCommand
        {
            get { return (ICommand)GetValue(SelectResetCommandProperty); }
            set { SetValue(SelectResetCommandProperty, value); }
        }


        /// <summary>
        /// 布局 依赖属性
        /// </summary>
        private static readonly GridLength[] defaultValue = { new GridLength(250), new GridLength(250), new GridLength(150), new GridLength(150), new GridLength() };
        [TypeConverter(typeof(GridLengthArrayTypeConverter))]
        public static readonly DependencyProperty ColWidthProperty = DependencyProperty.Register
            ("ColWidth", typeof(GridLength[]), typeof(QueryView), new PropertyMetadata(defaultValue));

        [Bindable(true)]
        [Category("自定义属性"), Description("宽度集合"), DisplayName("宽度集合")]
        [TypeConverter(typeof(GridLengthArrayTypeConverter))]
        public GridLength[] ColWidth
        {
            get { return (GridLength[])GetValue(ColWidthProperty); }
            set { SetValue(ColWidthProperty, value); }
        }


        /// <summary>
        /// 初始化 QueryView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public QueryView()
        {
            InitializeComponent();
        }
    }
}
