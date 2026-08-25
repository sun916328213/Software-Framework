using Device.RFID;
using Device.RFID.Definition;
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

namespace DeviceDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        RFIDManager _RFIDManager = new RFIDManager();

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            RFIDParam rFIDParam = new RFIDParam()
            { 
              PortName="COM5",
              RFIDType= ERFIDType.椹诺XK15AR01,
            };
            _RFIDManager.Create(rFIDParam);
            _RFIDManager.Connect();
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            _RFIDManager.Disconnect();
        }

        private void Read_Click(object sender, RoutedEventArgs e)
        {
            _RFIDManager[0].Read(1);
        }

        private void Write_Click(object sender, RoutedEventArgs e)
        {
            _RFIDManager[0].Write(1,"1234");
        }

        private void ReadMulit_Click(object sender, RoutedEventArgs e)
        {
            _RFIDManager[0].Read(1, 2);
        }

        private void WriteMulit_Click(object sender, RoutedEventArgs e)
        {
            List<string> data = new List<string>();
            data.Add("1111");
            data.Add("2222");
            _RFIDManager[0].Write(1, data);
        }
    }
}
