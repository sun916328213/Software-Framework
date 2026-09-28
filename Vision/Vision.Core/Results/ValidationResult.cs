using System.Text.Json.Serialization;

// P2 契约骨架（《Vision_Architecture_Optimization_Plan v1.2》§5.1「Vision.Core / Results」，
// §7「Validate —— 运行前验证参数、模型、ROI、Provider 能力」）。
//   为什么单独建模而不直接抛异常：视觉工具的参数问题绝大多数是**配置问题**，不是程序缺陷 ——
//   需要一次把「哪些参数不对、各自什么严重度」都收齐给属性面板高亮，
//   而不是撞到第一个就炸。所以 Validate 返回结果对象，不抛。
namespace Vision.Core.Results
{
    /// <summary>校验问题的严重度。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ValidationSeverity
    {
        /// <summary>提示，不影响运行。</summary>
        Info = 0,

        /// <summary>警告，可以继续运行，但结果可能不可信。</summary>
        Warning,

        /// <summary>错误，必须修好才能运行。</summary>
        Error,
    }

    /// <summary>一条校验问题。</summary>
    public sealed class ValidationIssue
    {
        /// <summary>严重度。</summary>
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;

        /// <summary>机器可判的稳定编码（如 <c>ROI_EMPTY</c>、<c>MODEL_NOT_LOADED</c>），供上层分支判断。</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>给人看的说明。</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>出问题的参数 / 属性名（用于在属性面板里定位并高亮）；无归属时留空。</summary>
        public string Member { get; set; } = string.Empty;

        /// <summary>构造一条问题。</summary>
        /// <param name="severity">严重度。</param>
        /// <param name="code">稳定编码。</param>
        /// <param name="message">说明。</param>
        /// <param name="member">参数 / 属性名。</param>
        public ValidationIssue(
            ValidationSeverity severity = ValidationSeverity.Error,
            string code = "",
            string message = "",
            string member = "")
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Member = member ?? string.Empty;
        }

        /// <inheritdoc/>
        public override string ToString() =>
            string.IsNullOrEmpty(Member)
                ? $"[{Severity}] {Code}: {Message}"
                : $"[{Severity}] {Member} ({Code}): {Message}";
    }

    /// <summary>
    /// 校验结果 —— 工具运行前 <c>Validate</c> 的返回值。
    ///
    /// 约定：<see cref="IsValid"/> 只看有没有 <see cref="ValidationSeverity.Error"/>；
    /// 只有 <see cref="ValidationSeverity.Warning"/> 时依然算「可运行」，
    /// 但调用方应把 <see cref="Issues"/> 透出到 UI（计划 §7「Validate」）。
    /// </summary>
    public sealed class ValidationResult
    {
        /// <summary>收集到的问题（按加入顺序）。</summary>
        public System.Collections.Generic.List<ValidationIssue> Issues { get; set; }
            = new System.Collections.Generic.List<ValidationIssue>();

        /// <summary>没有 <see cref="ValidationSeverity.Error"/> 级问题 ⇒ 可运行。</summary>
        [JsonIgnore]
        public bool IsValid
        {
            get
            {
                foreach (var issue in Issues)
                {
                    if (issue != null && issue.Severity == ValidationSeverity.Error)
                        return false;
                }

                return true;
            }
        }

        /// <summary>是否存在警告级问题。</summary>
        [JsonIgnore]
        public bool HasWarnings
        {
            get
            {
                foreach (var issue in Issues)
                {
                    if (issue != null && issue.Severity == ValidationSeverity.Warning)
                        return true;
                }

                return false;
            }
        }

        /// <summary>没有问题的结果（每次返回新实例 —— 共享单例会被调用方就地污染）。</summary>
        /// <returns>空结果。</returns>
        public static ValidationResult Success() => new ValidationResult();

        /// <summary>加一条错误。</summary>
        /// <param name="code">稳定编码。</param>
        /// <param name="message">说明。</param>
        /// <param name="member">参数 / 属性名。</param>
        /// <returns>自身，便于链式调用。</returns>
        public ValidationResult AddError(string code, string message, string member = "") =>
            Add(new ValidationIssue(ValidationSeverity.Error, code, message, member));

        /// <summary>加一条警告。</summary>
        /// <param name="code">稳定编码。</param>
        /// <param name="message">说明。</param>
        /// <param name="member">参数 / 属性名。</param>
        /// <returns>自身，便于链式调用。</returns>
        public ValidationResult AddWarning(string code, string message, string member = "") =>
            Add(new ValidationIssue(ValidationSeverity.Warning, code, message, member));

        /// <summary>加一条问题。</summary>
        /// <param name="issue">问题。</param>
        /// <returns>自身，便于链式调用。</returns>
        public ValidationResult Add(ValidationIssue issue)
        {
            if (issue != null)
                Issues.Add(issue);

            return this;
        }

        /// <summary>把全部问题拼成一行（日志 / 状态栏用）。</summary>
        /// <returns>汇总文本；无问题时返回空串。</returns>
        public string ToMessage()
        {
            if (Issues.Count == 0)
                return string.Empty;

            var parts = new System.Collections.Generic.List<string>(Issues.Count);
            foreach (var issue in Issues)
                parts.Add(issue?.ToString() ?? string.Empty);

            return string.Join(" | ", parts);
        }

        /// <inheritdoc/>
        public override string ToString() =>
            Issues.Count == 0 ? "ValidationResult(ok)" : $"ValidationResult({Issues.Count} issue(s): {ToMessage()})";
    }
}
