// ─────────────────────────────────────────────────────────────────────────────
// P6 —— Invi Provider / Session（计划 §6：Provider 拥有模型生命周期与结果转换）。
//
// native 任务类型映射（沿用旧 ToolType / InviType 的顺序）：
//   Classification = 0，Segmentation = 1，Detection = 2。
// AnomalyDetection 不声明（Invi 当前无异常检测模型；三家不强求 1:1）。
//
// 构造不碰 native（Workbench 切换 Provider 只构造不加载，见 P6 验收）。
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Vision.AI.Invi.Native;
using Vision.Core.AI;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.AI.Invi
{
    /// <summary>业视（Invi）AI Provider。</summary>
    public sealed class InviProvider : IAIProvider
    {
        private readonly Func<IInviNativeApi> _nativeFactory;

        /// <summary>
        /// 构造 Provider（不碰 native；缺 DLL 也可构造，加载时才暴露真实错误）。
        /// </summary>
        /// <param name="nativeFactory">native 适配层工厂；null 用真实 DLL 实现。测试注入 fake。</param>
        public InviProvider(Func<IInviNativeApi>? nativeFactory = null)
        {
            _nativeFactory = nativeFactory ?? (() => new DynamicDllInviNativeApi());
        }

        /// <inheritdoc/>
        public string Name => "Invi";

        /// <inheritdoc/>
        public AICapabilities Capabilities =>
            AICapabilities.Classification | AICapabilities.Detection | AICapabilities.Segmentation;

        /// <inheritdoc/>
        public bool IsAvailable
        {
            get
            {
                try
                {
                    return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DynamicDllInviNativeApi.DllName));
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <inheritdoc/>
        public IAIModelSession OpenSession(AIModelRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var nativeType = ToNativeType(request.ModelType);

            InviHelper helper;
            try
            {
                helper = new InviHelper(_nativeFactory());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Invi 会话打开失败（manager 创建阶段）：{ex.Message}", ex);
            }

            try
            {
                helper.InitModel(request.ModelPath, nativeType);
            }
            catch (Exception ex)
            {
                helper.Dispose();
                throw new InvalidOperationException($"Invi 模型加载失败（模型: {request.ModelPath}）：{ex.Message}", ex);
            }

            return new InviModelSession(helper, request.ModelPath, request.ModelType, nativeType);
        }

        /// <inheritdoc/>
        public void CloseSession(IAIModelSession? session)
        {
            if (session == null)
                return;
            if (session is InviModelSession invi)
                invi.Release();
            else
                session.Dispose();
        }

        private static int ToNativeType(AIModelType modelType) => modelType switch
        {
            AIModelType.Classification => 0,
            AIModelType.Segmentation => 1,
            AIModelType.Detection => 2,
            _ => throw new NotSupportedException($"Invi 不支持模型类型 {modelType}（仅分类/分割/检测）。"),
        };
    }

    /// <summary>Invi 模型会话（拥有一个 InviHelper；VisionImage 直驱 native 推理）。</summary>
    public sealed class InviModelSession : IAIModelSession
    {
        private InviHelper? _helper;
        private bool _disposed;

        internal InviModelSession(InviHelper helper, string modelPath, AIModelType modelType, int nativeType)
        {
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
            ModelId = modelPath;
            ModelType = modelType;
            NativeType = nativeType;
            Capabilities = modelType switch
            {
                AIModelType.Classification => AICapabilities.Classification,
                AIModelType.Segmentation => AICapabilities.Segmentation,
                AIModelType.Detection => AICapabilities.Detection,
                _ => AICapabilities.None,
            };
        }

        /// <inheritdoc/>
        public string ModelId { get; }

        /// <inheritdoc/>
        public AIModelType ModelType { get; }

        /// <summary>native 任务类型（0 分类 / 1 分割 / 2 检测）。</summary>
        public int NativeType { get; }

        /// <inheritdoc/>
        public AICapabilities Capabilities { get; }

        /// <inheritdoc/>
        public bool IsLoaded => !_disposed && _helper != null;

        /// <inheritdoc/>
        public ToolResult Run(AIInferenceRequest request)
        {
            var helper = _helper;
            if (_disposed || helper == null)
                throw new InvalidOperationException("会话已关闭。");
            ArgumentNullException.ThrowIfNull(request);
            if (request.Image == null || request.Image.IsEmpty)
                return Failed(ModelType, "输入图像为空。");
            if (request.Image.Format != PixelFormat.Mono8)
                return Failed(ModelType, $"P6 仅支持 Mono8 输入，实际 {request.Image.Format}。");

            var sw = Stopwatch.StartNew();
            var pixels = request.Image.Pixels!;
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                var results = helper.InferResults(ModelId, NativeType,
                    request.Image.Width, request.Image.Height, channels: 1,
                    handle.AddrOfPinnedObject(), request.ScoreThreshold);
                sw.Stop();

                ToolResult output = ModelType switch
                {
                    AIModelType.Classification => InviResultConverter.ToClassification(results),
                    AIModelType.Segmentation => InviResultConverter.ToSegmentation(results),
                    _ => InviResultConverter.ToDetection(results),
                };
                output.Success = true;
                output.ElapsedMs = sw.ElapsedMilliseconds;
                return output;
            }
            catch (Exception ex)
            {
                sw.Stop();
                return Failed(ModelType, ex.Message, sw.ElapsedMilliseconds);
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>释放模型并销毁 helper（失败上抛且保留已加载态；无会话时无操作）。</summary>
        internal void Release()
        {
            var helper = _helper;
            if (_disposed || helper == null)
                return;
            helper.ReleaseModel(ModelId);
            helper.Dispose();
            _helper = null;
            _disposed = true;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            try
            {
                var helper = _helper;
                _helper = null;
                helper?.Dispose();
            }
            catch
            {
                // Dispose 兜底不抛。
            }

            GC.SuppressFinalize(this);
        }

        private static ToolResult Failed(AIModelType modelType, string message, long elapsedMs = 0) => modelType switch
        {
            AIModelType.Classification => new AIClassificationResult { Success = false, Message = message, ElapsedMs = elapsedMs },
            AIModelType.Segmentation => new AISegmentationResult { Success = false, Message = message, ElapsedMs = elapsedMs },
            AIModelType.AnomalyDetection => new AIAnomalyResult { Success = false, Message = message, ElapsedMs = elapsedMs },
            _ => new AIDetectionResult { Success = false, Message = message, ElapsedMs = elapsedMs },
        };
    }
}
