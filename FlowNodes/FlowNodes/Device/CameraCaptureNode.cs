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

    /// <summary>采集到的图像（可绑定到输出端口）。</summary>
    [NodeProperty(DisplayName = "图像", Group = "1.采集参数", Order = 3, Description = "采集到的图像", IsOutput = true, IsDisplay = false)]
    [JsonIgnore]
    public ImageFrame? Image
    {
        get => _image;
        set => SetProperty(ref _image, value);
    }

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var camera = _cameras.Get(CameraName);

        if (camera.State != DeviceState.Connected)
        {
            var connect = await camera.ConnectAsync(ct);
            if (!connect.Success)
                throw new InvalidOperationException($"相机 '{CameraName}' 连接失败：{connect.Message}");
        }

        var parameters = await camera.SetExposureGainAsync(Exposure, Gain, ct);
        if (!parameters.Success)
            throw new InvalidOperationException($"相机 '{CameraName}' 下发曝光增益失败：{parameters.Message}");

        Image = await camera.CaptureAsync(ct);
    }
}