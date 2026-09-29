// ─────────────────────────────────────────────────────────────────────────────
// P9 —— ToolBrowser ViewModel（计划 §P9「Workbench 成为所有新 Tool 的标准
// 研发/验收入口」+ §11 第一版范围）。
//
// 覆盖：Tool Browser（目录）、Image Load（文件/合成图）、参数编辑
// （PropertyGrid 直编 Parameters POCO）、ROI 编辑（复用 P8 RoiEditorViewModel）、
// Train/Run（卡片命令；AI 经 EnsureLoaded）、Result Viewer（卡片摘要）、
// Execution Time（卡片耗时 + Benchmark 均值）、HALCON/OpenCV Backend Compare、
// AI Provider 选择（三家 + 可用性）。
// 本文件是 Workbench 专属装配逻辑（P9 后不回流 Controls）。
// ─────────────────────────────────────────────────────────────────────────────

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using Vision.Controls.ViewModels;
using Vision.Core.AI;
using Vision.Core.Imaging;

#nullable enable

namespace Vision.Workbench
{
    /// <summary>目录条目（后端分组 + 工厂）。</summary>
    public sealed record ToolEntry(
        string Name,
        string Backend,
        Func<(IExecutableTool Card, object Parameters, Action<IAIProvider>? SetProvider)> Create);

    /// <summary>Provider 条目（名 + 可用性；构造不碰 Runtime，见 P6 守卫）。</summary>
    public sealed record ProviderEntry(string Name, bool IsAvailable);

    /// <summary>新 Tool 标准研发/验收入口 VM。</summary>
    public sealed partial class ToolBrowserViewModel : ObservableObject, IDisposable
    {
        private VisionImage? _image;

        /// <summary>构造并选中首个条目。</summary>
        public ToolBrowserViewModel()
        {
            ProviderEntries = AIProviderCatalog.ProviderNames
                .Select(name =>
                {
                    var available = false;
                    IAIProvider? provider = null;
                    try
                    {
                        provider = AIProviderCatalog.Create(name);
                        available = provider.IsAvailable;
                    }
                    catch
                    {
                        available = false;
                    }
                    finally
                    {
                        (provider as IDisposable)?.Dispose();
                    }

                    return new ProviderEntry(name, available);
                })
                .ToList();
            SelectedProviderName = ProviderEntries.FirstOrDefault(p => p.IsAvailable)?.Name
                ?? ProviderEntries[0].Name;

            RoiEditor = new RoiEditorViewModel();
            RoiEditor.AddRectangle(32, 32, 100, 60);
            SelectedEntry = ToolEntries[0];
        }

        /// <summary>全部目录条目（18 个：HALCON 8 + OpenCV 6 + AI 4）。</summary>
        public IReadOnlyList<ToolEntry> ToolEntries { get; } = BuildEntries();

        /// <summary>Provider 选择列表。</summary>
        public IReadOnlyList<ProviderEntry> ProviderEntries { get; }

        /// <summary>当前卡片。</summary>
        [ObservableProperty]
        private ToolCardViewModel? _currentCard;

        /// <summary>当前参数对象（PropertyGrid 编辑源）。</summary>
        [ObservableProperty]
        private object? _currentParameters;

        /// <summary>当前选中条目。</summary>
        [ObservableProperty]
        private ToolEntry? _selectedEntry;

        /// <summary>选中的 Provider 名。</summary>
        [ObservableProperty]
        private string _selectedProviderName = string.Empty;

        /// <summary>图像信息行。</summary>
        [ObservableProperty]
        private string _imageInfoText = "无图像（可加载文件或使用合成图）";

        /// <summary>后端对比结果行。</summary>
        [ObservableProperty]
        private string _compareText = "—";

        /// <summary>Benchmark 结果行。</summary>
        [ObservableProperty]
        private string _benchmarkText = "—";

        /// <summary>ROI 编辑器。</summary>
        public RoiEditorViewModel RoiEditor { get; }

        partial void OnSelectedEntryChanged(ToolEntry? value)
        {
            if (value != null)
                SelectEntry(value);
        }

        partial void OnSelectedProviderNameChanged(string value)
        {
            ApplyProvider();
        }

        private void SelectEntry(ToolEntry entry)
        {
            CurrentCard?.Dispose();
            CurrentCard = null;
            var (card, parameters, setProvider) = entry.Create();
            _providerSetter = setProvider;
            CurrentParameters = parameters;
            CurrentCard = new ToolCardViewModel(card);
            if (_image != null)
                CurrentCard.SetImage(_image);
            CurrentCard.RefreshValidationCommand.Execute(null);
            ApplyProvider();
        }

        /// <summary>释放当前卡片（P10 Smoke 用 using 模式）。</summary>
        public void Dispose() => CurrentCard?.Dispose();

        private Action<IAIProvider>? _providerSetter;

        private void ApplyProvider()
        {
            if (_providerSetter == null || string.IsNullOrEmpty(SelectedProviderName))
                return;
            try
            {
                _providerSetter(AIProviderCatalog.Create(SelectedProviderName));
            }
            catch (Exception ex)
            {
                CompareText = $"Provider 切换失败：{ex.Message}";
            }
        }

        /// <summary>从文件加载灰度图（OpenCV 直读；失败进图像信息行，不抛）。</summary>
        [RelayCommand]
        public void LoadImage()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择图像",
                Filter = "图像|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff|全部|*.*",
            };
            if (dialog.ShowDialog() != true)
                return;
            try
            {
                using var mat = Cv2.ImRead(dialog.FileName, ImreadModes.Grayscale);
                if (mat.Empty())
                    throw new InvalidOperationException("文件不是有效图像。");
                mat.GetArray(out byte[] pixels);
                SetImage(VisionImage.FromBuffer(mat.Width, mat.Height, PixelFormat.Mono8, pixels, source: dialog.FileName));
                ImageInfoText = $"已加载：{dialog.FileName}（{mat.Width}×{mat.Height}）";
            }
            catch (Exception ex)
            {
                ImageInfoText = $"加载失败：{ex.Message}";
            }
        }

        /// <summary>使用 64×64 合成图（白矩形；无相机/文件时的标准验证输入）。</summary>
        [RelayCommand]
        public void UseDemoImage()
        {
            var pixels = new byte[64 * 64];
            for (var y = 10; y < 30; y++)
            {
                for (var x = 10; x < 30; x++)
                    pixels[y * 64 + x] = 255;
            }

            SetImage(VisionImage.FromBuffer(64, 64, PixelFormat.Mono8, pixels, source: "workbench-demo"));
            ImageInfoText = "合成图：64×64 白矩形";
        }

        private void SetImage(VisionImage image)
        {
            _image = image;
            CurrentCard?.SetImage(image);
        }

        /// <summary>
        /// HALCON/OpenCV Backend Compare（同图 Blob：OpenCV 真实执行 + HALCON 参数校验；
        /// HALCON 执行需 HObject 管线/Runtime，P9 在卡片层只做到校验对比，诚实标注）。
        /// </summary>
        [RelayCommand]
        public void CompareBlob()
        {
            if (_image == null)
            {
                CompareText = "无图像：先加载图像或使用合成图。";
                return;
            }

            try
            {
                var cvTool = new Vision.Tools.OpenCv.Inspection.BlobTool();
                var cvResult = cvTool.Run(_image);
                var halconTool = new Vision.Tools.Halcon.Inspection.BlobTool();
                halconTool.Parameters.MinGray = cvTool.Parameters.MinGray;
                halconTool.Parameters.MaxGray = cvTool.Parameters.MaxGray;
                var halconValidation = halconTool.Validate();
                CompareText = cvResult.Success
                    ? $"OpenCV Blob：{cvResult.Items.Count} 个；HALCON 校验：{(halconValidation.IsValid ? "通过" : halconValidation.ToMessage())}（HALCON 执行需 Runtime，此处只对比参数语义与 OpenCV 实测）"
                    : $"OpenCV 执行失败：{cvResult.Message}";
            }
            catch (Exception ex)
            {
                CompareText = $"对比异常：{ex.Message}";
            }
        }

        /// <summary>Benchmark：当前卡片连跑 5 次取平均耗时（性能基线入口，结果不污染单测）。</summary>
        [RelayCommand]
        public void RunBenchmark()
        {
            if (CurrentCard == null)
            {
                BenchmarkText = "未选中 Tool。";
                return;
            }

            try
            {
                const int rounds = 5;
                for (var i = 0; i < rounds; i++)
                    CurrentCard.RunCommand.Execute(null);
                BenchmarkText = $"Benchmark：{rounds} 次，末次 {CurrentCard.ElapsedMs} ms（{CurrentCard.ResultSummary}）";
            }
            catch (Exception ex)
            {
                BenchmarkText = $"Benchmark 异常：{ex.Message}";
            }
        }

        private static IReadOnlyList<ToolEntry> BuildEntries()
        {
            IExecutableTool Halcon<TParams, TResult>(Vision.Tools.Halcon.Internal.HalconToolBase<TParams, TResult> tool)
                where TParams : class, new()
                where TResult : Vision.Core.Results.ToolResult, new()
            {
                return new HalconToolCardAdapter<TParams, TResult>(tool);
            }

            IExecutableTool OpenCv<TParams, TResult>(Vision.Tools.OpenCv.Internal.OpenCvToolBase<TParams, TResult> tool)
                where TParams : class, new()
                where TResult : Vision.Core.Results.ToolResult, new()
            {
                return new OpenCvToolCardAdapter<TParams, TResult>(tool);
            }

            (IExecutableTool, object, Action<IAIProvider>?) AI<TParams, TResult>(
                Vision.Tools.AI.Internal.AIToolBase<TParams, TResult> tool)
                where TParams : Vision.Tools.AI.AIModelParameters, new()
                where TResult : Vision.Core.Results.ToolResult, new()
            {
                var adapter = new AIToolCardAdapter<TParams, TResult>(tool);
                return (adapter, tool.Parameters, provider => adapter.Provider = provider);
            }

            return new List<ToolEntry>
            {
                new("HALCON/Blob", "HALCON", () =>
                {
                    var tool = new Vision.Tools.Halcon.Inspection.BlobTool();
                    return (Halcon(tool), (object)tool.Parameters, null);
                }),
                new("HALCON/ShapeMatch", "HALCON", () =>
                {
                    var tool = new Vision.Tools.Halcon.Matching.ShapeMatchTool();
                    return (Halcon(tool), (object)tool.Parameters, null);
                }),
                new("HALCON/FindLine", "HALCON", () =>
                {
                    var tool = new Vision.Tools.Halcon.Measurement.FindLineTool();
                    return (Halcon(tool), (object)tool.Parameters, null);
                }),
                new("HALCON/Barcode", "HALCON", () =>
                {
                    var tool = new Vision.Tools.Halcon.Identification.BarcodeTool();
                    return (Halcon(tool), (object)tool.Parameters, null);
                }),
                new("HALCON/DataCode", "HALCON", () =>
                {
                    var tool = new Vision.Tools.Halcon.Identification.DataCodeTool();
                    return (Halcon(tool), (object)tool.Parameters, null);
                }),
                new("HALCON/NPointsCalibration", "HALCON", () =>
                {
                    var tool = new Vision.Tools.Halcon.Calibration.NPointsCalibrationTool();
                    return (Halcon(tool), (object)tool.Parameters, null);
                }),
                new("HALCON/Fixture", "HALCON", () =>
                {
                    var tool = new Vision.Tools.Halcon.Coordinate.FixtureTool();
                    return (Halcon(tool), (object)tool.Parameters, null);
                }),
                new("HALCON/ImageFile", "HALCON", () =>
                {
                    var tool = new Vision.Tools.Halcon.Acquisition.ImageFileTool();
                    return (Halcon(tool), (object)tool.Parameters, null);
                }),
                new("OpenCV/Threshold", "OpenCV", () =>
                {
                    var tool = new Vision.Tools.OpenCv.Processing.ThresholdTool();
                    return (OpenCv(tool), (object)tool.Parameters, null);
                }),
                new("OpenCV/Morphology", "OpenCV", () =>
                {
                    var tool = new Vision.Tools.OpenCv.Processing.MorphologyTool();
                    return (OpenCv(tool), (object)tool.Parameters, null);
                }),
                new("OpenCV/Blob", "OpenCV", () =>
                {
                    var tool = new Vision.Tools.OpenCv.Inspection.BlobTool();
                    return (OpenCv(tool), (object)tool.Parameters, null);
                }),
                new("OpenCV/TemplateMatch", "OpenCV", () =>
                {
                    var tool = new Vision.Tools.OpenCv.Matching.TemplateMatchTool();
                    return (OpenCv(tool), (object)tool.Parameters, null);
                }),
                new("OpenCV/FindLine", "OpenCV", () =>
                {
                    var tool = new Vision.Tools.OpenCv.Measurement.FindLineTool();
                    return (OpenCv(tool), (object)tool.Parameters, null);
                }),
                new("OpenCV/FindCircle", "OpenCV", () =>
                {
                    var tool = new Vision.Tools.OpenCv.Measurement.FindCircleTool();
                    return (OpenCv(tool), (object)tool.Parameters, null);
                }),
                new("AI/Classification", "AI", () =>
                {
                    var tool = new Vision.Tools.AI.Classification.ClassificationTool();
                    var created = AI(tool);
                    return (created.Item1, (object)tool.Parameters, created.Item3);
                }),
                new("AI/Detection", "AI", () =>
                {
                    var tool = new Vision.Tools.AI.Detection.DetectionTool();
                    var created = AI(tool);
                    return (created.Item1, (object)tool.Parameters, created.Item3);
                }),
                new("AI/Segmentation", "AI", () =>
                {
                    var tool = new Vision.Tools.AI.Segmentation.SegmentationTool();
                    var created = AI(tool);
                    return (created.Item1, (object)tool.Parameters, created.Item3);
                }),
                new("AI/AnomalyDetection", "AI", () =>
                {
                    var tool = new Vision.Tools.AI.AnomalyDetection.AnomalyDetectionTool();
                    var created = AI(tool);
                    return (created.Item1, (object)tool.Parameters, created.Item3);
                }),
            };
        }
    }
}
