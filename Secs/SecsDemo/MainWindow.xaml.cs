using Secs4Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SecsDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            List<string > data=new List<string>() {"SS", "AAA","111","BBB","222"};
            List<Item> list1 = new List<Item>();

          
            for (int index = 1; index < data.Count; index = index + 2)
            {
                Item[] item = new Item[2];
                item[0] = Item.A(data[index + 0]);
                item[1] = Item.A(data[index + 1]);
              
                list1.Add(Item.L(item));
            }
 
            Item SecsItem1 = Item.L
          (
              Item.B(0),
                Item.L(list1.ToArray())
          );
            InitializeComponent();
        }
    }
}