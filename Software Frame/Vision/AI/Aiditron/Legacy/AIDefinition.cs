// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：InferParam、InferResultInfo、InferResult、RectInfo、ContourInfo、DefectInfo。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.AI.Aiditron、HalconDotNet、System、Vision.Core.Tools。导入不代表所有类型都会在运行时加载。
using Vision.AI.Aiditron;
// ─────────────────────────────────────────────────────────────────────────────
// P6 —— Legacy 桥接类型（来源：DPLibrary_Halcon_F48/Tools/AI/Definition/AIParam.cs）。
// 用途：AiditronHelper 的 Inference 返回链（InferResultInfo / ContourInfo / DefectInfo）
// 沿用迁移源形状，Session 在边界处转成 Core 公共结果后即释放其中的 HObject。
// ⛔ P6-v1 桥接：不新增、不扩展；显示侧 XLD 随 P9 退场（Controls 接管可复用显示）。
// ─────────────────────────────────────────────────────────────────────────────

using HalconDotNet;
using System;
using Vision.Core.Tools;

namespace Vision.AI.Aiditron.Legacy
{
    /// <summary>预测信息参数（沿用迁移源字段）。</summary>
    internal class InferParam
    {
        public double Long_Side;
        public double Short_Side;
        public double MinArea;
        public double Confidence = 0.5;
    }

    /// <summary>推理结果（Tool.Results 兼容；ContourInfo 持有 HALCON 显示对象）。</summary>
    internal class InferResultInfo : IToolResult
    {
        public ContourInfo ContourInfo;
        public DefectInfo DefectInfo;

        public string Name => DefectInfo.DefectName;
        public double Area => DefectInfo.Area;
        public double Confidence => DefectInfo.Confidence;

        public bool IsFailed { get; set; }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// </summary>
        public override string ToString() => $"结果:{DefectInfo.DefectName} ";

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// </summary>
        public void Dispose()
        {
        }
    }

    /// <summary>推理中间结果（只用于程序内部，不对外输出）。</summary>
    internal class InferResult
    {
        public double area;
        public double confidence;
        public string class_name;
        public HTuple Cols;
        public HTuple Rows;

        /// <summary>
        /// 初始化 InferResult 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public InferResult() { }

        /// <summary>
        /// 初始化 InferResult 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public InferResult(double area, double confidence, string class_name, HTuple cols, HTuple rows)
        {
            this.area = area;
            this.confidence = confidence;
            this.class_name = class_name;
            Cols = cols;
            Rows = rows;
        }
    }

    /// <summary>缺陷外接矩形信息。</summary>
    internal class RectInfo
    {
        public double Width;
        public double Height;
        public double LongSide;
        public double ShortSide;
        public double CenterX;
        public double CenterY;
        public double OriginX;
        public double OriginY;
        public double Area;
    }

    /// <summary>缺陷轮廓与外接矩形（HALCON 显示对象；Session 转换后负责释放）。</summary>
    internal class ContourInfo
    {
        public HObject Xld;
        public HObject Rectangle;
        public RectInfo RectInfo { get; set; }

        /// <summary>
        /// 初始化 ContourInfo 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ContourInfo() { }

        /// <summary>
        /// 初始化 ContourInfo 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ContourInfo(HObject xld, HObject rect)
        {
            Xld = xld; Rectangle = rect;
        }
    }

    /// <summary>缺陷信息（Session 转 Core 结果的唯一数据源）。</summary>
    internal class DefectInfo
    {
        public string DefectName { get; set; } = string.Empty;
        public double Area { get; set; }
        public double Confidence { get; set; }
    }
}
