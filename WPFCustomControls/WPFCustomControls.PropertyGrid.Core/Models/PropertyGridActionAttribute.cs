using System.Windows.Input;

namespace WPFCustomControls.PropertyGrid.Models;

/// <summary>
/// 标记可执行命令属性为属性面板底部扩展按钮。
/// PropertyGridHost 扫描目标对象上带此特性的 <see cref="ICommand"/> 属性并自动渲染按钮，
/// 控件库通过特性解耦，不依赖具体业务命令名称。
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public class PropertyGridActionAttribute : Attribute
{
    /// <summary>按钮显示名称。</summary>
    public string DisplayName { get; }

    /// <summary>排序权重（越小越靠前），默认 0。</summary>
    public int Order { get; set; } = 0;

    public PropertyGridActionAttribute(string displayName)
    {
        DisplayName = displayName;
    }
}

/// <summary>属性面板扩展按钮数据项（供 ItemsControl 绑定）。</summary>
public record PropertyGridActionItem(string DisplayName, ICommand Command, int Order);
