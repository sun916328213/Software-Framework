using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;


namespace DPLibrary_Halcon_F48.Base
{
    public class DisplayModelToLayersConverter : IValueConverter
    {
        // Model → Layers
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DisplayLayers layers)
            {
                // 这里可以根据优先级来决定属于哪个 DisplayModel
                if (layers.HasFlag(DisplayLayers.Region))
                {
                    return DisplayModel.Result;
                }
                else if (layers.HasFlag(DisplayLayers.ROI))
                {
                    return DisplayModel.Image_ROI;
                }
                else if (layers.HasFlag(DisplayLayers.Image))
                {
                    return DisplayModel.Image;
                }

                return DisplayModel.None;
            }
            return DisplayModel.None;
        }

        // Layers → Model
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DisplayModel model)
            {
                switch (model)
                {
                    case DisplayModel.None:
                        return DisplayLayers.None;
                    case DisplayModel.Image:
                        return DisplayLayers.Image;
                    case DisplayModel.Image_ROI:
                        // ⚠️ MeasurePath 不能丢：Ctr_HWindow_DP.ResetDisp 用
                        // HasFlag(MeasurePath) 决定 DispMeasure()/DeDispMeasure()，
                        // 少了这一位，切到本档后卡尺（MeasurePaths）在图上直接消失。
                        // 与 DisplayModel.Image_ROI 的枚举值（1<<1|1<<0|1<<5）保持一致。
                        return DisplayLayers.Image | DisplayLayers.ROI | DisplayLayers.MeasurePath;
                    case DisplayModel.Result:
                        return DisplayLayers.Image | DisplayLayers.Region | DisplayLayers.XLD | DisplayLayers.Message | DisplayLayers.MeasurePath;
                    default:
                        break;
                }
            }
            return DisplayLayers.None;
        }
    }
}
