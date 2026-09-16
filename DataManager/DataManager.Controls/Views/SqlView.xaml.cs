using System.Windows.Controls;

namespace DataManager.Controls.Views
{
    /// <summary>
    /// SQL 测试页视图：纯 XAML（列定义、增删回调、选中同步全在 SqlView.xaml 声明），无 code-behind 逻辑。
    /// </summary>
    public partial class SqlView : UserControl
    {
        public SqlView()
        {
            InitializeComponent();
        }
    }
}
