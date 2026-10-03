// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：HalconToolSession。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Text.Json.Serialization、System.ComponentModel、HalconDotNet、Vision.Core.Results、Vision.Core.Tools、Vision.Tools.Halcon.Internal。导入不代表所有类型都会在运行时加载。
using System.Text.Json.Serialization;
using System.ComponentModel;
using HalconDotNet;
using Vision.Core.Results;
using Vision.Core.Tools;
using Vision.Tools.Halcon.Internal;

namespace Vision.Tools.Halcon.Sessions;

/// <summary>Named, persisted execution state. Input images belong to the caller.</summary>
/// <remarks>
/// HalconToolSession 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：Tool、IEditableTool、IDisposable。具体约束按这些契约及各成员实现解释。
/// </remarks>
public abstract class HalconToolSession : Tool, IEditableTool, IDisposable
{
    [JsonIgnore, Browsable(false)] public HObject? Image { get; set; }
    [JsonIgnore, Browsable(false)] public abstract object EditableParameters { get; }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Validate。
    /// </summary>
    public abstract ValidationResult Validate();
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Config。
    /// 正文中的关键协作调用包括 RaiseRequestShowView；具体条件和先后次序见方法体。
    /// </summary>
    public override void Config() => RaiseRequestShowView(this);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Pause。
    /// </summary>
    public override void Pause() => ToolState = E_ToolState.Paused;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Reset。
    /// 正文中的关键协作调用包括 Dispose、Clear；具体条件和先后次序见方法体。
    /// </summary>
    public override void Reset()
    {
        foreach (var result in Results) result.Dispose();
        Results.Clear();
        ToolState = E_ToolState.Idle;
    }
    /// <summary>
    /// 执行保存操作；对应入口为 SaveSnapShot。
    /// 正文中的关键协作调用包括 Save；具体条件和先后次序见方法体。
    /// </summary>
    public override void SaveSnapShot() => Save();
    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// </summary>
    public abstract void Dispose();
}

/// <summary>Keeps parameters/identity in JSON and runtime handles outside serialization.</summary>
/// <remarks>
/// HalconToolSession 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：HalconToolSession。具体约束按这些契约及各成员实现解释。
/// </remarks>
public class HalconToolSession<TParameters, TResult, TTool> : HalconToolSession
    where TParameters : class, new()
    where TResult : ToolResult, new()
    where TTool : HalconToolBase<TParameters, TResult>, new()
{
    [JsonIgnore, Browsable(false)] public TTool Engine { get; } = new();
    public TParameters Parameters { get => Engine.Parameters; set => Engine.Parameters = value ?? new(); }
    [JsonIgnore, Browsable(false)] public override object EditableParameters => Parameters;
    [JsonIgnore, Browsable(false)] public TResult? LastResult { get; private set; }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Reset。
    /// 正文中的关键协作调用包括 Reset；具体条件和先后次序见方法体。
    /// </summary>
    public override void Reset() { base.Reset(); LastResult = null; }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Validate。
    /// 正文中的关键协作调用包括 Validate；具体条件和先后次序见方法体。
    /// </summary>
    public override ValidationResult Validate() => Engine.Validate();
    /// <summary>
    /// 运行当前处理流程；对应入口为 Run。
    /// 正文中的关键协作调用包括 Reset、Run、Add；具体条件和先后次序见方法体。
    /// </summary>
    public override void Run()
    {
        Reset();
        ToolState = E_ToolState.Running;
        LastResult = Engine.Run(Image);
        Results.Add(LastResult);
        RunTime = LastResult.ElapsedMs;
        RunMessage = LastResult.Message;
        ToolState = LastResult.Success ? E_ToolState.RunCompleted : E_ToolState.Failed;
    }
    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Reset、Dispose；具体条件和先后次序见方法体。
    /// </summary>
    public override void Dispose() { Reset(); Engine.Dispose(); Image = null; }
}
