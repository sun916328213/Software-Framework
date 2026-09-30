using DataManager.SQL;
using Prism.Ioc;
using System;
using System.Collections.Generic;
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
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace Packing_Machine.UI.View
{
    /// <summary>
    /// Test.xaml 的交互逻辑
    /// </summary>
    public partial class Test : UserControl
    {
        IContainerProvider containerProvider;
        public Test(IContainerProvider  container)
        {
            InitializeComponent();
            containerProvider = container;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var d1 = containerProvider.Resolve<CapacityStatisticsService>();
            d1.SaveStatistics(DateTime.Now, true);
        }
    }
}
