// ─────────────────────────────────────────────────────────────────────────────
// P8 —— 新 Tool 的统一 UI 抽象（计划 §P8「UI 根据 Tool/Result 展示」）。
//
// P3/P4/P5 三套 Tool 基类形状一致（Name / Validate / Run / LastElapsedMs）但无公共
// 接口；UI 若直接依赖具体 Tool 类，每加一类 Tool 就要改一遍展示层。本文件定义
// UI 侧最小契约 IExecutableTool + 三个薄适配器，展示层只认它。
// ⛔ 本目录（ViewModels/、Presentation/、View/）不得出现 HALCON/OpenCV/native 类型，
//    也不得引用 DPLibrary_*（守卫见 Vision.Tests/Architecture/ControlsDependencyTests）。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Core.Imaging;
using Vision.Core.Results;

#nullable enable

namespace Vision.Controls.ViewModels
{
    /// <summary>
    /// 可在 UI 上展示与执行的 Tool（P3/P4/P5 新 Tool 的 UI 侧统一形状）。
    /// </summary>
    public interface IExecutableTool : IDisposable
    {
        /// <summary>工具名称。</summary>
        string Name { get; }

        /// <summary>运行前校验（纯逻辑，无需图像/Runtime）。</summary>
        ValidationResult Validate();

        /// <summary>执行一次；失败转为失败结果，不抛（各 Tool 基类契约）。</summary>
        ToolResult Run(VisionImage? image);

        /// <summary>上次执行耗时（毫秒）。</summary>
        long LastElapsedMs { get; }

        /// <summary>
        /// 确保可执行（AI 类 Tool 加载模型；传统 Tool 无操作）。
        /// 失败抛异常，调用方转状态文本（见 ToolCardViewModel）。
        /// </summary>
        void EnsureLoaded();
    }

    /// <summary>HALCON 传统 Tool 适配器（图像管线走 HSmartWindow，见 Ctr_Halcon；此处只跑校验）。</summary>
    public sealed class HalconToolCardAdapter<TParameters, TResult> : IExecutableTool
        where TParameters : class, new()
        where TResult : ToolResult, new()
    {
        private readonly Vision.Tools.Halcon.Internal.HalconToolBase<TParameters, TResult> _tool;
        private bool _disposed;

        /// <summary>包装一个 HALCON Tool。</summary>
        public HalconToolCardAdapter(Vision.Tools.Halcon.Internal.HalconToolBase<TParameters, TResult> tool)
        {
            _tool = tool ?? throw new ArgumentNullException(nameof(tool));
        }

        /// <inheritdoc/>
        public string Name => _tool.Name;

        /// <inheritdoc/>
        public long LastElapsedMs => _tool.LastElapsedMs;

        /// <inheritdoc/>
        public ValidationResult Validate()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _tool.Validate();
        }

        /// <inheritdoc/>
        public ToolResult Run(VisionImage? image)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            // HALCON 执行需要 HObject 管线（P9 接入 HSmartWindow）；卡片只验证参数与状态。
            return _tool.Run(null);
        }

        /// <inheritdoc/>
        public void EnsureLoaded()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _tool.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>OpenCV Tool 适配器（VisionImage 直驱，可真实执行）。</summary>
    public sealed class OpenCvToolCardAdapter<TParameters, TResult> : IExecutableTool
        where TParameters : class, new()
        where TResult : ToolResult, new()
    {
        private readonly Vision.Tools.OpenCv.Internal.OpenCvToolBase<TParameters, TResult> _tool;
        private bool _disposed;

        /// <summary>包装一个 OpenCV Tool。</summary>
        public OpenCvToolCardAdapter(Vision.Tools.OpenCv.Internal.OpenCvToolBase<TParameters, TResult> tool)
        {
            _tool = tool ?? throw new ArgumentNullException(nameof(tool));
        }

        /// <inheritdoc/>
        public string Name => _tool.Name;

        /// <inheritdoc/>
        public long LastElapsedMs => _tool.LastElapsedMs;

        /// <inheritdoc/>
        public ValidationResult Validate()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _tool.Validate();
        }

        /// <inheritdoc/>
        public ToolResult Run(VisionImage? image)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _tool.Run(image);
        }

        /// <inheritdoc/>
        public void EnsureLoaded()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _tool.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>AI Tool 适配器（Provider 由外部注入；执行前需 EnsureLoaded）。</summary>
    public sealed class AIToolCardAdapter<TParameters, TResult> : IExecutableTool
        where TParameters : Vision.Tools.AI.AIModelParameters, new()
        where TResult : ToolResult, new()
    {
        private readonly Vision.Tools.AI.Internal.AIToolBase<TParameters, TResult> _tool;
        private bool _disposed;

        /// <summary>包装一个 AI Tool。</summary>
        public AIToolCardAdapter(Vision.Tools.AI.Internal.AIToolBase<TParameters, TResult> tool)
        {
            _tool = tool ?? throw new ArgumentNullException(nameof(tool));
        }

        /// <inheritdoc/>
        public string Name => _tool.Name;

        /// <inheritdoc/>
        public long LastElapsedMs => _tool.LastElapsedMs;

        /// <summary>注入的 Provider（透传给 Tool）。</summary>
        public Vision.Core.AI.IAIProvider? Provider
        {
            get => _tool.Provider;
            set => _tool.Provider = value;
        }

        /// <inheritdoc/>
        public ValidationResult Validate()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _tool.Validate();
        }

        /// <inheritdoc/>
        public ToolResult Run(VisionImage? image)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _tool.Run(image);
        }

        /// <inheritdoc/>
        public void EnsureLoaded()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_tool.IsModelLoaded)
                _tool.LoadModel();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _tool.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
