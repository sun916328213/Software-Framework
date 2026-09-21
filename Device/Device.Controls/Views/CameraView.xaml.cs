using System.Windows.Controls;

namespace Device.Controls.Views
{
    /// <summary>
    /// 相机测试页视图：纯 XAML（档案表列、回调、预览绑定全在 CameraView.xaml 声明），无 code-behind 逻辑。
    /// </summary>
    public partial class CameraView : UserControl
    {
        public CameraView()
        {
            InitializeComponent();
        }
    }
}
