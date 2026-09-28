// ─────────────────────────────────────────────────────────────────────────────
// P8 —— Tool 卡片 VM（计划 §P8「UI 根据 Tool/Result 展示」）。
//
// 只依赖 IExecutableTool + ToolResultText：任意 P3/P4/P5 Tool 配上适配器即可上卡，
// 新增 Tool 类型时展示层零修改。命令用 CommunityToolkit（与 ViewModelT 一致），
// 本身不触任何 WPF 可视化类型，可在普通单元测试里全覆盖。
// ─────────────────────────────────────────────────────────────────────────────

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vision.Controls.Presentation;
using Vision.Core.Imaging;

#nullable enable

namespace Vision.Controls.ViewModels
{
    /// <summary>
    /// 单个 Tool 的展示卡片：校验状态 + 执行 + 结果摘要。
    /// </summary>
    public sealed partial class ToolCardViewModel : ObservableObject
    {
        private readonly IExecutableTool _tool;
        private VisionImage? _image;

        /// <summary>包装一个可执行 Tool。</summary>
        public ToolCardViewModel(IExecutableTool tool)
        {
            _tool = tool ?? throw new ArgumentNullException(nameof(tool));
            ToolName = tool.Name;
            StatusText = "未校验";
            ResultSummary = "—";
        }

        /// <summary>工具名称。</summary>
        public string ToolName { get; }

        /// <summary>校验/执行状态文本（绑定展示）。</summary>
        [ObservableProperty]
        private string _statusText = string.Empty;

        /// <summary>最近一次校验是否通过。</summary>
        [ObservableProperty]
        private bool _isValid;

        /// <summary>最近一次执行耗时（毫秒）。</summary>
        [ObservableProperty]
        private long _elapsedMs;

        /// <summary>最近一次结果摘要（见 ToolResultText）。</summary>
        [ObservableProperty]
        private string _resultSummary = string.Empty;

        /// <summary>设置本次执行的输入图像（可为 null 走纯校验路径）。</summary>
        public void SetImage(VisionImage? image) => _image = image;

        /// <summary>刷新校验并更新状态文本。</summary>
        [RelayCommand]
        public void RefreshValidation()
        {
            var validation = _tool.Validate();
            IsValid = validation.IsValid;
            StatusText = validation.IsValid ? "校验通过" : $"校验失败：{validation.ToMessage()}";
        }

        /// <summary>确保可执行（AI 加载模型；失败进状态文本，不抛）。</summary>
        [RelayCommand]
        public void EnsureLoaded()
        {
            try
            {
                _tool.EnsureLoaded();
                StatusText = "就绪";
            }
            catch (Exception ex)
            {
                IsValid = false;
                StatusText = $"加载失败：{ex.Message}";
            }
        }

        /// <summary>执行一次并更新结果摘要（失败转摘要，不抛）。</summary>
        [RelayCommand]
        public void Run()
        {
            try
            {
                var result = _tool.Run(_image);
                ElapsedMs = _tool.LastElapsedMs;
                ResultSummary = ToolResultText.Summarize(result);
                StatusText = result.Success ? "执行成功" : $"执行失败：{result.Message}";
            }
            catch (Exception ex)
            {
                ResultSummary = "—";
                StatusText = $"执行异常：{ex.Message}";
            }
        }
    }
}
