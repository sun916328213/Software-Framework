using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Device.Controls.Services;
using Device.Workbench.Tests.Fakes;
using Device.Controls.ViewModels;
using Device.Core.Cameras;
using Xunit;

namespace Device.Workbench.Tests;

[Collection(CameraWorkspaceCollection.Name)]
public sealed class CameraViewModelTests
{
    private readonly CameraConfigScope _scope;

    public CameraViewModelTests(CameraConfigScope scope) => _scope = scope;

    private static (CameraViewModel Vm, FakeCameraFactory Factory) CreateVm()
    {
        var factory = new FakeCameraFactory("camera.hik.gige");
        var vm = new CameraViewModel(new CameraWorkspace(factory));
        return (vm, factory);
    }

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

    [Fact]
    public async Task ConnectCommand_WhenDisconnected_ConnectsViaBaseCommand()
    {
        _scope.Reset();
        var (vm, _) = CreateVm();

        await ((IAsyncRelayCommand)vm.ConnectCommand).ExecuteAsync(null);

        Assert.True(vm.Model.IsConnected);
        Assert.Contains("连接相机", vm.OutputText, StringComparison.Ordinal);
    }

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

    [Fact]
    public async Task ApplyTriggerCommand_WithoutConnect_DoesNothingAndKeepsBusyFalse()
    {
        _scope.Reset();
        var (vm, _) = CreateVm();

        await ((IAsyncRelayCommand)vm.ApplyTriggerCommand).ExecuteAsync(null);

        Assert.False(vm.IsBusy);
        Assert.DoesNotContain("下发触发配置", vm.OutputText, StringComparison.Ordinal);
    }

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
}
