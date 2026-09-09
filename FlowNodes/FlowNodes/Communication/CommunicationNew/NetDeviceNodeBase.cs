using CommunicationNew.Core;
using FlowEditor.Nodes;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.CommunicationNew;

/// <summary>
/// 已创建设备的操作节点基类：统一设备选择、在线检查、取消和错误记录语义。
/// </summary>
internal abstract class NetDeviceNodeBase : NodeModel
{
    protected NetDeviceNodeBase(CommunicationManager manager)
    {
        Manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    protected CommunicationManager Manager { get; }

    private string _deviceName = string.Empty;

    [NodeProperty(DisplayName = "设备名称", Group = "1.基础", Order = 0, Description = "已创建的设备名", ItemsSource = nameof(DeviceNames))]
    public string DeviceName
    {
        get => _deviceName;
        set => SetProperty(ref _deviceName, value);
    }

    /// <summary>管理器中已创建的设备名（下拉选项）。</summary>
    public List<string> DeviceNames => NetDeviceHelper.DeviceNames(Manager);

    protected ICommunicationDevice GetDevice(bool requireConnected = true)
    {
        var device = NetDeviceHelper.GetDeviceOrThrow(Manager, DeviceName);
        if (requireConnected)
            NetDeviceHelper.EnsureConnected(device);
        return device;
    }

    protected async Task<T> RunDeviceOperationAsync<T>(
        string operation,
        string? target,
        CancellationToken ct,
        Func<ICommunicationDevice, Task<T>> action,
        bool requireConnected = true,
        ICommunicationDevice? device = null)
    {
        device ??= GetDevice(requireConnected);
        try
        {
            return await action(device);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw await CreateFailureAsync(operation, target, ex);
        }
    }

    protected async Task<InvalidOperationException> CreateFailureAsync(
        string operation,
        string? target,
        Exception? inner = null,
        string? detail = null)
    {
        var location = string.IsNullOrWhiteSpace(target) ? string.Empty : $" {target}";
        var reason = detail ?? (inner is OperationCanceledException
            ? "操作超时，请检查设备与网络"
            : inner?.Message ?? "未知错误");
        var message = $"新通讯{operation}失败 [{DeviceName.Trim()}]{location}：{reason}";
        await NodeLog.ErrorAsync(message);
        return new InvalidOperationException(message, inner);
    }
}
