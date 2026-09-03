using System.Collections.Concurrent;
using CommunicationNew.Core;

namespace CommunicationNew.Composition;

/// <summary>
/// 基于参数类型的设备注册表。
/// 新协议通过注册参数工厂接入，不需要修改核心枚举 switch。
/// </summary>
public sealed class CommunicationDeviceRegistry : ICommunicationDeviceFactory
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
