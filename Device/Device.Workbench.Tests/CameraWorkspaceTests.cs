using System.IO;
using Device.Controls.Models;
using Device.Controls.Services;
using Device.Workbench.Tests.Fakes;
using Device.Core.Cameras;
using Device.Core.Devices;
using Extension.Core.Conversion;
using Xunit;

namespace Device.Workbench.Tests;

/// <summary>
/// 相机测试页流程：档案兜底/读盘、连接状态、抓图回填、实时开关、指纹重建、触发播种。
/// </summary>
[Collection(CameraWorkspaceCollection.Name)]
public sealed class CameraWorkspaceTests
{
    private readonly CameraConfigScope _scope;

    public CameraWorkspaceTests(CameraConfigScope scope) => _scope = scope;

    [Fact]
    public void Ctor_NoProfilesFile_CreatesFallbackProfileAndDriverList()
    {
        _scope.Reset();

        var factory = new FakeCameraFactory("camera.hik.gige", "camera.basler.gige");
        var workspace = new CameraWorkspace(factory);

        Assert.Equal(["camera.hik.gige", "camera.basler.gige"], workspace.Model.Drivers);
        Assert.Single(workspace.Model.DataCollection);
        var profile = Assert.Single(workspace.Model.DataCollection);
        Assert.Equal("相机01", profile.Name);
        Assert.Equal("camera.hik.gige", profile.DriverId);
        Assert.Same(profile, workspace.Model.SelectedProfile);
        Assert.Equal("未连接", workspace.Model.StatusText);
        Assert.False(workspace.Model.IsConnected);
    }

    [Fact]
    public void Ctor_NoDrivers_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new CameraWorkspace(new FakeCameraFactory()));
    }

    [Fact]
    public void Ctor_LoadsExistingProfilesFromFile()
    {
        _scope.WriteProfiles(
            new CameraProfile { Name = "产线相机", DriverId = "camera.dahua.gige", IpAddress = "192.168.9.9" },
            new CameraProfile { Name = "备用相机", DriverId = "camera.hik.gige" });

        var workspace = new CameraWorkspace(new FakeCameraFactory("camera.hik.gige", "camera.dahua.gige"));

        Assert.Equal(2, workspace.Model.DataCollection.Count);
        Assert.Equal("产线相机", workspace.Model.SelectedProfile?.Name);
        Assert.Equal("camera.dahua.gige", workspace.Model.SelectedProfile?.DriverId);
    }

    [Fact]
    public async Task ConnectAsync_Success_MarksConnectedAndSeedsRuntimeTriggerFromProfile()
    {
        _scope.Reset();
        var factory = new FakeCameraFactory("camera.hik.gige");
        var workspace = new CameraWorkspace(factory);
        var profile = workspace.Model.SelectedProfile!;
        profile.FrameTriggerMode = CameraTriggerMode.Off;
        profile.FrameTriggerSource = CameraTriggerSources.Line2;
        profile.LineTriggerMode = CameraTriggerMode.On;
        profile.LineTriggerSource = CameraTriggerSources.Line3;

        // 模拟测试卡上的临时改动：连接后应被档案值覆盖。
        workspace.Model.RuntimeFrameTriggerMode = CameraTriggerMode.On;
        workspace.Model.RuntimeFrameTriggerSource = CameraTriggerSources.Software;

        await workspace.ConnectAsync();

        Assert.True(workspace.Model.IsConnected);
        Assert.Equal("已连接", workspace.Model.StatusText);
        Assert.Equal(CameraTriggerMode.Off, workspace.Model.RuntimeFrameTriggerMode);
        Assert.Equal(CameraTriggerSources.Line2, workspace.Model.RuntimeFrameTriggerSource);
        Assert.Equal(CameraTriggerMode.On, workspace.Model.RuntimeLineTriggerMode);
        Assert.Equal(CameraTriggerSources.Line3, workspace.Model.RuntimeLineTriggerSource);
        Assert.Equal(1, factory.Cameras[0].ConnectCount);
    }

    [Fact]
    public async Task ConnectAsync_FactoryFailure_ThrowsAndStaysDisconnected()
    {
        _scope.Reset();
        var factory = new FakeCameraFactory("camera.hik.gige");
        var workspace = new CameraWorkspace(factory);
        // 先连一次拿到实例并让后续 Create 返回失败相机。
        await workspace.ConnectAsync();
        await workspace.DisconnectAsync();
        factory.Cameras[^1].FailConnect = true;

        // 指纹未变会复用同一实例。
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(workspace.ConnectAsync);

        Assert.Contains("测试连接失败", ex.Message, StringComparison.Ordinal);
        Assert.False(workspace.Model.IsConnected);
        Assert.Equal("未连接", workspace.Model.StatusText);
    }

    [Fact]
    public async Task CaptureAsync_BeforeConnect_Throws()
    {
        _scope.Reset();
        var workspace = new CameraWorkspace(new FakeCameraFactory("camera.hik.gige"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(workspace.CaptureAsync);

        Assert.Contains("请先连接", ex.Message, StringComparison.Ordinal);
        Assert.Null(workspace.Model.Preview);
        Assert.Equal("尚无图像", workspace.Model.FrameInfo);
    }

    [Fact]
    public async Task CaptureAsync_Connected_FillsPreviewAndFrameInfo()
    {
        _scope.Reset();
        var factory = new FakeCameraFactory("camera.hik.gige");
        var workspace = new CameraWorkspace(factory);
        await workspace.ConnectAsync();
        factory.Cameras[^1].NextFrame = new ImageFrame(6, 4, "Mono8", new byte[24]);

        await workspace.CaptureAsync();

        Assert.NotNull(workspace.Model.Preview);
        Assert.Equal("6×4 Mono8", workspace.Model.FrameInfo);
        Assert.Equal(1, factory.Cameras[^1].CaptureCount);
    }

    [Fact]
    public async Task ConnectAsync_AfterProfileFieldChange_RebuildsCameraInstance()
    {
        _scope.Reset();
        var factory = new FakeCameraFactory("camera.hik.gige");
        var workspace = new CameraWorkspace(factory);
        var profile = workspace.Model.SelectedProfile!;

        await workspace.ConnectAsync();
        Assert.Equal(1, factory.CreateCallCount);

        profile.ExposureMilliseconds = 99;
        await workspace.ConnectAsync();

        Assert.Equal(2, factory.CreateCallCount);
        Assert.Equal(99, factory.CreatedOptions[^1].ExposureMilliseconds);
        Assert.Equal(1, factory.Cameras[0].DisposeCount);
    }

    [Fact]
    public async Task ConnectAsync_SameProfile_ReusesCameraInstance()
    {
        _scope.Reset();
        var factory = new FakeCameraFactory("camera.hik.gige");
        var workspace = new CameraWorkspace(factory);

        await workspace.ConnectAsync();
        await workspace.DisconnectAsync();
        await workspace.ConnectAsync();

        Assert.Equal(1, factory.CreateCallCount);
        Assert.Equal(2, factory.Cameras[0].ConnectCount);
    }

    [Fact]
    public void ToggleLive_WithoutConnect_Throws()
    {
        _scope.Reset();
        var workspace = new CameraWorkspace(new FakeCameraFactory("camera.hik.gige"));

        var ex = Assert.Throws<InvalidOperationException>(workspace.ToggleLive);

        Assert.Contains("请先连接", ex.Message, StringComparison.Ordinal);
        Assert.Equal("图像实时", workspace.Model.LiveButtonText);
    }

    [Fact]
    public async Task ToggleLive_WithConnect_StartsAndStopResetsButtonText()
    {
        _scope.Reset();
        var factory = new FakeCameraFactory("camera.hik.gige");
        var workspace = new CameraWorkspace(factory);
        await workspace.ConnectAsync();

        workspace.ToggleLive();
        Assert.Equal("实时关闭", workspace.Model.LiveButtonText);

        workspace.StopLive();
        Assert.Equal("图像实时", workspace.Model.LiveButtonText);

        // StopLive 可重入。
        workspace.StopLive();
        Assert.Equal("图像实时", workspace.Model.LiveButtonText);
    }

    [Fact]
    public async Task DisconnectAsync_StopsLiveAndClearsConnection()
    {
        _scope.Reset();
        var factory = new FakeCameraFactory("camera.hik.gige");
        var workspace = new CameraWorkspace(factory);
        await workspace.ConnectAsync();
        workspace.ToggleLive();

        await workspace.DisconnectAsync();

        Assert.False(workspace.Model.IsConnected);
        Assert.Equal("未连接", workspace.Model.StatusText);
        Assert.Equal("图像实时", workspace.Model.LiveButtonText);
    }

    [Fact]
    public async Task ApplyTriggerAsync_Connected_PushesRuntimeSettings()
    {
        _scope.Reset();
        var factory = new FakeCameraFactory("camera.hik.gige");
        var workspace = new CameraWorkspace(factory);
        await workspace.ConnectAsync();
        workspace.Model.RuntimeFrameTriggerMode = CameraTriggerMode.On;
        workspace.Model.RuntimeFrameTriggerSource = CameraTriggerSources.Line1;
        workspace.Model.RuntimeLineTriggerMode = CameraTriggerMode.On;
        workspace.Model.RuntimeLineTriggerSource = CameraTriggerSources.RotaryEncoder1;

        await workspace.ApplyTriggerAsync();

        var applied = Assert.Single(factory.Cameras[^1].AppliedTriggers);
        Assert.Equal(CameraTriggerSources.Line1, applied.Frame.Source);
        Assert.Equal(CameraTriggerSelectors.LineStart, applied.Line.Selector);
        Assert.Equal(CameraTriggerSources.RotaryEncoder1, applied.Line.Source);
    }

    [Fact]
    public void SetCurDevice_SwitchingProfile_SyncsRuntimeTriggerFromProfile()
    {
        _scope.Reset();
        var factory = new FakeCameraFactory("camera.hik.gige");
        var workspace = new CameraWorkspace(factory);
        var second = new CameraProfile
        {
            Name = "相机02",
            DriverId = "camera.hik.gige",
            FrameTriggerMode = CameraTriggerMode.Off,
            FrameTriggerSource = CameraTriggerSources.Line0,
        };
        workspace.Model.DataCollection.Add(second);

        workspace.Model.RuntimeFrameTriggerMode = CameraTriggerMode.On;
        workspace.Model.SelectedProfile = second;
        workspace.SetCurDevice();

        Assert.Equal(CameraTriggerMode.Off, workspace.Model.RuntimeFrameTriggerMode);
        Assert.Equal(CameraTriggerSources.Line0, workspace.Model.RuntimeFrameTriggerSource);
        Assert.False(workspace.Model.IsConnected);
    }

    [Fact]
    public void CompleteAddedProfile_NamesAndSelectsFallbackDriver()
    {
        _scope.Reset();
        var workspace = new CameraWorkspace(new FakeCameraFactory("camera.hik.gige"));

        workspace.CompleteAddedProfile();

        Assert.Equal("相机2", workspace.Model.SelectedProfile?.Name);
        Assert.Equal("camera.hik.gige", workspace.Model.SelectedProfile?.DriverId);
    }

    [Fact]
    public void FixAfterDelete_EmptyCollection_RestoresFallbackProfile()
    {
        _scope.Reset();
        var workspace = new CameraWorkspace(new FakeCameraFactory("camera.hik.gige"));
        workspace.Model.DataCollection.Clear();
        workspace.Model.SelectedProfile = null;

        workspace.FixAfterDelete();

        var profile = Assert.Single(workspace.Model.DataCollection);
        Assert.Equal("相机01", profile.Name);
        Assert.Same(profile, workspace.Model.SelectedProfile);
    }

    [Fact]
    public void SaveProfiles_RoundTripsThroughDisk()
    {
        _scope.Reset();
        var workspace = new CameraWorkspace(new FakeCameraFactory("camera.hik.gige"));
        workspace.Model.SelectedProfile!.Name = "落盘相机";
        workspace.Model.SelectedProfile.IpAddress = "172.16.0.8";

        workspace.SaveProfiles();

        Assert.True(File.Exists(workspace.Model.FullPath));
        var reloaded = JsonEx.TryRead<List<CameraProfile>>(workspace.Model.FullPath!);
        Assert.NotNull(reloaded);
        Assert.Contains(reloaded, p => p.Name == "落盘相机" && p.IpAddress == "172.16.0.8");
    }
}

/// <summary>相机页共用 Config/Cameras.json，串行并隔离读写。</summary>
public sealed class CameraConfigScope
{
    public string FilePath { get; } =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Cameras.json");

    public CameraConfigScope() => Reset();

    public void Reset()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }

    public void Dispose() => Reset();

    public void WriteProfiles(params CameraProfile[] profiles)
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        profiles.Save(FilePath);
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CameraWorkspaceCollection : ICollectionFixture<CameraConfigScope>
{
    public const string Name = "CameraWorkspace";
}
