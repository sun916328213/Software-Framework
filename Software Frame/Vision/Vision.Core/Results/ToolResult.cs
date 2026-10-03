// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：ToolResult。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core、System.Text.Json.Serialization、Vision.Core.Tools。导入不代表所有类型都会在运行时加载。
using Vision.Core;
using System.Text.Json.Serialization;
using Vision.Core.Tools;

// P2 契约骨架（《Vision_Architecture_Optimization_Plan v1.2》§5.1「Vision.Core / Results」，
// §7「Run —— 统一执行入口；明确成功/失败/耗时」「Result —— 返回稳定结果对象」）。
//
// ⛔ 与既有 IToolResult（Vision.Core/Tools/IToolResult.cs）的关系（务必先读再改）：
//    IToolResult 是**旧**契约：`bool IsFailed` + `IDisposable`，只有两个成员，
//    `Tool.Results` 就装它（BulkObservableCollection<IToolResult>）。
//    ToolResult 是**目标**契约：成功/说明/耗时/校验结果。
//    P2 的做法是**桥接而不是替换** —— ToolResult 实现 IToolResult，
//    于是它今天就能直接放进 Tool.Results、被既有 UI 显示，
//    而不用在 P2 阶段去改 Tool / HTool_* 的签名（那是 P3「每个 Tool 建立
//    Parameters / Result / Validate / Run」的活）。
namespace Vision.Core.Results
{
    /// <summary>
    /// 工具结果基类 —— 所有 <c>XxxResult</c>（BlobResult / MatchResult / DetectionResult…）的公共头。
    ///
    /// 设计取向：
    /// <list type="bullet">
    ///   <item>派生类只加**该工具特有**的字段，公共的成败/耗时/校验一律从这里继承；</item>
    ///   <item><see cref="Validation"/> 记录**运行前**的校验结论，便于事后复盘「这次跑的是不是带病参数」；</item>
    ///   <item>实现 <see cref="IToolResult"/> 是为了与既有 <c>Tool.Results</c> 兼容（见文件头注释）。</item>
    /// </list>
    /// ⛔ 不要在这里放 HALCON / OpenCV 类型（<c>HTuple</c>/<c>HObject</c>/<c>Mat</c>）——
    /// 计划 §5.1 明确要求「避免直接返回 HTuple/HObject/Mat」。厂商类型只允许出现在
    /// Vision.Tools.* / Vision.AI.* 的内部实现里，出口处必须转成本命名空间下的稳定类型。
    /// </summary>
    public class ToolResult : IToolResult
    {
        /// <summary>是否成功。</summary>
        public bool Success { get; set; } = true;

        /// <summary>失败原因 / 补充说明；成功时通常为空。</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>本次执行耗时（毫秒）。</summary>
        public long ElapsedMs { get; set; }

        /// <summary>运行前的校验结论；未做校验时为 null。</summary>
        public ValidationResult Validation { get; set; }

        /// <summary>
        /// 旧契约的失败标记，与 <see cref="Success"/> 互为反面。
        /// 标 <c>[JsonIgnore]</c>：它是派生量，存两份会在 JSON 里出现自相矛盾的可能。
        /// </summary>
        [JsonIgnore]
        public bool IsFailed
        {
            get => !Success;
            set => Success = !value;
        }

        /// <summary>
        /// 释放结果持有的原生资源。基类没有原生资源，故为空实现；
        /// 派生类若持有（如 HALCON 的 <c>HObject</c> / 模型句柄）必须重写。
        /// </summary>
        public virtual void Dispose()
        {
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// 正文中的关键协作调用包括 GetType、IsNullOrEmpty；具体条件和先后次序见方法体。
        /// </remarks>
        public override string ToString() =>
            $"{GetType().Name}({(Success ? "ok" : "failed")}, {ElapsedMs}ms" +
            (string.IsNullOrEmpty(Message) ? ")" : $", {Message})");
    }
}
