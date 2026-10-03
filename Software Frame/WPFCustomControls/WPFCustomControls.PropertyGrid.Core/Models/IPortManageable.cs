// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：IPortManageable。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections。导入不代表所有类型都会在运行时加载。
using System.Collections;

namespace WPFCustomControls.PropertyGrid.Core.Models;

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