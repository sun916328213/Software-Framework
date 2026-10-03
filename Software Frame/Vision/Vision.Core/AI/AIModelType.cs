// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：AIModelType、AICapabilities。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core、System.Text.Json.Serialization。导入不代表所有类型都会在运行时加载。
using Vision.Core;
using System.Text.Json.Serialization;

// P2 契约骨架（《Vision_Architecture_Optimization_Plan v1.2》§5.1「Vision.Core / AI」与 §6「AI 架构」）。
//   §6 原文：「AI Tool 与 AI Runtime 分离。Vision.Tools.AI 只定义『分类/检测/分割/异常检测
//   这些工具怎么工作』；Vision.AI.* 只负责『某家 SDK 如何加载模型、执行推理、释放资源并转换结果』。」
//   所以这两个类型住在 Core（公共语义），三家 Provider 各自实现/声明，Tools.AI 只消费。
namespace Vision.Core.AI
{
    /// <summary>
    /// AI 模型（任务）类型 —— 对应计划 §6 表里的四种 Tool。
    /// ⛔ 取代旧的 <c>AIToolType</c> 大一统枚举：计划 §6 明确「当前 AITool 中 AIBrand + AIToolType
    /// 的大一统对象，后续逐步拆成独立 Tool，避免参数和结果不断膨胀」（P5 执行）。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AIModelType
    {
        /// <summary>未指定。</summary>
        Unknown = 0,

        /// <summary>分类 —— 整图/整 ROI 判一个类别（对应 ClassificationTool / ClassificationResult）。</summary>
        Classification,

        /// <summary>检测 —— 输出若干带类别的框（对应 DetectionTool / DetectionResult + boxes）。</summary>
        Detection,

        /// <summary>分割 —— 输出区域 / 轮廓（对应 SegmentationTool / SegmentationResult + region/contour）。</summary>
        Segmentation,

        /// <summary>异常检测 —— 输出「是否异常 + 异常分数/位置」（对应 AnomalyDetectionTool / AnomalyResult）。</summary>
        AnomalyDetection,
    }

    /// <summary>
    /// Provider / Session 的能力位。
    ///
    /// §6 原文：「三个 Provider 都声明 Capability，不强求完全 1:1」
    /// —— 所以这是一个 <see cref="FlagsAttribute"/> 位集，而不是「一对一映射的枚举」：
    /// 调用方必须先查能力再决定能不能用某个 Tool，而不是假定三家都能做同一件事。
    /// </summary>
    [Flags]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AICapabilities
    {
        /// <summary>无能力。</summary>
        None = 0,

        /// <summary>支持分类。</summary>
        Classification = 1 << 0,

        /// <summary>支持检测。</summary>
        Detection = 1 << 1,

        /// <summary>支持分割。</summary>
        Segmentation = 1 << 2,

        /// <summary>支持异常检测。</summary>
        AnomalyDetection = 1 << 3,

        /// <summary>支持一次提交多张图。</summary>
        BatchInference = 1 << 4,

        /// <summary>可用 GPU 加速。</summary>
        Gpu = 1 << 5,

        /// <summary>支持在线训练 / 微调。</summary>
        Training = 1 << 6,
    }
}
