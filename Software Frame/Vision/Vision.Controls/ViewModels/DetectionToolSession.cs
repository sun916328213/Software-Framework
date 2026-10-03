// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：DetectionToolSession。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Text.Json.Serialization、System.ComponentModel、Vision.Core.AI、Vision.Core.Imaging、Vision.Core.Results、Vision.Core.Tools。导入不代表所有类型都会在运行时加载。
#nullable enable
using System.Text.Json.Serialization;
using System.ComponentModel;
using Vision.Core.AI;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Tools;
using Vision.Tools.AI.Detection;

namespace Vision.Controls.ViewModels;

/// <summary>Persisted detection parameters; the host supplies a provider before loading a model.</summary>
/// <remarks>
/// DetectionToolSession 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：Tool、IEditableTool、IDisposable。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class DetectionToolSession : Tool, IEditableTool, IDisposable
{
    /// <summary>
    /// 初始化 DetectionToolSession 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 AttachPresentation；具体条件和先后次序见方法体。
    /// </summary>
    public DetectionToolSession() => AttachPresentation(new Vision.Controls.Presentation.WpfToolPresentation());

    [JsonIgnore, Browsable(false)] public DetectionTool Engine { get; } = new();
    public DetectionParameters Parameters { get => Engine.Parameters; set => Engine.Parameters = value ?? new(); }
    [JsonIgnore, Browsable(false)] public object EditableParameters => Parameters;
    [JsonIgnore, Browsable(false)] public VisionImage? Image { get; set; }
    [JsonIgnore, Browsable(false)] public IAIProvider? Provider { get => Engine.Provider; set => Engine.Provider = value; }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Validate。
    /// 正文中的关键协作调用包括 Validate；具体条件和先后次序见方法体。
    /// </summary>
    public ValidationResult Validate() => Engine.Validate();
    /// <summary>
    /// 运行当前处理流程；对应入口为 Run。
    /// 正文中的关键协作调用包括 Reset、Run、Add；具体条件和先后次序见方法体。
    /// </summary>
    public override void Run()
    {
        Reset();
        ToolState = E_ToolState.Running;
        var result = Engine.Run(Image);
        Results.Add(result);
        RunTime = result.ElapsedMs;
        RunMessage = result.Message;
        ToolState = result.Success ? E_ToolState.RunCompleted : E_ToolState.Failed;
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Reset。
    /// 正文中的关键协作调用包括 Dispose、Clear；具体条件和先后次序见方法体。
    /// </summary>
    public override void Reset() { foreach (var r in Results) r.Dispose(); Results.Clear(); ToolState = E_ToolState.Idle; }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Config。
    /// 正文中的关键协作调用包括 LoadModel；具体条件和先后次序见方法体。
    /// </summary>
    public override void Config() => Engine.LoadModel();
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Pause。
    /// </summary>
    public override void Pause() => ToolState = E_ToolState.Paused;
    /// <summary>
    /// 执行保存操作；对应入口为 SaveSnapShot。
    /// 正文中的关键协作调用包括 Save；具体条件和先后次序见方法体。
    /// </summary>
    public override void SaveSnapShot() => Save();
    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Reset、Dispose；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose() { Reset(); Engine.Dispose(); }
}
