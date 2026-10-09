// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：NetDeviceNodeBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowNodes、Communication.Core、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、WPFCustomControls.PropertyGrid.Core.Models。导入不代表所有类型都会在运行时加载。
using FlowNodes;
using Communication.Core;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace FlowNodes.Communication;

/// <summary>
/// 已创建设备的操作节点基类：统一设备选择、在线检查、取消和错误记录语义。
/// </summary>
internal abstract class NetDeviceNodeBase : NodeModel
{
    /// <summary>
    /// 初始化 NetDeviceNodeBase 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
    /// </summary>
    protected NetDeviceNodeBase(ICommunicationManager manager)
    {
        Manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    protected ICommunicationManager Manager { get; }

    private string _deviceName = string.Empty;

    [NodeProperty(DisplayName = "设备名称", Group = "1.基础", Order = 0, Description = "已创建的设备名", ItemsSource = nameof(DeviceNames))]
    public string DeviceName
    {
        get => _deviceName;
        set => SetProperty(ref _deviceName, value);
    }

    /// <summary>管理器中已创建的设备名（下拉选项）。</summary>
    public List<string> DeviceNames => NetDeviceHelper.DeviceNames(Manager);

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetDevice。
    /// 正文中的关键协作调用包括 GetDeviceOrThrow、EnsureConnected；具体条件和先后次序见方法体。
    /// </summary>
    protected ICommunicationDevice GetDevice(bool requireConnected = true)
    {
        var device = NetDeviceHelper.GetDeviceOrThrow(Manager, DeviceName);
        if (requireConnected)
            NetDeviceHelper.EnsureConnected(device);
        return device;
    }

    /// <summary>
    /// 运行当前处理流程；对应入口为 RunDeviceOperationAsync。
    /// 正文中的关键协作调用包括 GetDevice、action、CreateFailureAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
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

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateFailureAsync。
    /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、Trim、ErrorAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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
