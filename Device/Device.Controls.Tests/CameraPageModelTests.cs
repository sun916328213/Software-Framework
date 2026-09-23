using Device.Controls.Models;
using Device.Core.Cameras;
using Xunit;

namespace Device.Controls.Tests;

public sealed class CameraPageModelTests
{
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

    [Fact]
    public void SelectedProfileOrThrow_WithoutProfile_Throws()
    {
        var model = new CameraPageModel();

        var ex = Assert.Throws<InvalidOperationException>(model.SelectedProfileOrThrow);

        Assert.Contains("档案", ex.Message, StringComparison.Ordinal);
    }
}
