// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：CameraWorkspaceTests、CameraConfigScope、CameraWorkspaceCollection。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Workbench、System.IO、Device.Controls.Models、Device.Controls.Services、Device.Workbench.Tests.Fakes、Device.Core.Cameras。导入不代表所有类型都会在运行时加载。
using Device.Workbench;
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

    /// <summary>
    /// 初始化 CameraWorkspaceTests 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public CameraWorkspaceTests(CameraConfigScope scope) => _scope = scope;

    /// <summary>
    /// 验证 Ctor_NoProfilesFile_CreatesFallbackProfileAndDriverList 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Reset、Equal、Single、Same、False；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Ctor_NoDrivers_Throws 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Throws；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Ctor_NoDrivers_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new CameraWorkspace(new FakeCameraFactory()));
    }

    /// <summary>
    /// 验证 Ctor_LoadsExistingProfilesFromFile 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 WriteProfiles、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 ConnectAsync_Success_MarksConnectedAndSeedsRuntimeTriggerFromProfile 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Reset、ConnectAsync、True、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 ConnectAsync_FactoryFailure_ThrowsAndStaysDisconnected 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Reset、ConnectAsync、DisconnectAsync、ThrowsAsync、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 CaptureAsync_BeforeConnect_Throws 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Reset、ThrowsAsync、Contains、Null、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 CaptureAsync_Connected_FillsPreviewAndFrameInfo 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Reset、ConnectAsync、CaptureAsync、NotNull、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 ConnectAsync_AfterProfileFieldChange_RebuildsCameraInstance 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Reset、ConnectAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 ConnectAsync_SameProfile_ReusesCameraInstance 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Reset、ConnectAsync、DisconnectAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 ToggleLive_WithoutConnect_Throws 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Reset、Throws、Contains、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ToggleLive_WithoutConnect_Throws()
    {
        _scope.Reset();
        var workspace = new CameraWorkspace(new FakeCameraFactory("camera.hik.gige"));

        var ex = Assert.Throws<InvalidOperationException>(workspace.ToggleLive);

        Assert.Contains("请先连接", ex.Message, StringComparison.Ordinal);
        Assert.Equal("图像实时", workspace.Model.LiveButtonText);
    }

    /// <summary>
    /// 验证 ToggleLive_WithConnect_StartsAndStopResetsButtonText 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Reset、ConnectAsync、ToggleLive、Equal、StopLive；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 DisconnectAsync_StopsLiveAndClearsConnection 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Reset、ConnectAsync、ToggleLive、DisconnectAsync、False；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 ApplyTriggerAsync_Connected_PushesRuntimeSettings 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Reset、ConnectAsync、ApplyTriggerAsync、Single、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 SetCurDevice_SwitchingProfile_SyncsRuntimeTriggerFromProfile 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Reset、Add、SetCurDevice、Equal、False；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 CompleteAddedProfile_NamesAndSelectsFallbackDriver 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Reset、CompleteAddedProfile、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CompleteAddedProfile_NamesAndSelectsFallbackDriver()
    {
        _scope.Reset();
        var workspace = new CameraWorkspace(new FakeCameraFactory("camera.hik.gige"));

        workspace.CompleteAddedProfile();

        Assert.Equal("相机2", workspace.Model.SelectedProfile?.Name);
        Assert.Equal("camera.hik.gige", workspace.Model.SelectedProfile?.DriverId);
    }

    /// <summary>
    /// 验证 FixAfterDelete_EmptyCollection_RestoresFallbackProfile 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Reset、Clear、FixAfterDelete、Single、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 SaveProfiles_RoundTripsThroughDisk 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Reset、SaveProfiles、True、Exists、TryRead；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 初始化 CameraConfigScope 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 Reset；具体条件和先后次序见方法体。
    /// </summary>
    public CameraConfigScope() => Reset();

    /// <summary>
    /// 验证 Reset 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Exists、Delete；具体条件和先后次序见方法体。
    /// </summary>
    public void Reset()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }

    /// <summary>
    /// 验证 Dispose 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Reset；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose() => Reset();

    /// <summary>
    /// 验证 WriteProfiles 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetDirectoryName、CreateDirectory、Save；具体条件和先后次序见方法体。
    /// </summary>
    public void WriteProfiles(params CameraProfile[] profiles)
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        profiles.Save(FilePath);
    }
}

/// <summary>
/// CameraWorkspaceCollection 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// 继承或实现的契约：ICollectionFixture&lt;CameraConfigScope&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CameraWorkspaceCollection : ICollectionFixture<CameraConfigScope>
{
    public const string Name = "CameraWorkspace";
}
