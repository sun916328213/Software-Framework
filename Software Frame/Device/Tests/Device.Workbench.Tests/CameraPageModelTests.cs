// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：CameraPageModelTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Workbench、Device.Controls.Models、Device.Core.Cameras、Xunit。导入不代表所有类型都会在运行时加载。
using Device.Workbench;
using Device.Controls.Models;
using Device.Core.Cameras;
using Xunit;

namespace Device.Workbench.Tests;

/// <summary>
/// CameraPageModelTests 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class CameraPageModelTests
{
    /// <summary>
    /// 测试相关入口 StaticLookupLists_ExposeStableEnumAndSourceValues；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void StaticLookupLists_ExposeStableEnumAndSourceValues()
    {
        Assert.Equal(
            [CameraScanType.AreaScan, CameraScanType.LineScan],
            CameraPageModel.ScanTypes);

        Assert.Equal(
            [CameraTriggerMode.Off, CameraTriggerMode.On],
            CameraPageModel.TriggerModes);

        Assert.Contains(CameraTriggerSources.Software, CameraPageModel.TriggerSources);
        Assert.Contains(CameraTriggerSources.Line1, CameraPageModel.TriggerSources);
        Assert.Contains(CameraTriggerSources.RotaryEncoder1, CameraPageModel.TriggerSources);
    }

    /// <summary>
    /// 测试相关入口 Defaults_MatchTestCardExpectations；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、Null、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Defaults_MatchTestCardExpectations()
    {
        var model = new CameraPageModel();

        Assert.Equal("图像实时", model.LiveButtonText);
        Assert.Equal("尚无图像", model.FrameInfo);
        Assert.Null(model.Preview);
        Assert.False(model.IsLineTriggerEnabled);
        Assert.Equal(CameraTriggerMode.On, model.RuntimeFrameTriggerMode);
        Assert.Equal(CameraTriggerSources.Software, model.RuntimeFrameTriggerSource);
        Assert.Equal(CameraTriggerMode.Off, model.RuntimeLineTriggerMode);
        Assert.Equal(CameraTriggerSources.Line1, model.RuntimeLineTriggerSource);
    }

    /// <summary>
    /// 测试相关入口 SelectedProfileChange_ResetsConnectionState；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 False、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void SelectedProfileChange_ResetsConnectionState()
    {
        var model = new CameraPageModel
        {
            SelectedProfile = new CameraProfile { Name = "相机01" },
            IsConnected = true,
            StatusText = "已连接",
        };

        model.SelectedProfile = new CameraProfile { Name = "相机02" };

        Assert.False(model.IsConnected);
        Assert.Equal("未连接", model.StatusText);
    }

    /// <summary>
    /// 测试相关入口 IsLineTriggerEnabled_FollowsSelectedProfileScanType；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 RefreshLineTriggerAvailability、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData(CameraScanType.AreaScan, false)]
    [InlineData(CameraScanType.LineScan, true)]
    public void IsLineTriggerEnabled_FollowsSelectedProfileScanType(
        CameraScanType scanType,
        bool expected)
    {
        var model = new CameraPageModel
        {
            SelectedProfile = new CameraProfile { ScanType = scanType },
        };
        model.RefreshLineTriggerAvailability();

        Assert.Equal(expected, model.IsLineTriggerEnabled);
    }

    /// <summary>
    /// 测试相关入口 RefreshLineTriggerAvailability_RaisesPropertyChanged；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 nameof、RefreshLineTriggerAvailability、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RefreshLineTriggerAvailability_RaisesPropertyChanged()
    {
        var model = new CameraPageModel();
        var raised = false;
        model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CameraPageModel.IsLineTriggerEnabled))
                raised = true;
        };

        model.RefreshLineTriggerAvailability();

        Assert.True(raised);
    }

    /// <summary>
    /// 测试相关入口 BuildRuntimeTriggers_CaptureCurrentTestCardValues；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 BuildRuntimeFrameTrigger、BuildRuntimeLineTrigger、Equal、Null；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void BuildRuntimeTriggers_CaptureCurrentTestCardValues()
    {
        var model = new CameraPageModel
        {
            RuntimeFrameTriggerMode = CameraTriggerMode.On,
            RuntimeFrameTriggerSource = CameraTriggerSources.Line0,
            RuntimeLineTriggerMode = CameraTriggerMode.On,
            RuntimeLineTriggerSource = CameraTriggerSources.Line3,
        };

        var frame = model.BuildRuntimeFrameTrigger();
        var line = model.BuildRuntimeLineTrigger();

        Assert.Equal(CameraTriggerMode.On, frame.Mode);
        Assert.Equal(CameraTriggerSources.Line0, frame.Source);
        Assert.Null(frame.Selector);
        Assert.Equal(CameraTriggerSelectors.LineStart, line.Selector);
        Assert.Equal(CameraTriggerMode.On, line.Mode);
        Assert.Equal(CameraTriggerSources.Line3, line.Source);
    }

    /// <summary>
    /// 测试相关入口 SelectedProfileOrThrow_WithoutProfile_Throws；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void SelectedProfileOrThrow_WithoutProfile_Throws()
    {
        var model = new CameraPageModel();

        var ex = Assert.Throws<InvalidOperationException>(model.SelectedProfileOrThrow);

        Assert.Contains("档案", ex.Message, StringComparison.Ordinal);
    }
}
