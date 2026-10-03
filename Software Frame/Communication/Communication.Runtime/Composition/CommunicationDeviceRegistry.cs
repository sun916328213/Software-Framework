// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：CommunicationDeviceRegistry。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.Concurrent、Communication.Core、Communication.Runtime.Core。导入不代表所有类型都会在运行时加载。
using System.Collections.Concurrent;
using Communication.Core;
using Communication.Runtime.Core;

namespace Communication.Runtime.Composition;

/// <summary>
/// 基于参数类型的设备注册表。
/// 新协议通过注册参数工厂接入，不需要修改核心枚举 switch。
/// </summary>
#pragma warning disable CS0618 // 注册表同时实现旧接口，以维持迁移期的二进制组合入口。
public sealed class CommunicationDeviceRegistry : Communication.Core.ICommunicationDeviceFactory
#pragma warning restore CS0618
{
    private readonly ConcurrentDictionary<Type, Func<CommunicationParam, ICommunicationDevice>> _factories = new();
    private Func<CommunicationParam, ICommunicationDevice>? _fallback;

    /// <summary>注册一种参数类型对应的设备工厂。</summary>
    public CommunicationDeviceRegistry Register<TParameter>(
        Func<TParameter, ICommunicationDevice> factory)
        where TParameter : CommunicationParam
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (!_factories.TryAdd(typeof(TParameter), parameter => factory((TParameter)parameter)))
        {
            throw new InvalidOperationException($"参数类型 {typeof(TParameter).Name} 已注册。");
        }

        return this;
    }

    /// <summary>设置未注册参数类型的兼容工厂。</summary>
    public CommunicationDeviceRegistry SetFallback(
        Func<CommunicationParam, ICommunicationDevice> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _fallback = factory;
        return this;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 创建当前实现提供的对象或结果；对应入口为 Create。
    /// 正文中的关键协作调用包括 ThrowIfNull、TryGetValue、GetType、factory；具体条件和先后次序见方法体。
    /// </remarks>
    public ICommunicationDevice Create(CommunicationParam parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        if (_factories.TryGetValue(parameter.GetType(), out var factory))
        {
            return factory(parameter);
        }

        return _fallback?.Invoke(parameter) ??
            throw new NotSupportedException($"未注册通信参数类型 {parameter.GetType().FullName}。");
    }
}
