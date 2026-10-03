// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：BindingProxy。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、System.Windows。导入不代表所有类型都会在运行时加载。
using Device.Controls;
using System.Windows;

namespace Device.Controls.Common;

/// <summary>
/// DataContext 代理：DataGrid 列对象不在可视树里，RelativeSource/ElementName 都取不到页面 VM，
/// 借 Freezable 可继承 DataContext 的特性把 VM 递进列属性绑定（如 DataGridComboBoxColumn.ItemsSource）。
/// </summary>
public sealed class BindingProxy : Freezable
{
    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateInstanceCore。
    /// </summary>
    protected override Freezable CreateInstanceCore() => new BindingProxy();

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new PropertyMetadata(null));

    /// <summary>通常是所在 UserControl 的 DataContext（页面 VM）。</summary>
    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }
}
