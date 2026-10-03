// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：AIModelRequest、AIInferenceRequest。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core。导入不代表所有类型都会在运行时加载。
using Vision.Core;
// P2 契约骨架（《Vision_Architecture_Optimization_Plan v1.2》§5.1「Vision.Core / AI」）。
//   §6 原文：「当前 IAI 的 Inference(HObject) 需要改造，统一入口不再绑定 HALCON 图像类型。」
//   ⇒ 请求里的图像一律是 Vision.Core.Imaging.VisionImage，ROI 一律是 Vision.Core.Roi.IRoi。
//   ⛔ 本文件（以及整个 Vision.Core）不允许出现 HObject / Mat / HTuple。
namespace Vision.Core.AI
{
    /// <summary>
    /// 打开一个模型会话的请求。
    /// </summary>
    public sealed class AIModelRequest
    {
        /// <summary>模型文件 / 模型目录路径（各 Provider 自行解释）。</summary>
        public string ModelPath { get; set; } = string.Empty;

        /// <summary>模型任务类型。</summary>
        public AIModelType ModelType { get; set; } = AIModelType.Unknown;

        /// <summary>设备标识（如 "cpu"、"gpu0"）；空表示由 Provider 决定。</summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>Provider 专有选项（键值由各 Provider 约定，Core 不解释）。</summary>
        public System.Collections.Generic.Dictionary<string, string> Options { get; set; }
            = new System.Collections.Generic.Dictionary<string, string>();

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// 正文中的关键协作调用包括 IsNullOrEmpty；具体条件和先后次序见方法体。
        /// </remarks>
        public override string ToString() =>
            $"AIModelRequest({ModelType}, {ModelPath}, device={(string.IsNullOrEmpty(DeviceId) ? "auto" : DeviceId)})";
    }

    /// <summary>
    /// 一次推理请求。
    /// </summary>
    public sealed class AIInferenceRequest
    {
        /// <summary>输入图像。</summary>
        public Imaging.VisionImage Image { get; set; }

        /// <summary>感兴趣区域；为 null 表示整图。</summary>
        public Roi.IRoi Roi { get; set; }

        /// <summary>置信度阈值（0~1）；&lt;= 0 表示用 Provider 默认值。</summary>
        public double ScoreThreshold { get; set; }

        /// <summary>最多返回多少个结果；&lt;= 0 表示不限制。</summary>
        public int MaxResults { get; set; }

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// 正文中的关键协作调用包括 ToString；具体条件和先后次序见方法体。
        /// </remarks>
        public override string ToString() =>
            $"AIInferenceRequest({Image}, roi={(Roi == null ? "full" : Roi.Kind.ToString())}, " +
            $"th={ScoreThreshold:0.###}, max={MaxResults})";
    }
}
