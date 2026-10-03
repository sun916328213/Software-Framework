// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：BlobSession、BarcodeSession、AutoBarcodeSession、DataCodeSession、CalibrationSession、TransformPointSession、FixtureSession、LineGroupSession、CannyLineSession、GeometrySession、ImageFileSession、ShapeMatchSession。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Text.Json.Serialization、Vision.Tools.Halcon.Acquisition、Vision.Tools.Halcon.Calibration、Vision.Tools.Halcon.Coordinate、Vision.Tools.Halcon.Identification、Vision.Tools.Halcon.Inspection。导入不代表所有类型都会在运行时加载。
using System.Text.Json.Serialization;
using Vision.Tools.Halcon.Acquisition;
using Vision.Tools.Halcon.Calibration;
using Vision.Tools.Halcon.Coordinate;
using Vision.Tools.Halcon.Identification;
using Vision.Tools.Halcon.Inspection;
using Vision.Tools.Halcon.Matching;
using Vision.Tools.Halcon.Measurement;

namespace Vision.Tools.Halcon.Sessions;

/// <summary>
/// BlobSession 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：HalconToolSession&lt;BlobParameters, BlobResult, BlobTool&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class BlobSession : HalconToolSession<BlobParameters, BlobResult, BlobTool>;
/// <summary>
/// BarcodeSession 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：HalconToolSession&lt;BarcodeParameters, BarcodeResult, BarcodeTool&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class BarcodeSession : HalconToolSession<BarcodeParameters, BarcodeResult, BarcodeTool>;
/// <summary>
/// AutoBarcodeSession 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：HalconToolSession&lt;AutoBarcodeParameters, BarcodeResult, AutoBarcodeTool&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class AutoBarcodeSession : HalconToolSession<AutoBarcodeParameters, BarcodeResult, AutoBarcodeTool>;
/// <summary>
/// DataCodeSession 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：HalconToolSession&lt;DataCodeParameters, DataCodeResult, DataCodeTool&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class DataCodeSession : HalconToolSession<DataCodeParameters, DataCodeResult, DataCodeTool>;
/// <summary>
/// CalibrationSession 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：HalconToolSession&lt;NPointsCalibrationParameters, NPointsCalibrationResult, NPointsCalibrationTool&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class CalibrationSession : HalconToolSession<NPointsCalibrationParameters, NPointsCalibrationResult, NPointsCalibrationTool>;
/// <summary>
/// TransformPointSession 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：HalconToolSession&lt;TransformPointParameters, TransformPointResult, TransformPointTool&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class TransformPointSession : HalconToolSession<TransformPointParameters, TransformPointResult, TransformPointTool>;
/// <summary>
/// FixtureSession 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：HalconToolSession&lt;FixtureParameters, FixtureResult, FixtureTool&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class FixtureSession : HalconToolSession<FixtureParameters, FixtureResult, FixtureTool>;
/// <summary>
/// LineGroupSession 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：HalconToolSession&lt;LineGroupParameters, LineGroupResult, LineGroupTool&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class LineGroupSession : HalconToolSession<LineGroupParameters, LineGroupResult, LineGroupTool>;
/// <summary>
/// CannyLineSession 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：HalconToolSession&lt;CannyLineParameters, CannyLineResult, CannyLineTool&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class CannyLineSession : HalconToolSession<CannyLineParameters, CannyLineResult, CannyLineTool>;
/// <summary>
/// GeometrySession 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：HalconToolSession&lt;GeometryParameters, GeometryResult, GeometryTool&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class GeometrySession : HalconToolSession<GeometryParameters, GeometryResult, GeometryTool>;
/// <summary>
/// ImageFileSession 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：HalconToolSession&lt;ImageFileParameters, ImageFileResult, ImageFileTool&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class ImageFileSession : HalconToolSession<ImageFileParameters, ImageFileResult, ImageFileTool>;

/// <summary>
/// ShapeMatchSession 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：HalconToolSession&lt;ShapeMatchParameters, ShapeMatchResult, ShapeMatchTool&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class ShapeMatchSession : HalconToolSession<ShapeMatchParameters, ShapeMatchResult, ShapeMatchTool>
{
    /// <summary>Portable template path. Native models are restored explicitly, never during JSON loading.</summary>
    public string TemplateImagePath { get; set; } = string.Empty;
    [JsonIgnore] public bool IsModelTrained => Engine.IsModelTrained;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 TrainFromFile。
    /// 正文中的关键协作调用包括 ReadImage、Train；具体条件和先后次序见方法体。
    /// </summary>
    public void TrainFromFile()
    {
        HalconDotNet.HOperatorSet.ReadImage(out var template, TemplateImagePath);
        using (template) Engine.Train(template);
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Config。
    /// 正文中的关键协作调用包括 TrainFromFile；具体条件和先后次序见方法体。
    /// </summary>
    public override void Config() => TrainFromFile();
    /// <summary>
    /// 运行当前处理流程；对应入口为 Run。
    /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、TrainFromFile、Run；具体条件和先后次序见方法体。
    /// </summary>
    public override void Run()
    {
        if (!Engine.IsModelTrained && !string.IsNullOrWhiteSpace(TemplateImagePath)) TrainFromFile();
        base.Run();
    }
}
