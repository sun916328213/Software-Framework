// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：ToolBrowserViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：OpenCvSharp、Vision.Controls.Models、Vision.Controls.Services、Vision.Controls.ViewModels、Vision.Core.AI、Vision.Core.Imaging。导入不代表所有类型都会在运行时加载。
using OpenCvSharp;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Controls.ViewModels;
using Vision.Core.AI;
using Vision.Core.Imaging;

#nullable enable
namespace Vision.Workbench;

/// <summary>宿主装配具体工具、Provider、图像解码与后端对比。</summary>
public sealed class ToolBrowserViewModel : VisionBrowserViewModel
{
    /// <summary>
    /// 初始化 ToolBrowserViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 CreateWorkspace；具体条件和先后次序见方法体。
    /// </summary>
    public ToolBrowserViewModel() : base(CreateWorkspace(), ownsWorkspace: true) { }
    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateWorkspace。
    /// 正文中的关键协作调用包括 ToList、Select、Create、BuildEntries；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private static VisionBrowserWorkspace CreateWorkspace()
    {
        var providers = AIProviderCatalog.ProviderNames.Select(name =>
        {
            IAIProvider? provider = null;
            try { provider = AIProviderCatalog.Create(name); return new ProviderEntry(name, provider.IsAvailable); }
            catch { return new ProviderEntry(name, false); }
            finally { (provider as IDisposable)?.Dispose(); }
        }).ToList();
        return new VisionBrowserWorkspace(new VisionBrowserCatalog(BuildEntries(), providers, AIProviderCatalog.Create, LoadImageFile, CompareBackends));
    }
    /// <summary>
    /// 执行加载操作；对应入口为 LoadImageFile。
    /// 正文中的关键协作调用包括 ShowDialog、ImRead、Empty、GetArray、FromBuffer；具体条件和先后次序见方法体。
    /// </summary>
    private static VisionImage? LoadImageFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "选择图像", Filter = "图像|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff|全部|*.*" };
        if (dialog.ShowDialog() != true) return null;
        using var mat = Cv2.ImRead(dialog.FileName, ImreadModes.Grayscale);
        if (mat.Empty()) throw new InvalidOperationException("文件不是有效图像。");
        mat.GetArray(out byte[] pixels);
        return VisionImage.FromBuffer(mat.Width, mat.Height, PixelFormat.Mono8, pixels, source: dialog.FileName);
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CompareBackends。
    /// 正文中的关键协作调用包括 Run、Validate、ToMessage；具体条件和先后次序见方法体。
    /// </summary>
    private static string CompareBackends(VisionImage image)
    {
        var cvTool = new Vision.Tools.OpenCv.Inspection.BlobTool();
        var cvResult = cvTool.Run(image);
        var otherTool = new Vision.Tools.Halcon.Inspection.BlobTool();
        otherTool.Parameters.MinGray = cvTool.Parameters.MinGray;
        otherTool.Parameters.MaxGray = cvTool.Parameters.MaxGray;
        var validation = otherTool.Validate();
        return cvResult.Success
            ? $"OpenCV Blob：{cvResult.Items.Count} 个；HALCON 校验：{(validation.IsValid ? "通过" : validation.ToMessage())}（此处对比参数语义与 OpenCV 实测）"
            : $"OpenCV 执行失败：{cvResult.Message}";
    }
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 BuildEntries。
        /// 正文中的关键协作调用包括 Halcon、OpenCv、AI；具体条件和先后次序见方法体。
        /// </summary>
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
