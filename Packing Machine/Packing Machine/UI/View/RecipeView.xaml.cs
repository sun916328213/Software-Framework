using DataManager.SQL;
using Packing_Machine.Business.Flow;
using Packing_Machine.Business.Order;
using Packing_Machine.Business.Station;
using Packing_Machine.Infra.Tool;
using Prism.Ioc;
using System.Windows;
using System.Windows.Controls;

namespace Packing_Machine.UI.View
{
    /// <summary>
    /// Recipe.xaml 的交互逻辑
    /// </summary>
    public partial class Recipe : UserControl
    {
        public Recipe()
        {
            InitializeComponent();
        }
    }
}