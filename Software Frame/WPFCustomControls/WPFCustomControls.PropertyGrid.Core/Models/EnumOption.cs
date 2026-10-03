// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：EnumOption。成员说明可结合下方具体实现阅读。
namespace WPFCustomControls.PropertyGrid.Core.Models;

/// <summary>枚举下拉选项（值与显示名分离，显示名可来自枚举成员的 [Description]）。</summary>
public sealed record EnumOption(object? Value, string DisplayName)
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ToString。
    /// </summary>
    public override string ToString() => DisplayName;
}