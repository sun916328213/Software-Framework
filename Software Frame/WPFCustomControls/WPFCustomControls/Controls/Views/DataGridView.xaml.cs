// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：DataGridView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System、System.ComponentModel、System.Reflection、System.Windows、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace WPFCustomControls.Controls
{
    /// <summary>
    /// DataGridView.xaml 的交互逻辑
    /// </summary>
    public partial class DataGridView : DataGrid
    {
        /// <summary>
        /// 初始化 DataGridView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public DataGridView()
        {
            InitializeComponent();
            AutoGeneratingColumn += DataGridView_AutoGeneratingColumn;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 DataGridView_AutoGeneratingColumn。
        /// 正文中的关键协作调用包括 GetGenericArguments、GetType、GetProperty、GetCustomAttribute；具体条件和先后次序见方法体。
        /// </summary>
        private void DataGridView_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            PropertyInfo? propertyInfo = null;
            
            var itemsSource = ItemsSource;
            if (itemsSource != null)
            {
                var genericArguments = itemsSource.GetType().GetGenericArguments();
                if (genericArguments.Length > 0)
                    propertyInfo = genericArguments[0].GetProperty(e.PropertyName);
            }

            if (propertyInfo != null)
            {
                var browsableAttribute = propertyInfo.GetCustomAttribute<BrowsableAttribute>();
                if (browsableAttribute != null && !browsableAttribute.Browsable)
                {
                    e.Cancel = true;
                    return;
                }
                
                var displayAttribute = propertyInfo.GetCustomAttribute<System.ComponentModel.DataAnnotations.DisplayAttribute>();
                if (displayAttribute != null && !displayAttribute.AutoGenerateField)
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        public static new readonly DependencyProperty RowHeightProperty =
         DependencyProperty.Register("RowHeight", typeof(double), typeof(DataGridView), new PropertyMetadata(double.NaN));

        [Bindable(true)]
        [Category("自定义属性"), Description("行高"), DisplayName("行高")]
        public new double RowHeight
        {
            get { return (double)GetValue(RowHeightProperty); }
            set { SetValue(RowHeightProperty, value); }
        }
    }
}
