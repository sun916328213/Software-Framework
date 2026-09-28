using DPLibrary_Halcon_F48.Tools.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;

namespace Vision.Workbench
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            InitP8Controls();
        }

        /// <summary>
        /// P8 新控件复用实证：OpenCV 阈值卡（合成图真实执行）+ ROI 编辑器（种子 ROI）。
        /// 旧 ViewModelT 页签逻辑不动。
        /// </summary>
        private void InitP8Controls()
        {
            var tool = new Vision.Tools.OpenCv.Processing.ThresholdTool();
            tool.Parameters.MinGray = 128;
            var card = new ToolCardViewModel(
                new OpenCvToolCardAdapter<
                    Vision.Tools.OpenCv.Processing.ThresholdParameters,
                    Vision.Tools.OpenCv.Processing.ThresholdResult>(tool));
            card.SetImage(BuildP8DemoImage());
            P8ToolCard.Card = card;
            card.RefreshValidationCommand.Execute(null);
            card.RunCommand.Execute(null);

            var editor = new RoiEditorViewModel();
            editor.AddRectangle(32, 32, 100, 60);
            editor.AddCircle(160, 120, 50);
            P8RoiEditor.Editor = editor;
        }

        /// <summary>64×64 合成图（白矩形，供 P8 卡片真实执行）。</summary>
        private static VisionImage BuildP8DemoImage()
        {
            var pixels = new byte[64 * 64];
            for (var y = 10; y < 30; y++)
            {
                for (var x = 10; x < 30; x++)
                    pixels[y * 64 + x] = 255;
            }

            return VisionImage.FromBuffer(64, 64, Vision.Core.Imaging.PixelFormat.Mono8, pixels, source: "workbench-p8-demo");
        }

      

        private void Button_Click(object sender, RoutedEventArgs e)
        {
        
        }
    }
}
