// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：E_VisionColor、E_ColorEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core、System.Text.Json.Serialization。导入不代表所有类型都会在运行时加载。
using Vision.Core;
using System.Text.Json.Serialization;

namespace Vision.Core.Vision
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
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToVisionStr。
        /// 正文中的关键协作调用包括 ToLower、ToString；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToHalconStr。
        /// 正文中的关键协作调用包括 ToLower、ToString；具体条件和先后次序见方法体。
        /// </summary>
        public static string ToHalconStr(this E_VisionColor color)
        {
            return color.ToString().ToLower();
        }
    }

}
