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
        public DataGridView()
        {
            InitializeComponent();
            AutoGeneratingColumn += DataGridView_AutoGeneratingColumn;
        }

        private void DataGridView_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            PropertyInfo propertyInfo = null;
            
            var itemsSource = ItemsSource;
            if (itemsSource != null)
            {
                var itemType = itemsSource.GetType().GetGenericArguments()[0];
                propertyInfo = itemType.GetProperty(e.PropertyName);
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
