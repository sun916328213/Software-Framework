using DPLibrary_Halcon_F48.Tools;
using HalconDotNet;
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

namespace Vision.Controls.Ctr_Halcon.View
{
    /// <summary>
    /// Ctr_HDispWindowTool_DP.xaml 的交互逻辑
    /// </summary>
    public partial class Ctr_HDispWindowTool_DP : UserControl
    {
        public Ctr_HDispWindowTool_DP()
        {
            InitializeComponent();
            Viewer.DisplayLayers = DPLibrary_Halcon_F48.Base.DisplayLayers.Image | 
                                   DPLibrary_Halcon_F48.Base.DisplayLayers.Region |
                                   DPLibrary_Halcon_F48.Base.DisplayLayers.XLD |
                                   DPLibrary_Halcon_F48.Base.DisplayLayers.Message;
            Viewer.IsShowMenu = Visibility.Collapsed;
        }

        #region  依赖属性( 读码器实例 - HDisp : Reader)

        /// <summary>
        /// 工具实例附加
        /// </summary>
        public static readonly DependencyProperty HDispProperty =
              DependencyProperty.Register(
                  name: "HDispTool", // 属性名称
                  propertyType: typeof(HVisionTool), // 属性类型
                  ownerType: typeof(Ctr_HDispWindowTool_DP), // 所属控件类型
                  new FrameworkPropertyMetadata(
                      null, // 默认值
                      FrameworkPropertyMetadataOptions.None,
                      OnHDispChanged // 属性变化回调
                  )
              );

        /// <summary>
        /// 工具实例源
        /// </summary>
        public HVisionTool HDispTool
        {
            get => (HVisionTool)GetValue(HDispProperty);
            set => SetValue(HDispProperty, value);
        }

        /// <summary>
        /// 工具实例变化时的处理
        /// </summary>
        private static void OnHDispChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_HDispWindowTool_DP control)
            {
                HVisionTool val = (HVisionTool)e.NewValue;
                if (val == null)
                {
                    return;
                }
            }
        }


        #endregion

        /// <summary>
        /// 获取显示图像
        /// </summary>
        /// <returns></returns>
        public HObject GetDisplayImage()
        {
            using var raw = Viewer.HWindow1?.HalconWindow?.DumpWindowImage();
            if (raw == null)
            {
                return null;
            }
            return raw.Clone();
        }

        /// <summary>
        /// 清除显示
        /// </summary>
        /// <returns></returns>
        public void Clear()
        {
            Viewer.HWindow1?.HalconWindow?.ClearWindow();
        }
    }
}
