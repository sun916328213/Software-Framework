using Vision.Core.Tools;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows;

namespace DPLibrary_Halcon_F48.Tools.Blob
{
    [JsonConverter(typeof(JsonStringEnumConverter))]

    public enum E_ColorSpaceType
    {

        hsv,
        hsi,
        yuv
    }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum E_channelsType
    {

        Channel1,
        Channel2,
        Channel3
    }
    public enum E_FeaturesType
    {

        area,
        row,
        column,
        width,
        height,
        Phi,
        circularity,
        rectangularity,
        outer_radius,
        inner_radius

    }
    public enum E_FeaturesDispType
    {

        area,
        row,
        column,
        circularity,
        rectangularity,
        outer_radius,
        inner_radius

    }
    public enum E_OpearationType
    {
        and,
        or
    }

    public class HBlob_Result : IToolResult
    {

        /// <summary>
        /// Blob数量
        /// </summary>
        public int Count { get; set; }
        /// <summary>
        /// Blob面积
        /// </summary>
        public double Area { get; set; }
        /// <summary>
        /// 中心坐标
        /// </summary>
        public Point Center { get; set; }

        [Browsable(false)]
        public HROI ResultRegion { get; set; }
        public bool IsFailed { get; set; }

        public void Dispose()
        {
            ResultRegion.Dispose();
        }

    }
  
    }

