using System.Collections;

namespace WPFCustomControls.PropertyGrid.Models;

/// <summary>
/// 目标对象实现此接口后，属性网格会渲染"自定义端口"管理区。
/// 通过接口约束端口增删能力，替代魔法字符串反射，目标改名时编译期即可发现。
/// </summary>
public interface IPortManageable
{
    /// <summary>自定义输入端口集合。</summary>
    IEnumerable CustomInputPorts { get; }

    /// <summary>自定义输出端口集合。</summary>
    IEnumerable CustomOutputPorts { get; }

    /// <summary>添加自定义输入端口。</summary>
    void AddCustomInputPort(string name);

    /// <summary>添加自定义输出端口（可绑定属性）。</summary>
    void AddCustomOutputPort(string name, string? boundProperty = null);

    /// <summary>删除自定义端口。</summary>
    bool RemoveCustomPort(string portId);

    /// <summary>使端口缓存失效（端口变更后调用）。</summary>
    void InvalidatePorts();
}
