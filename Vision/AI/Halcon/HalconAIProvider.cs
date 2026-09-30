// ─────────────────────────────────────────────────────────────────────────────
// P6 —— HALCON AI Provider / Session（计划 §P6「实现 HALCON AI Model/Session、
// 推理和结果转换」；§3「Vision.Tools.Halcon 与 Vision.AI.Halcon 不互相依赖」——
// 本工程自带图像转换，不引用 Tools.Halcon）。
//
// 推理走 DL 字典链：模型输入几何（GetDlModelParam）→ 通道、尺寸、灰度范围适配 →
// 字典样本 → ApplyDlModel → 按任务取字典键转 Core 结果。
// 注：23.11 绑定无 DL 预处理算子，手动样本制备只做整图通道/尺寸适配，
// domain 感知预处理随后续 HALCON 版本补齐。
// 分割 P6-v1 只给外接框（多边形/掩膜随 P7）；异常给分数 + 可用时的异常区域框。
// 构造不碰 Runtime（Workbench 切换只构造不加载）。
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using HalconDotNet;
using Vision.Core.AI;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.AI.Halcon
{
    /// <summary>HALCON AI Provider（深度学习模型）。</summary>
    public sealed class HalconAIProvider : IAIProvider
    {
        private static readonly Lazy<bool> CachedAvailability = new(ProbeRuntime);

        /// <inheritdoc/>
        public string Name => "Halcon";

        /// <inheritdoc/>
        public AICapabilities Capabilities =>
            AICapabilities.Classification | AICapabilities.Detection |
            AICapabilities.Segmentation | AICapabilities.AnomalyDetection;

        /// <inheritdoc/>
        public bool IsAvailable => CachedAvailability.Value;

        /// <inheritdoc/>
        public IAIModelSession OpenSession(AIModelRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.ModelType == AIModelType.Unknown)
                throw new NotSupportedException("模型任务类型未知（AIModelType.Unknown），无法打开 HALCON AI 会话。");
            if (string.IsNullOrWhiteSpace(request.ModelPath) || !File.Exists(request.ModelPath))
                throw new InvalidOperationException($"HALCON 模型文件不存在：{request.ModelPath}");

            HTuple? model = null;
            try
            {
                HOperatorSet.ReadDlModel(request.ModelPath, out model);
                if (string.Equals(request.DeviceId, "cpu", StringComparison.OrdinalIgnoreCase))
                    HOperatorSet.SetDlModelParam(model, "runtime", "cpu");
            }
            catch (Exception ex)
            {
                try { model?.Dispose(); } catch { /* 清理兜底 */ }
                throw new InvalidOperationException($"HALCON 模型加载失败（模型: {request.ModelPath}）：{ex.Message}", ex);
            }

            return new HalconAIModelSession(model, request.ModelPath, request.ModelType);
        }

        /// <inheritdoc/>
        public void CloseSession(IAIModelSession? session)
        {
            if (session == null)
                return;
            if (session is HalconAIModelSession halcon)
                halcon.Release();
            else
                session.Dispose();
        }

        private static bool ProbeRuntime()
        {
            try
            {
                HOperatorSet.GetSystem("version", out var version);
                try { version.Dispose(); } catch { /* 探测清理 */ }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>HALCON AI 模型会话（拥有 DL 模型句柄）。</summary>
    public sealed class HalconAIModelSession : IAIModelSession
    {
        private HTuple? _model;
        private bool _disposed;

        internal HalconAIModelSession(HTuple model, string modelPath, AIModelType modelType)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            ModelId = modelPath;
            ModelType = modelType;
            Capabilities = modelType switch
            {
                AIModelType.Classification => AICapabilities.Classification,
                AIModelType.Detection => AICapabilities.Detection,
                AIModelType.Segmentation => AICapabilities.Segmentation,
                AIModelType.AnomalyDetection => AICapabilities.AnomalyDetection,
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
        public bool IsLoaded => !_disposed && _model != null;

        /// <inheritdoc/>
        public ToolResult Run(AIInferenceRequest request)
        {
            var model = _model;
            if (_disposed || model == null)
                throw new InvalidOperationException("会话已关闭。");
            ArgumentNullException.ThrowIfNull(request);
            if (request.Image == null || request.Image.IsEmpty)
                return Failed("输入图像为空。");
            if (request.Image.Format != PixelFormat.Mono8)
                return Failed($"P6 仅支持 Mono8 输入，实际 {request.Image.Format}。");

            var sw = Stopwatch.StartNew();
            HObject? image = null;
            HObject? adapted = null;
            HObject? zoomed = null;
            HTuple? sample = null;
            HTuple? result = null;
            try
            {
                image = ToHObject(request.Image);
                zoomed = BuildModelInput(model, image, out adapted);
                HOperatorSet.CreateDict(out sample);
                HOperatorSet.SetDictObject(zoomed, sample, "image");
                HOperatorSet.ApplyDlModel(model, sample, new HTuple(), out result);
                sw.Stop();

                var output = ModelType switch
                {
                    AIModelType.Classification => (ToolResult)ReadClassification(model, result),
                    AIModelType.Detection => ReadDetection(model, result, request.Image.Width, request.Image.Height),
                    AIModelType.Segmentation => ReadSegmentation(result),
                    _ => ReadAnomaly(result, request.ScoreThreshold),
                };
                output.Success = true;
                output.ElapsedMs = sw.ElapsedMilliseconds;
                return output;
            }
            catch (Exception ex)
            {
                sw.Stop();
                return Failed(ex.Message, sw.ElapsedMilliseconds);
            }
            finally
            {
                try { image?.Dispose(); } catch { /* 释放兜底 */ }
                try { adapted?.Dispose(); } catch { /* 释放兜底 */ }
                try { zoomed?.Dispose(); } catch { /* 释放兜底 */ }
                try { sample?.Dispose(); } catch { /* 释放兜底 */ }
                try { result?.Dispose(); } catch { /* 释放兜底 */ }
            }
        }

        /// <summary>
        /// 手动样本制备（23.11 绑定无 DL 预处理算子）：按模型输入要求做通道、
        /// 尺寸和灰度范围适配。整图推理（domain 处理随后续版本补齐）。
        /// </summary>
        /// <param name="model">DL 模型句柄（调用方持有）。</param>
        /// <param name="image">输入图（调用方持有）。</param>
        /// <param name="adapted">通道适配中间图（调用方负责释放；无需适配时为 null）。</param>
        /// <returns>缩放到模型输入尺寸的新图像（调用方负责释放）。</returns>
        private static HObject BuildModelInput(HTuple model, HObject image, out HObject? adapted)
        {
            adapted = null;
            HTuple? width = null;
            HTuple? height = null;
            HTuple? channels = null;
            HTuple? rangeMin = null;
            HTuple? rangeMax = null;
            HTuple? imageChannels = null;
            HObject? zoomed = null;
            HObject? real = null;
            try
            {
                HOperatorSet.GetDlModelParam(model, "image_width", out width);
                HOperatorSet.GetDlModelParam(model, "image_height", out height);
                HOperatorSet.GetDlModelParam(model, "image_num_channels", out channels);
                HOperatorSet.GetDlModelParam(model, "image_range_min", out rangeMin);
                HOperatorSet.GetDlModelParam(model, "image_range_max", out rangeMax);
                HOperatorSet.CountChannels(image, out imageChannels);

                HObject source = image;
                if (channels.I == 1 && imageChannels.I > 1)
                {
                    HOperatorSet.Rgb1ToGray(image, out adapted);
                    source = adapted;
                }
                else if (channels.I == 3 && imageChannels.I == 1)
                {
                    HOperatorSet.Compose3(image, image, image, out adapted);
                    source = adapted;
                }

                HOperatorSet.ZoomImageSize(source, out zoomed, width, height, "constant");
                HOperatorSet.ConvertImageType(zoomed, out real, "real");
                HOperatorSet.ScaleImage(real, out var scaled,
                    (rangeMax.D - rangeMin.D) / 255.0, rangeMin.D);
                return scaled;
            }
            finally
            {
                real?.Dispose();
                zoomed?.Dispose();
                imageChannels?.Dispose();
                rangeMax?.Dispose();
                rangeMin?.Dispose();
                channels?.Dispose();
                height?.Dispose();
                width?.Dispose();
            }
        }

        /// <summary>释放模型（ClearDlModel 失败上抛且保留已加载态；无会话时无操作）。</summary>
        internal void Release()
        {
            var model = _model;
            if (_disposed || model == null)
                return;
            try
            {
                HOperatorSet.ClearDlModel(model);
            }
            catch
            {
                throw;
            }

            _model = null;
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
                var model = _model;
                _model = null;
                model?.Dispose();
            }
            catch
            {
                // Dispose 兜底不抛。
            }

            GC.SuppressFinalize(this);
        }

        private AIClassificationResult ReadClassification(HTuple model, HTuple result)
        {
            HOperatorSet.GetDictTuple(result, "classification_class_ids", out var ids);
            HOperatorSet.GetDictTuple(result, "classification_confidences", out var confs);
            try
            {
                string[]? names = TryGetClassNames(model);
                return HalconResultMapper.MapClassification(ids.LArr, confs.DArr, names);
            }
            finally
            {
                ids.Dispose();
                confs.Dispose();
            }
        }

        private AIDetectionResult ReadDetection(HTuple model, HTuple result, int inputWidth, int inputHeight)
        {
            HTuple? r1 = null;
            HTuple? c1 = null;
            HTuple? r2 = null;
            HTuple? c2 = null;
            HTuple? ids = null;
            HTuple? confs = null;
            HTuple? modelWidth = null;
            HTuple? modelHeight = null;
            try
            {
                HOperatorSet.GetDictTuple(result, "bbox_row1", out r1);
                HOperatorSet.GetDictTuple(result, "bbox_col1", out c1);
                HOperatorSet.GetDictTuple(result, "bbox_row2", out r2);
                HOperatorSet.GetDictTuple(result, "bbox_col2", out c2);
                HOperatorSet.GetDictTuple(result, "bbox_class_id", out ids);
                HOperatorSet.GetDictTuple(result, "bbox_confidence", out confs);
                HOperatorSet.GetDlModelParam(model, "image_width", out modelWidth);
                HOperatorSet.GetDlModelParam(model, "image_height", out modelHeight);
                string[]? names = TryGetClassNames(model);
                var output = HalconResultMapper.MapDetection(r1.DArr, c1.DArr, r2.DArr, c2.DArr,
                    ids.LArr, confs.DArr, names);
                var scaleX = (double)inputWidth / modelWidth.I;
                var scaleY = (double)inputHeight / modelHeight.I;
                foreach (var box in output.Boxes)
                    box.Box = new Rect2D(box.Box.X * scaleX, box.Box.Y * scaleY,
                        box.Box.Width * scaleX, box.Box.Height * scaleY);
                return output;
            }
            finally
            {
                r1?.Dispose(); c1?.Dispose(); r2?.Dispose(); c2?.Dispose(); ids?.Dispose(); confs?.Dispose();
                modelWidth?.Dispose(); modelHeight?.Dispose();
            }
        }

        private AISegmentationResult ReadSegmentation(HTuple result)
        {
            // P6-v1：分割图→连通域→外接框（多边形/掩膜随 P7）。
            HOperatorSet.GetDictObject(out var segImage, result, "segmentation_image");
            try
            {
                HOperatorSet.CountChannels(segImage, out var channels);
                HObject gray = segImage;
                HObject? converted = null;
                try
                {
                    if (channels.I > 1)
                    {
                        HOperatorSet.Rgb1ToGray(segImage, out converted);
                        gray = converted;
                    }

                    HOperatorSet.Threshold(gray, out var region, 1, 255);
                    try
                    {
                        HOperatorSet.Connection(region, out var connected);
                        try
                        {
                            var items = new List<(Rect2D Bounds, long ClassId, double Score)>();
                            var count = connected.CountObj();
                            for (var i = 1; i <= count; i++)
                            {
                                using var obj = connected.SelectObj(i);
                                HOperatorSet.SmallestRectangle1(obj, out var rr1, out var cc1, out var rr2, out var cc2);
                                try
                                {
                                    items.Add((new Rect2D(cc1.D, rr1.D, cc2.D - cc1.D + 1, rr2.D - rr1.D + 1), 0, 1.0));
                                }
                                finally
                                {
                                    rr1.Dispose(); cc1.Dispose(); rr2.Dispose(); cc2.Dispose();
                                }
                            }

                            return HalconResultMapper.MapSegmentation(items);
                        }
                        finally
                        {
                            connected.Dispose();
                        }
                    }
                    finally
                    {
                        region.Dispose();
                    }
                }
                finally
                {
                    channels.Dispose();
                    converted?.Dispose();
                }
            }
            finally
            {
                segImage.Dispose();
            }
        }

        private AIAnomalyResult ReadAnomaly(HTuple result, double threshold)
        {
            var cutoff = threshold > 0 ? threshold : 0.5;
            double score = 0;
            try
            {
                HOperatorSet.GetDictTuple(result, "anomaly_scores", out var scores);
                try
                {
                    foreach (var value in scores.DArr)
                    {
                        if (value > score)
                            score = value;
                    }
                }
                finally
                {
                    scores.Dispose();
                }
            }
            catch
            {
                // 无异常分数键：视为正常（score 保持 0）。
            }

            var bounds = Rect2D.Empty;
            try
            {
                HOperatorSet.GetDictObject(out var region, result, "anomaly_region");
                try
                {
                    HOperatorSet.SmallestRectangle1(region, out var r1, out var c1, out var r2, out var c2);
                    try
                    {
                        bounds = new Rect2D(c1.D, r1.D, c2.D - c1.D + 1, r2.D - r1.D + 1);
                    }
                    finally
                    {
                        r1.Dispose(); c1.Dispose(); r2.Dispose(); c2.Dispose();
                    }
                }
                finally
                {
                    region.Dispose();
                }
            }
            catch
            {
                // 无异常区域键：bounds 保持 Empty。
            }

            return HalconResultMapper.MapAnomaly(score, score >= cutoff, bounds);
        }

        private static string[]? TryGetClassNames(HTuple model)
        {
            try
            {
                HOperatorSet.GetDlModelParam(model, "class_names", out var names);
                try
                {
                    return names.SArr;
                }
                finally
                {
                    names.Dispose();
                }
            }
            catch
            {
                return null;
            }
        }

        private static HObject ToHObject(VisionImage image)
        {
            var handle = GCHandle.Alloc(image.Pixels!, GCHandleType.Pinned);
            try
            {
                HOperatorSet.GenImage1(out var hImage, "byte", image.Width, image.Height, handle.AddrOfPinnedObject());
                return hImage;
            }
            finally
            {
                handle.Free();
            }
        }

        private ToolResult Failed(string message, long elapsedMs = 0) => ModelType switch
        {
            AIModelType.Classification => new AIClassificationResult { Success = false, Message = message, ElapsedMs = elapsedMs },
            AIModelType.Segmentation => new AISegmentationResult { Success = false, Message = message, ElapsedMs = elapsedMs },
            AIModelType.AnomalyDetection => new AIAnomalyResult { Success = false, Message = message, ElapsedMs = elapsedMs },
            _ => new AIDetectionResult { Success = false, Message = message, ElapsedMs = elapsedMs },
        };
    }
}
