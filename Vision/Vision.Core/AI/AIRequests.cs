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
        public override string ToString() =>
            $"AIInferenceRequest({Image}, roi={(Roi == null ? "full" : Roi.Kind.ToString())}, " +
            $"th={ScoreThreshold:0.###}, max={MaxResults})";
    }
}
