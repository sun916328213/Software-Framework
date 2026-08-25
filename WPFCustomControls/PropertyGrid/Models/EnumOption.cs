namespace WPFCustomControls.PropertyGrid.Models;

/// <summary>枚举下拉选项（值与显示名分离，显示名可来自枚举成员的 [Description]）。</summary>
public sealed record EnumOption(object? Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}
