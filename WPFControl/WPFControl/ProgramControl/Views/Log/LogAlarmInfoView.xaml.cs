using System.Windows;
using System.Windows.Controls;

namespace WPFControl.ProgramControl.Views
{
    /// <summary>
    /// RunInfoView.xaml 的交互逻辑
    /// 滚动和批量化刷新由 DataGridLogView 内部定时器处理
    /// </summary>
    public partial class LogAlarmInfoView : UserControl
    {
        public LogAlarmInfoView()
        {
            InitializeComponent();
        }
    }
}