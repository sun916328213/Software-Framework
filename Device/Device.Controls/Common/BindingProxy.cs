using System.Windows;

namespace Device.Controls.Common;

/// <summary>
/// DataContext 代理：DataGrid 列对象不在可视树里，RelativeSource/ElementName 都取不到页面 VM，
/// 借 Freezable 可继承 DataContext 的特性把 VM 递进列属性绑定（如 DataGridComboBoxColumn.ItemsSource）。
/// </summary>
public sealed class BindingProxy : Freezable
{
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
