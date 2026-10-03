// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：Ctr_ROI。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Controls、Vision.Core.Vision、System、System.Collections.Generic、System.Collections.Specialized、System.ComponentModel。导入不代表所有类型都会在运行时加载。
using Vision.Controls;
using Vision.Core.Vision;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Vision.Controls.Ctr_Halcon.View
{
    /// <summary>
    /// Ctr_ConfigurableDataGrid.xaml 的交互逻辑
    /// </summary>
    public partial class Ctr_ROI : DataGrid
    {
        /// <summary>
        /// 初始化 Ctr_ROI 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent、ConfigureColumns；具体条件和先后次序见方法体。
        /// </summary>
        public Ctr_ROI()
        {
            InitializeComponent();
            // 初始化默认列配置
            ConfigureColumns();
        }
 
        #region 依赖属性 (NameBindingPath)
        
        public static readonly DependencyProperty NameBindingPathProperty =
            DependencyProperty.Register(
                nameof(NameBindingPath),
                typeof(string),
                typeof(Ctr_ROI),
                new PropertyMetadata("Name", OnNameBindingPathChanged));

        /// <summary>
        /// 名称列绑定路径
        /// </summary>
        public string NameBindingPath
        {
            get { return (string)GetValue(NameBindingPathProperty); }
            set { SetValue(NameBindingPathProperty, value); }
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnNameBindingPathChanged。
        /// 正文中的关键协作调用包括 ConfigureColumns；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnNameBindingPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_ROI control)
            {
                control.ConfigureColumns();
            }
        }

        #endregion

        #region 依赖属性 (TypeBindingPath)
        
        public static readonly DependencyProperty TypeBindingPathProperty =
            DependencyProperty.Register(
                nameof(TypeBindingPath),
                typeof(string),
                typeof(Ctr_ROI),
                new PropertyMetadata("Type", OnTypeBindingPathChanged));

        /// <summary>
        /// 类型列绑定路径
        /// </summary>
        public string TypeBindingPath
        {
            get { return (string)GetValue(TypeBindingPathProperty); }
            set { SetValue(TypeBindingPathProperty, value); }
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnTypeBindingPathChanged。
        /// 正文中的关键协作调用包括 ConfigureColumns；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnTypeBindingPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_ROI control)
            {
                control.ConfigureColumns();
            }
        }

        #endregion

        #region 依赖属性 (NameColumnHeader)
        
        public static readonly DependencyProperty NameColumnHeaderProperty =
            DependencyProperty.Register(
                nameof(NameColumnHeader),
                typeof(string),
                typeof(Ctr_ROI),
                new PropertyMetadata("名称", OnNameColumnHeaderChanged));

        /// <summary>
        /// 名称列标题
        /// </summary>
        public string NameColumnHeader
        {
            get { return (string)GetValue(NameColumnHeaderProperty); }
            set { SetValue(NameColumnHeaderProperty, value); }
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnNameColumnHeaderChanged。
        /// 正文中的关键协作调用包括 ConfigureColumns；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnNameColumnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_ROI control)
            {
                control.ConfigureColumns();
            }
        }

        #endregion

        #region 依赖属性 (TypeColumnHeader)
        
        public static readonly DependencyProperty TypeColumnHeaderProperty =
            DependencyProperty.Register(
                nameof(TypeColumnHeader),
                typeof(string),
                typeof(Ctr_ROI),
                new PropertyMetadata("类型", OnTypeColumnHeaderChanged));

        /// <summary>
        /// 类型列标题
        /// </summary>
        public string TypeColumnHeader
        {
            get { return (string)GetValue(TypeColumnHeaderProperty); }
            set { SetValue(TypeColumnHeaderProperty, value); }
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnTypeColumnHeaderChanged。
        /// 正文中的关键协作调用包括 ConfigureColumns；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnTypeColumnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_ROI control)
            {
                control.ConfigureColumns();
            }
        }

        #endregion

        #region 依赖属性 (ShowBorderInRowDetails)
        
        public static readonly DependencyProperty ShowBorderInRowDetailsProperty =
            DependencyProperty.Register(
                nameof(ShowBorderInRowDetails),
                typeof(bool),
                typeof(Ctr_ROI),
                new PropertyMetadata(true));

        public bool ShowBorderInRowDetails
        {
            get { return (bool)GetValue(ShowBorderInRowDetailsProperty); }
            set { SetValue(ShowBorderInRowDetailsProperty, value); }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 配置DataGrid列
        /// </summary>
        private void ConfigureColumns()
        {
            // 清除现有列
            this.Columns.Clear();

            // 添加名称列
            var nameColumn = new DataGridTextColumn
            {
                Header = NameColumnHeader,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                IsReadOnly = false
            };
            
            // 设置绑定
            if (!string.IsNullOrEmpty(NameBindingPath))
            {
                nameColumn.Binding = new Binding(NameBindingPath);
            }
            
            this.Columns.Add(nameColumn);

            // 添加类型列
            var typeColumn = new DataGridTextColumn
            {
                Header = TypeColumnHeader,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                IsReadOnly = true
            };
            
            // 设置绑定
            if (!string.IsNullOrEmpty(TypeBindingPath))
            {
                typeColumn.Binding = new Binding(TypeBindingPath);
            }
            
            this.Columns.Add(typeColumn);
            this.Columns.Add(CreateActionColumn());
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateActionColumn。
        /// 正文中的关键协作调用包括 SetValue、AppendChild、CreateUseButtonFactory、CreateActionButtonFactory；具体条件和先后次序见方法体。
        /// </summary>
        private DataGridTemplateColumn CreateActionColumn()
        {
            var column = new DataGridTemplateColumn
            {
                Header = "操作",
                Width = new DataGridLength(168),
                IsReadOnly = true
            };

            var panelFactory = new FrameworkElementFactory(typeof(StackPanel));
            panelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            panelFactory.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            panelFactory.AppendChild(CreateUseButtonFactory());
            panelFactory.AppendChild(CreateActionButtonFactory("配置", OnConfigureRoiClicked));
            panelFactory.AppendChild(CreateActionButtonFactory("删除", OnDeleteRoiClicked));

            column.CellTemplate = new DataTemplate { VisualTree = panelFactory };
            return column;
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateUseButtonFactory。
        /// 正文中的关键协作调用包括 SetBinding、SetValue、Add；具体条件和先后次序见方法体。
        /// </summary>
        private FrameworkElementFactory CreateUseButtonFactory()
        {
            var buttonFactory = new FrameworkElementFactory(typeof(ToggleButton));
            buttonFactory.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsActive")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            buttonFactory.SetValue(ToggleButton.MarginProperty, new Thickness(2, 0, 2, 0));
            buttonFactory.SetValue(ToggleButton.PaddingProperty, new Thickness(8, 2, 8, 2));
            buttonFactory.SetValue(ToggleButton.MinWidthProperty, 48.0);
            buttonFactory.SetValue(ToggleButton.HeightProperty, 24.0);

            var style = new Style(typeof(ToggleButton));
            style.Setters.Add(new Setter(ContentControl.ContentProperty, "使用"));
            style.Setters.Add(new Setter(Control.OpacityProperty, 0.62));

            var trigger = new Trigger
            {
                Property = ToggleButton.IsCheckedProperty,
                Value = true
            };
            trigger.Setters.Add(new Setter(ContentControl.ContentProperty, "使用中"));
            trigger.Setters.Add(new Setter(Control.OpacityProperty, 1.0));
            trigger.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            style.Triggers.Add(trigger);

            buttonFactory.SetValue(ToggleButton.StyleProperty, style);
            return buttonFactory;
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateActionButtonFactory。
        /// 正文中的关键协作调用包括 SetValue、AddHandler；具体条件和先后次序见方法体。
        /// </summary>
        private FrameworkElementFactory CreateActionButtonFactory(string text, RoutedEventHandler clickHandler)
        {
            var buttonFactory = new FrameworkElementFactory(typeof(Button));
            buttonFactory.SetValue(Button.ContentProperty, text);
            buttonFactory.SetValue(Button.MarginProperty, new Thickness(2, 0, 2, 0));
            buttonFactory.SetValue(Button.PaddingProperty, new Thickness(8, 2, 8, 2));
            buttonFactory.SetValue(Button.MinWidthProperty, 42.0);
            buttonFactory.SetValue(Button.HeightProperty, 24.0);
            buttonFactory.AddHandler(Button.ClickEvent, clickHandler);
            return buttonFactory;
        }

        #endregion

        #region 事件处理

        private void dg_Common_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var row = ItemsControl.ContainerFromElement(this, e.OriginalSource as DependencyObject) as DataGridRow;
            if (row != null)
            {
                // 如果点击的行已被选中，则取消选中
                if (row.IsSelected)
                {
                    // 同步 IsHit 属性 
                    if (row.DataContext is I_IsHit ob)
                        ob.IsHit = false;

                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnConfigureRoiClicked。
        /// 正文中的关键协作调用包括 ContainerFromItem；具体条件和先后次序见方法体。
        /// </summary>
        private void OnConfigureRoiClicked(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement)?.DataContext;
            if (item == null)
                return;

            SelectedItem = item;
            if (ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row)
            {
                row.IsSelected = true;
                row.DetailsVisibility = Visibility.Visible;
            }

            e.Handled = true;
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnDeleteRoiClicked。
        /// 正文中的关键协作调用包括 Remove；具体条件和先后次序见方法体。
        /// </summary>
        private void OnDeleteRoiClicked(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement)?.DataContext;
            if (item == null)
                return;

            if (ItemsSource is System.Collections.IList list && !list.IsReadOnly)
                list.Remove(item);

            e.Handled = true;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 获取选中的项
        /// </summary>
        public new IList<I_IsHit> SelectedItems
        {
            get
            {
                return base.SelectedItems.Cast<I_IsHit>().ToList();
            }
        }

        /// <summary>
        /// 取消所有选中项
        /// </summary>
        public void ClearSelection()
        {
            base.SelectedItems.Clear();
        }

        #endregion
    }
}
