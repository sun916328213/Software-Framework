// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：ToolCardWorkspace。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Controls.Models、Vision.Controls.ViewModels、Vision.Controls.Presentation、Vision.Core.Imaging。导入不代表所有类型都会在运行时加载。
using Vision.Controls.Models;
using Vision.Controls.ViewModels;
using Vision.Controls.Presentation;
using Vision.Core.Imaging;

#nullable enable

namespace Vision.Controls.Services
{
    /// <summary>
    /// 单个 Tool 的展示卡片：校验状态 + 执行 + 结果摘要。
    /// </summary>
    public sealed class ToolCardWorkspace : IDisposable
    {
        private readonly IExecutableTool _tool;
        private VisionImage? _image;

        /// <summary>包装一个可执行 Tool。</summary>
        public ToolCardWorkspace(IExecutableTool tool)
        {
            _tool = tool ?? throw new ArgumentNullException(nameof(tool));
            Model.ToolName = tool.Name;
            Model.StatusText = "未校验";
            Model.ResultSummary = "—";
        }

        public ToolCardModel Model { get; } = new();
        private bool _disposed;

        /// <summary>设置本次执行的输入图像（可为 null 走纯校验路径）。</summary>
        public void SetImage(VisionImage? image)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _image = image;
        }

        /// <summary>刷新校验并更新状态文本。</summary>
        public void RefreshValidation()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var validation = _tool.Validate();
            Model.IsValid = validation.IsValid;
            Model.StatusText = validation.IsValid ? "校验通过" : $"校验失败：{validation.ToMessage()}";
        }

        /// <summary>确保可执行（AI 加载模型；失败进状态文本，不抛）。</summary>
        public void EnsureLoaded()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            try
            {
                _tool.EnsureLoaded();
                Model.StatusText = "就绪";
            }
            catch (Exception ex)
            {
                Model.IsValid = false;
                Model.StatusText = $"加载失败：{ex.Message}";
            }
        }

        /// <summary>执行一次并更新结果摘要（失败转摘要，不抛）。</summary>
        public void Run()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            try
            {
                var result = _tool.Run(_image);
                Model.ElapsedMs = _tool.LastElapsedMs;
                Model.ResultSummary = ToolResultText.Summarize(result);
                Model.StatusText = result.Success ? "执行成功" : $"执行失败：{result.Message}";
            }
            catch (Exception ex)
            {
                Model.ResultSummary = "—";
                Model.StatusText = $"执行异常：{ex.Message}";
            }
        }

        /// <summary>释放底层 Tool（P9 切换条目时调用，避免会话/native 泄漏）。</summary>
        public void Dispose() { if (_disposed) return; _disposed = true; _image = null; _tool.Dispose(); }
    }
}
