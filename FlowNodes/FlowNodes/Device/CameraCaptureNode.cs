using System.Text.Json.Serialization;
using Device.Core.Cameras;
using Device.Core.Devices;
using FlowEditor.Core;
using FlowEditor.Nodes;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Device;

/// <summary>
/// 相机采集节点：按相机名称采集一帧图像，输出 ImageFrame 可绑定到后续节点。
/// </summary>
[FlowTool("相机采集", "Camera", ToolGroup = "设备", ToolSubGroup = "相机", SortIndex = 10, Description = "按相机名称采集一张图像")]
public class CameraCaptureNode : NodeModel
{
    private readonly ICameraProvider _cameras;
    private ImageFrame? _image;

    public CameraCaptureNode(ICameraProvider cameras)
    {
        _cameras = cameras;
        Name = "相机采集";
        IsInEnabled = true;
        IsOutEnabled = true;
    }

    /// <summary>相机名称（从 <see cref="CameraNames"/> 下拉选择）。</summary>
    private string _cameraName = "";
    [NodeProperty(DisplayName = "相机名称", Group = "1.采集参数", Order = 0, Description = "相机名称（宿主 ICameraProvider 中配置的设备名）", ItemsSource = nameof(CameraNames))]
    public string CameraName
    {
        get => _cameraName;
        set => SetProperty(ref _cameraName, value);
    }

    /// <summary>可选相机集合（从宿主 <see cref="ICameraProvider"/> 动态读取，作为下拉选项）。</summary>
    public List<string> CameraNames => _cameras.Names.ToList();

    /// <summary>曝光时间,单位ms。</summary>
    private int _exposure = 20;
    [NodeProperty(DisplayName = "曝光时间(ms)", Group = "1.采集参数", Order = 1, Description = "相机的曝光时间,单位ms")]
    public int Exposure
    {
        get => _exposure;
        set => SetProperty(ref _exposure, value);
    }

    /// <summary>相机增益。</summary>
    private double _gain = 0;
    [NodeProperty(DisplayName = "增益", Group = "1.采集参数", Order = 2, Description = "相机的增益")]
    public double Gain
    {
        get => _gain;
        set => SetProperty(ref _gain, value);
    }

    /// <summary>采集到的图像（可绑定到输出端口）。运行态输出：由 Runtime 统一管理生命周期，不序列化结果值。</summary>
    [NodeProperty(DisplayName = "图像", Group = "1.采集参数", Order = 3, Description = "采集到的图像", IsOutput = true, IsDisplay = false)]
    [NodeOutput(Description = "采集到的图像帧")]
    [NodeRuntimeState(Description = "采集结果，运行期生成，不保存结果值")]
    [JsonIgnore]
    public ImageFrame? Image
    {
        get => _image;
        set => SetProperty(ref _image, value);
    }

    /// <summary>P1：后台 I/O，同相机串行（不同相机可并行）。</summary>
    public override NodeExecutionAffinity Affinity => NodeExecutionAffinity.Background;

    /// <summary>P1：同相机互斥，避免多分支并发采集同一设备。</summary>
    public override NodeConcurrencyMode ConcurrencyMode => NodeConcurrencyMode.ResourceExclusive;

    /// <inheritdoc />
    public override IEnumerable<string> GetResourceKeys() =>
        string.IsNullOrWhiteSpace(CameraName) ? [] : [$"Camera:{CameraName}"];

    /// <summary>
    /// P1：启动前即可发现缺配置、缺设备、参数错误，无需真正执行设备动作。
    /// </summary>
    public override Task<IEnumerable<FlowDiagnostic>> ValidateAsync(NodeValidationContext ctx)
    {
        var problems = new List<FlowDiagnostic>();
        if (string.IsNullOrWhiteSpace(CameraName))
        {
            problems.Add(ValidationError("E-CAMERA-NAME-EMPTY", "未配置相机名称。"));
            return Task.FromResult<IEnumerable<FlowDiagnostic>>(problems);
        }
        var cameras = ctx.GetService<ICameraProvider>() ?? _cameras;
        if (cameras == null)
        {
            problems.Add(ValidationError("E-CAMERA-PROVIDER-MISSING", "缺少相机服务（ICameraProvider）。"));
            return Task.FromResult<IEnumerable<FlowDiagnostic>>(problems);
        }
        if (!cameras.Names.Contains(CameraName))
            problems.Add(ValidationError("E-CAMERA-NOTFOUND", $"相机 '{CameraName}' 不存在。"));
        if (Exposure <= 0)
            problems.Add(ValidationError("E-CAMERA-PARAM-RANGE", $"曝光时间必须 > 0，当前为 {Exposure}。"));
        return Task.FromResult<IEnumerable<FlowDiagnostic>>(problems);
    }

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        var ctx = NodeExecutionContext.CreateTest(Id, ct);
        await ExecuteAsync(ctx);
    }

    /// <summary>
    /// P0 样板：从 Context 获取取消令牌/日志/服务；设备缺失、连接失败、参数下发失败均抛
    /// 带稳定 ErrorCode/Category 的 <see cref="FlowNodeException"/>，Runtime 不解析字符串。
    /// 图像输出由 Runtime 统一管理生命周期，本节点不 Dispose 上游输入。
    /// </summary>
    public override async Task ExecuteAsync(NodeExecutionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ctx.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(CameraName))
            throw FlowNodeException.Config("E-CAMERA-NAME-EMPTY", $"节点 [{Name}] 未配置相机名称。", NodeId: Id);

        // P0：运行时依赖优先从 Context 获取，构造注入作为回退；不再主动访问全局单例。
        var cameras = ctx.GetService<ICameraProvider>() ?? _cameras;
        if (cameras == null)
            throw FlowNodeException.Config("E-CAMERA-PROVIDER-MISSING", $"节点 [{Name}] 缺少相机服务（ICameraProvider）。", NodeId: Id);

        Device.Core.Cameras.ICamera camera;
        try
        {
            camera = cameras.Get(CameraName);
        }
        catch (Exception ex)
        {
            throw FlowNodeException.Config("E-CAMERA-NOTFOUND", $"相机 '{CameraName}' 不存在。", NodeId: Id, inner: ex);
        }

        if (camera.State != DeviceState.Connected)
        {
            Device.Core.Devices.OperationResult connect;
            try
            {
                connect = await camera.ConnectAsync(ctx.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw FlowNodeException.Cancelled(NodeId: Id);
            }
            catch (Exception ex)
            {
                throw FlowNodeException.DeviceOffline($"相机 '{CameraName}' 连接异常：{ex.Message}", NodeId: Id, inner: ex);
            }
            if (!connect.Success)
                throw FlowNodeException.DeviceOffline($"相机 '{CameraName}' 连接失败：{connect.Message}", NodeId: Id);
        }

        try
        {
            var parameters = await camera.SetExposureGainAsync(Exposure, Gain, ctx.CancellationToken);
            if (!parameters.Success)
                throw FlowNodeException.Config("E-CAMERA-PARAM-REJECTED", $"相机 '{CameraName}' 下发曝光增益失败：{parameters.Message}", NodeId: Id);
        }
        catch (FlowNodeException) { throw; }
        catch (OperationCanceledException)
        {
            throw FlowNodeException.Cancelled(NodeId: Id);
        }

        try
        {
            Image = await camera.CaptureAsync(ctx.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw FlowNodeException.Cancelled(NodeId: Id);
        }
        catch (FlowNodeException) { throw; }
        catch (Exception ex)
        {
            throw FlowNodeException.DeviceOffline($"相机 '{CameraName}' 采集失败：{ex.Message}", NodeId: Id, inner: ex);
        }

        ctx.Logger.LogInfo($"相机 '{CameraName}' 采集完成。");
    }
}