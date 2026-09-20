namespace Extension.UI.Theme
{
    /// <summary>
    /// 主题持久化模型（<see cref="ThemeService.ConfigPath"/> 对应文件的根对象）。
    /// 与 <see cref="ThemeService"/> 同放 ThemeEx，宿主需要时可直接构造它。
    /// </summary>
    /// <param name="IsDark">是否深色主题。</param>
    /// <param name="Primary">主色名（MaterialDesign <c>PrimaryColor</c> 枚举名）。</param>
    /// <param name="Secondary">辅色名（MaterialDesign <c>SecondaryColor</c> 枚举名）。</param>
    public sealed record ThemeSettings(
        bool IsDark = false,
        string Primary = "Teal",
        string Secondary = "Amber");
}
