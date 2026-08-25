using System.Text.Json.Serialization;

namespace DPLibrary_Base_F48.Vision
{
    /// <summary>
    /// 用于自定义的视觉用颜色
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum E_VisionColor
    {
        Red,
        Green,
        Blue,
        Yellow,
        Black,
        White
    }

    /// <summary>
    /// 用于操作视觉用颜色的扩展方法
    /// </summary>
    public static class E_ColorEx
    {
        public static string ToVisionStr(this E_VisionColor color, E_VisionType type)
        {
            switch (type)
            {
                case E_VisionType.Halcon:
                    return color.ToString().ToLower();
                case E_VisionType.VisionPro:
                case E_VisionType.OpenCV:
                default:
                    return color.ToString();
            }
        }

        public static string ToHalconStr(this E_VisionColor color)
        {
            return color.ToString().ToLower();
        }
    }

}
