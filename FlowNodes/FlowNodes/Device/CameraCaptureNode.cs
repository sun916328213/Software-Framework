using System.Text.Json.Serialization;
using Device.Camera;
using Device.Camera.Definition;
using FlowEditor.Core;
using FlowEditor.Nodes;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Device;

/// <summary>
/// 相机采集节点：按相机名称采集一张图像，输出 HObject 可绑定到后续节点。
/// </summary>
[FlowTool("相机采集", "Camera", ToolGroup = "设备", ToolSubGroup = "相机", SortIndex = 10, Description = "按相机名称采集一张图像")]
public class CameraCaptureNode : NodeModel
{
    private readonly CamManager _camManager;
    private ImageData? _image;

    public CameraCaptureNode(CamManager camManager)
    {
        _camManager = camManager;
        Name = "相机采集";
        IsInEnabled = true;
        IsOutEnabled = true;
    }

    /// <summary>相机名称（从 <see cref="CameraNames"/> 下拉选择）。</summary>
    private string _cameraName = "";
    [NodeProperty(DisplayName = "相机名称", Group = "1.采集参数", Order = 0, Description = "相机名称（CamManager 中配置的设备名）", ItemsSource = nameof(CameraNames))]
    public string CameraName
    {
        get => _cameraName;
        set => SetProperty(ref _cameraName, value);
    }

    /// <summary>可选相机集合（从 CamManager 动态读取，作为下拉选项）。</summary>
    public List<string> CameraNames => _camManager.DeviceNameCollection();

    /// <summary>曝光时间,单位ms。</summary>
    private int _exposure = 2000;
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
    public ImageData? Image
    {
        get => _image;
        set => SetProperty(ref _image, value);
    }

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var cam = _camManager[CameraName]
            ?? throw new System.Exception($"未找到相机: {CameraName}");

        if (!cam.IsConnected)
            cam.Connect();

        cam.Exposure = Exposure;
        cam.Gain = Gain;
        cam.Snap();
 
        Image    = cam.GetImage();
        await Task.CompletedTask;
    }
}