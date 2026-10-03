// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：AiditronProvider、AiditronModelSession。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Diagnostics、System.IO、System.Runtime.InteropServices、Vision.AI.Aiditron.Legacy。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P6 —— Aiditron Provider / Session（补齐旧 AITool 的空 Provider 分支）。
//
// 旧 AITool.Config 里 Aiditron 分支是空的（helper 保持 null，Run 报“未初始化”）。
// P6 补齐：AiditronHelper.InitModel 真实打通 teAddProj/teInitProjs，Session 接管
// 推理→转换→释放。无 SDK 时 InitModel 抛 DllNotFoundException，Provider 包一层
// 上下文（模型路径）后上抛，不伪装状态。
//
// native 任务类型沿用旧 ToolType 顺序：Classification = 0，Segmentation = 1，
// Detection = 2。AnomalyDetection 不声明（三家不强求 1:1）。
// 构造不碰 native（Workbench 切换只构造不加载）。
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Vision.AI.Aiditron.Legacy;
using Vision.Core.AI;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.AI.Aiditron
{
    /// <summary>谛创（Aiditron）AI Provider。</summary>
    public sealed class AiditronProvider : IAIProvider
    {
        /// <inheritdoc/>
        public string Name => "Aiditron";

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
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    return File.Exists(Path.Combine(baseDir, "teTreePredict.dll"))
                        || File.Exists(Path.Combine(baseDir, "teTreePredictd.dll"));
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 处理当前类型提供的操作；对应入口为 OpenSession。
        /// 正文中的关键协作调用包括 ThrowIfNull、ToNativeType、InitModel；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </remarks>
        public IAIModelSession OpenSession(AIModelRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var nativeType = ToNativeType(request.ModelType);

            AiditronHelper helper;
            try
            {
                helper = new AiditronHelper();
                helper.InitModel(request.ModelPath, nativeType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Aiditron 模型加载失败（模型: {request.ModelPath}）：{ex.Message}", ex);
            }

            return new AiditronModelSession(helper, request.ModelPath, request.ModelType);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 执行关闭步骤；对应入口为 CloseSession。
        /// 正文中的关键协作调用包括 Dispose；具体条件和先后次序见方法体。
        /// </remarks>
        public void CloseSession(IAIModelSession? session)
        {
            if (session == null)
                return;
            // AiditronHelper 无显式卸载单个工程的 API（Legacy 行为：helper 随用随建）；
            // 会话释放即 Dispose（helper 本身无 managed 状态可残留，ProjID 随对象消亡）。
            session.Dispose();
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToNativeType。
        /// </summary>
        private static int ToNativeType(AIModelType modelType) => modelType switch
        {
            AIModelType.Classification => 0,
            AIModelType.Segmentation => 1,
            AIModelType.Detection => 2,
            _ => throw new NotSupportedException($"Aiditron 不支持模型类型 {modelType}（仅分类/分割/检测）。"),
        };
    }

    /// <summary>Aiditron 模型会话（拥有一个 AiditronHelper；Legacy 结果转 Core 后释放 XLD）。</summary>
    public sealed class AiditronModelSession : IAIModelSession
    {
        private AiditronHelper? _helper;
        private bool _disposed;

        /// <summary>
        /// 初始化 AiditronModelSession 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
        /// </summary>
        internal AiditronModelSession(AiditronHelper helper, string modelPath, AIModelType modelType)
        {
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
            ModelId = modelPath;
            ModelType = modelType;
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

        /// <inheritdoc/>
        public AICapabilities Capabilities { get; }

        /// <inheritdoc/>
        public bool IsLoaded => !_disposed && _helper != null;

        /// <inheritdoc/>
        /// <remarks>
        /// 运行当前处理流程；对应入口为 Run。
        /// 正文中的关键协作调用包括 ThrowIfNull、Failed、StartNew、ToHObject、Inference；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </remarks>
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
            HalconDotNet.HObject? image = null;
            try
            {
                image = ToHObject(request.Image);
                helper.InferParam = new InferParam
                {
                    Confidence = request.ScoreThreshold > 0 ? request.ScoreThreshold : 0.5,
                };

                var legacy = helper.Inference(image);
                var defects = new List<AiditronDefect>();
                try
                {
                    foreach (var item in legacy)
                    {
                        if (item?.DefectInfo == null || item.ContourInfo?.RectInfo == null)
                            continue;
                        var rect = item.ContourInfo.RectInfo;
                        defects.Add(new AiditronDefect
                        {
                            Label = item.DefectInfo.DefectName ?? string.Empty,
                            Confidence = item.DefectInfo.Confidence,
                            Area = item.DefectInfo.Area,
                            CenterX = rect.CenterX,
                            CenterY = rect.CenterY,
                            Width = rect.Width,
                            Height = rect.Height,
                        });
                    }
                }
                finally
                {
                    // Legacy ContourInfo 的 XLD /A Rectangle 显示对象在这里释放
                    //（旧 InferResultInfo.Dispose 是空实现，P6 在边界处补齐释放语义）。
                    foreach (var item in legacy)
                    {
                        try { item?.ContourInfo?.Xld?.Dispose(); } catch { /* 释放兜底不抛 */ }
                        try { item?.ContourInfo?.Rectangle?.Dispose(); } catch { /* 释放兜底不抛 */ }
                    }
                }

                sw.Stop();
                ToolResult output = ModelType switch
                {
                    AIModelType.Classification => AiditronResultConverter.ToClassification(defects),
                    AIModelType.Segmentation => AiditronResultConverter.ToSegmentation(defects),
                    _ => AiditronResultConverter.ToDetection(defects),
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
                try { image?.Dispose(); } catch { /* 释放兜底不抛 */ }
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 SuppressFinalize；具体条件和先后次序见方法体。
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _helper = null;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToHObject。
        /// 正文中的关键协作调用包括 Alloc、GenImage1、AddrOfPinnedObject、Free；具体条件和先后次序见方法体。
        /// </summary>
        private static HalconDotNet.HObject ToHObject(VisionImage image)
        {
            var handle = GCHandle.Alloc(image.Pixels!, GCHandleType.Pinned);
            try
            {
                HalconDotNet.HOperatorSet.GenImage1(out var hImage, "byte", image.Width, image.Height, handle.AddrOfPinnedObject());
                return hImage;
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Failed。
        /// </summary>
        private static ToolResult Failed(AIModelType modelType, string message, long elapsedMs = 0) => modelType switch
        {
            AIModelType.Classification => new AIClassificationResult { Success = false, Message = message, ElapsedMs = elapsedMs },
            AIModelType.Segmentation => new AISegmentationResult { Success = false, Message = message, ElapsedMs = elapsedMs },
            AIModelType.AnomalyDetection => new AIAnomalyResult { Success = false, Message = message, ElapsedMs = elapsedMs },
            _ => new AIDetectionResult { Success = false, Message = message, ElapsedMs = elapsedMs },
        };
    }
}
