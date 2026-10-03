// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：RegionKind、IVisionRegion。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core、System.Text.Json.Serialization。导入不代表所有类型都会在运行时加载。
using Vision.Core;
using System.Text.Json.Serialization;

// P2 契约骨架（《Vision_Architecture_Optimization_Plan v1.2》§5.1「Vision.Core / Regions」）。
//   「Region」= 分割/阈值/Blob 出来的**区域**（面积、掩膜、行程），
//   与「Roi」= 用户画的**感兴趣区域**（Vision.Core/Roi）刻意分成两个命名空间：
//   前者是算法输出，后者是算法输入，生命周期与语义都不同（计划 §5.1 把它们并列列出）。
//   ⛔ 这里不出现 HObject / Mat：区域数据一律用「种类 + 外接框 + 面积」这类稳定描述表达，
//      真要拿 HALCON region 时由 Vision.Tools.Halcon 在边界处转换。
namespace Vision.Core.Regions
{
    /// <summary>
    /// 区域表示形式。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RegionKind
    {
        /// <summary>未指定。</summary>
        Unknown = 0,

        /// <summary>轴对齐矩形区域。</summary>
        Rectangle,

        /// <summary>椭圆（含圆）区域。</summary>
        Ellipse,

        /// <summary>多边形区域。</summary>
        Polygon,

        /// <summary>位图掩膜（逐像素 0/1）。</summary>
        Mask,

        /// <summary>行程编码（每行若干 [起始列, 长度] 段）。</summary>
        RunLength,
    }

    /// <summary>
    /// 区域契约 —— Blob / 阈值 / 形态学等「产出区域」的工具统一返回它。
    ///
    /// 只读接口：实现方各自决定内部表示（HALCON region / OpenCV 掩膜 / 行程表），
    /// 上层只依赖这里声明的稳定语义。这样 <c>Vision.Tools.OpenCv</c> 与
    /// <c>Vision.Tools.Halcon</c> 才能产出同一种东西（计划 §P4「HALCON/OpenCV 同类 Tool 的结果对比测试」）。
    /// </summary>
    public interface IVisionRegion
    {
        /// <summary>表示形式。</summary>
        RegionKind Kind { get; }

        /// <summary>轴对齐外接框（图像坐标系，像素）。</summary>
        Geometry.Rect2D Bounds { get; }

        /// <summary>面积（像素个数）。</summary>
        double Area { get; }

        /// <summary>是否为空区域。</summary>
        bool IsEmpty { get; }
    }
}
