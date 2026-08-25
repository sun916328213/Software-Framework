using System;
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

namespace WPFCustomControls.Controls
{
    /// <summary>
    /// CircleProcessBar.xaml 的交互逻辑
    /// </summary>
    public partial class CircleProcessBar : UserControl
    {

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register
           ("Value", typeof(double), typeof(CircleProcessBar), new PropertyMetadata(0.0, null));

       
        [Bindable(true)]
        [Category("自定义属性"), Description("数据值"), DisplayName("数据值")]
        public double Value 
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

    

        public CircleProcessBar()
        {
            InitializeComponent();
        }
    }
}
