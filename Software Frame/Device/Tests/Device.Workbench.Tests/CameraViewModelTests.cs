// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：CameraViewModelTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Workbench、System.Windows.Input、CommunityToolkit.Mvvm.Input、Device.Controls.Services、Device.Workbench.Tests.Fakes、Device.Controls.ViewModels。导入不代表所有类型都会在运行时加载。
using Device.Workbench;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Device.Controls.Services;
using Device.Workbench.Tests.Fakes;
using Device.Controls.ViewModels;
using Device.Core.Cameras;
using Xunit;

namespace Device.Workbench.Tests;

/// <summary>
/// CameraViewModelTests 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection(CameraWorkspaceCollection.Name)]
public sealed class CameraViewModelTests
{
    private readonly CameraConfigScope _scope;

    /// <summary>
    /// 初始化 CameraViewModelTests 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public CameraViewModelTests(CameraConfigScope scope) => _scope = scope;

    /// <summary>
    /// 测试相关入口 CreateVm；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    private static (CameraViewModel Vm, FakeCameraFactory Factory) CreateVm()
    {
        var factory = new FakeCameraFactory("camera.hik.gige");
        var vm = new CameraViewModel(new CameraWorkspace(factory));
        return (vm, factory);
    }

    /// <summary>
    /// 测试相关入口 Model_ExposesWorkspaceState；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Reset、CreateVm、NotNull、Equal、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Model_ExposesWorkspaceState()
    {
        _scope.Reset();
        var (vm, _) = CreateVm();

        Assert.NotNull(vm.Model);
        Assert.Equal("图像实时", vm.Model.LiveButtonText);
        Assert.False(vm.IsBusy);
        Assert.Equal("-", vm.LastElapsed);
        Assert.Equal(string.Empty, vm.OutputText);
    }

    /// <summary>
    /// 测试相关入口 ConnectToggleAsync_WhenDisconnected_ConnectsAndLogsSuccess；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Reset、CreateVm、ExecuteAsync、True、False；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ConnectToggleAsync_WhenDisconnected_ConnectsAndLogsSuccess()
    {
        _scope.Reset();
        var (vm, _) = CreateVm();

        await ((IAsyncRelayCommand)vm.ConnectToggleCommand).ExecuteAsync(null);

        Assert.True(vm.Model.IsConnected);
        Assert.False(vm.IsBusy);
        Assert.Contains("连接相机", vm.OutputText, StringComparison.Ordinal);
        Assert.Contains("✓", vm.OutputText, StringComparison.Ordinal);
        Assert.NotEqual("-", vm.LastElapsed);
        Assert.DoesNotContain("✗", vm.OutputText, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 ConnectCommand_WhenDisconnected_ConnectsViaBaseCommand；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Reset、CreateVm、ExecuteAsync、True、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ConnectCommand_WhenDisconnected_ConnectsViaBaseCommand()
    {
        _scope.Reset();
        var (vm, _) = CreateVm();

        await ((IAsyncRelayCommand)vm.ConnectCommand).ExecuteAsync(null);

        Assert.True(vm.Model.IsConnected);
        Assert.Contains("连接相机", vm.OutputText, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 CaptureCommand_Connected_FillsPreviewAndLogs；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Reset、CreateVm、ExecuteAsync、NotNull、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task CaptureCommand_Connected_FillsPreviewAndLogs()
    {
        _scope.Reset();
        var (vm, factory) = CreateVm();
        await ((IAsyncRelayCommand)vm.ConnectToggleCommand).ExecuteAsync(null);
        factory.Cameras[^1].NextFrame = new ImageFrame(3, 2, "Mono8", new byte[6]);

        await ((IAsyncRelayCommand)vm.CaptureCommand).ExecuteAsync(null);

        Assert.NotNull(vm.Model.Preview);
        Assert.Equal("3×2 Mono8", vm.Model.FrameInfo);
        Assert.Contains("抓图", vm.OutputText, StringComparison.Ordinal);
        Assert.DoesNotContain("✗", vm.OutputText, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 CaptureCommand_BeforeConnect_LogsFailure；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Reset、CreateVm、ExecuteAsync、Null、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task CaptureCommand_BeforeConnect_LogsFailure()
    {
        _scope.Reset();
        var (vm, _) = CreateVm();

        await ((IAsyncRelayCommand)vm.CaptureCommand).ExecuteAsync(null);

        Assert.Null(vm.Model.Preview);
        Assert.Contains("✗", vm.OutputText, StringComparison.Ordinal);
        Assert.Contains("请先连接", vm.OutputText, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 LiveCommand_TogglesLiveButtonTextAndStopsOnDisconnect；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Reset、CreateVm、ExecuteAsync、Execute、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task LiveCommand_TogglesLiveButtonTextAndStopsOnDisconnect()
    {
        _scope.Reset();
        var (vm, _) = CreateVm();
        await ((IAsyncRelayCommand)vm.ConnectToggleCommand).ExecuteAsync(null);

        vm.LiveCommand.Execute(null);
        Assert.Equal("实时关闭", vm.Model.LiveButtonText);
        Assert.Contains("实时", vm.OutputText, StringComparison.Ordinal);

        vm.LiveCommand.Execute(null);
        Assert.Equal("图像实时", vm.Model.LiveButtonText);

        await ((IAsyncRelayCommand)vm.DisconnectCommand).ExecuteAsync(null);
        Assert.False(vm.Model.IsConnected);
        Assert.Equal("图像实时", vm.Model.LiveButtonText);
    }

    /// <summary>
    /// 测试相关入口 ApplyTriggerCommand_WithoutConnect_DoesNothingAndKeepsBusyFalse；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Reset、CreateVm、ExecuteAsync、False、DoesNotContain；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ApplyTriggerCommand_WithoutConnect_DoesNothingAndKeepsBusyFalse()
    {
        _scope.Reset();
        var (vm, _) = CreateVm();

        await ((IAsyncRelayCommand)vm.ApplyTriggerCommand).ExecuteAsync(null);

        Assert.False(vm.IsBusy);
        Assert.DoesNotContain("下发触发配置", vm.OutputText, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 ApplyTriggerCommand_Connected_PushesRuntimeTriggerAndLogs；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Reset、CreateVm、ExecuteAsync、Contains、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ApplyTriggerCommand_Connected_PushesRuntimeTriggerAndLogs()
    {
        _scope.Reset();
        var (vm, factory) = CreateVm();
        await ((IAsyncRelayCommand)vm.ConnectToggleCommand).ExecuteAsync(null);
        vm.Model.RuntimeFrameTriggerMode = CameraTriggerMode.On;
        vm.Model.RuntimeFrameTriggerSource = CameraTriggerSources.Line1;

        await ((IAsyncRelayCommand)vm.ApplyTriggerCommand).ExecuteAsync(null);

        Assert.Contains("下发触发配置", vm.OutputText, StringComparison.Ordinal);
        Assert.Contains("帧触发", vm.OutputText, StringComparison.Ordinal);
        Assert.Equal(CameraTriggerSources.Line1, factory.Cameras[^1].AppliedTriggers[^1].Frame.Source);
        Assert.DoesNotContain("✗", vm.OutputText, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 ClearOutputCommand_ClearsLog；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Reset、CreateVm、ProfileAddedAction、NotEqual、Execute；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ClearOutputCommand_ClearsLog()
    {
        _scope.Reset();
        var (vm, _) = CreateVm();
        vm.ProfileAddedAction();

        Assert.NotEqual(string.Empty, vm.OutputText);

        vm.ClearOutputCommand.Execute(null);

        Assert.Equal(string.Empty, vm.OutputText);
    }

    /// <summary>
    /// 测试相关入口 ProfileCallbacks_AddDeleteSaveSelect_LogExpectedMessages；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Reset、CreateVm、ProfileAddedAction、Contains、SelectCallbackAction；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ProfileCallbacks_AddDeleteSaveSelect_LogExpectedMessages()
    {
        _scope.Reset();
        var (vm, _) = CreateVm();

        vm.ProfileAddedAction();
        Assert.Contains("已添加相机档案", vm.OutputText, StringComparison.Ordinal);

        vm.SelectCallbackAction();
        Assert.Contains("当前相机", vm.OutputText, StringComparison.Ordinal);

        vm.ProfilesSavedAction();
        Assert.Contains("已保存", vm.OutputText, StringComparison.Ordinal);

        while (vm.Model.DataCollection.Count > 0)
            vm.Model.DataCollection.RemoveAt(vm.Model.DataCollection.Count - 1);
        vm.ProfileDeletedAction();
        Assert.NotEmpty(vm.Model.DataCollection);
    }

    /// <summary>
    /// 测试相关入口 ConnectToggleAsync_WhenAlreadyConnected_Disconnects；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Reset、CreateVm、ExecuteAsync、True、False；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ConnectToggleAsync_WhenAlreadyConnected_Disconnects()
    {
        _scope.Reset();
        var (vm, _) = CreateVm();

        await ((IAsyncRelayCommand)vm.ConnectToggleCommand).ExecuteAsync(null);
        Assert.True(vm.Model.IsConnected);

        await ((IAsyncRelayCommand)vm.ConnectToggleCommand).ExecuteAsync(null);

        Assert.False(vm.Model.IsConnected);
        Assert.Equal("未连接", vm.Model.StatusText);
        Assert.Contains("断开相机", vm.OutputText, StringComparison.Ordinal);
    }
    /// <summary>
    /// 测试相关入口 CommandAvailability_FollowsConnectionAndRetirement；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Reset、CreateVm、True、CanExecute、False；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task CommandAvailability_FollowsConnectionAndRetirement()
    {
        _scope.Reset();
        var (vm, factory) = CreateVm();
        Assert.True(vm.ConnectCommand.CanExecute(null));
        Assert.False(vm.CaptureCommand.CanExecute(null));
        await vm.ConnectCommand.ExecuteAsync(null);
        Assert.False(vm.ConnectCommand.CanExecute(null));
        Assert.True(vm.CaptureCommand.CanExecute(null));
        vm.Dispose();
        Assert.False(vm.ConnectToggleCommand.CanExecute(null));
        Assert.False(vm.CaptureCommand.CanExecute(null));
        await vm.ConnectToggleCommand.ExecuteAsync(null);
        Assert.Equal(1, Assert.Single(factory.Cameras).ConnectCount);
    }

    /// <summary>
    /// 测试相关入口 WorkspaceDisposal_ReleasesOwnedCameraOnce_AndRejectsReconnect；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Reset、ConnectAsync、DisposeAsync、True、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task WorkspaceDisposal_ReleasesOwnedCameraOnce_AndRejectsReconnect()
    {
        _scope.Reset();
        var factory = new FakeCameraFactory("camera.hik.gige");
        var workspace = new CameraWorkspace(factory);
        await workspace.ConnectAsync();
        await workspace.DisposeAsync();
        await workspace.DisposeAsync();
        Assert.True(workspace.IsClosed);
        Assert.Equal(1, Assert.Single(factory.Cameras).DisposeCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => workspace.ConnectAsync());
    }

}
