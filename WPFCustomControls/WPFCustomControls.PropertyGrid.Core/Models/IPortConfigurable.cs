namespace WPFCustomControls.PropertyGrid.Models;

/// <summary>
/// 目标对象实现此接口后，属性网格会自动为
/// PortConfigurable=true 的属性渲染内联端口开关（☑入 ☑出）。
/// 属性网格通过此接口解耦，不依赖具体业务类型。
/// </summary>
public interface IPortConfigurable
{
    /// <summary>获取指定属性的运行时端口标记。</summary>
    (bool IsInput, bool IsOutput) GetPortConfig(string propertyName);

    /// <summary>设置指定属性的运行时端口标记，会触发 ConfigChanged 事件。</summary>
    void SetPortConfig(string propertyName, bool isInput, bool isOutput);

    /// <summary>端口配置变更后触发，属性网格监听此事件同步 UI。</summary>
    event Action ConfigChanged;
}