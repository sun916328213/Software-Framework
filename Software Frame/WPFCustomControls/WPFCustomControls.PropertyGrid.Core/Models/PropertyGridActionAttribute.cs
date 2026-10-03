// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：PropertyGridActionAttribute、PropertyGridActionItem。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows.Input。导入不代表所有类型都会在运行时加载。
using System.Windows.Input;

namespace WPFCustomControls.PropertyGrid.Core.Models;

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

    /// <summary>
    /// 初始化 PropertyGridActionAttribute 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public PropertyGridActionAttribute(string displayName)
    {
        DisplayName = displayName;
    }
}

/// <summary>属性面板扩展按钮数据项（供 ItemsControl 绑定）。</summary>
public record PropertyGridActionItem(string DisplayName, ICommand Command, int Order);
