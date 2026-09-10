using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using System.Windows;

namespace WPFCustomControls.Services;

/// <summary>
/// 全框架共享主题服务（官方极简做法）。
/// 单一真相源：只改 <see cref="BundledTheme"/> 这一个实例
/// （BaseTheme + PrimaryColor + SecondaryColor），MD 控件全局换肤。
/// 样式只引用 MD 原生键（MaterialDesign.Brush.*），不维护自有画刷体系。
/// </summary>
public class AppThemeService
{
    /// <summary>主题引擎版本标记（诊断用）。</summary>
    public const string EngineTag = "shared-v2-official-slim";

    private BundledTheme? _bundledTheme;

    /// <summary>当前是否深色主题。</summary>
    public bool IsDarkTheme { get; private set; }

    /// <summary>当前主色。</summary>
    public PrimaryColor Primary { get; private set; } = PrimaryColor.Indigo;

    /// <summary>当前辅色。</summary>
    public SecondaryColor Secondary { get; private set; } = SecondaryColor.Teal;

    /// <summary>最近一次切换的诊断信息：null 表示已同步。</summary>
    public string? LastError { get; private set; }

    public event EventHandler? ThemeChanged;

    /// <summary>应用启动时调用：找到 App.xaml 中的 BundledTheme 并应用初始值。</summary>
    public void Initialize(bool dark = false,
        PrimaryColor primary = PrimaryColor.Indigo,
        SecondaryColor secondary = SecondaryColor.Teal)
    {
        _bundledTheme = FindBundledTheme();
        Apply(dark, primary, secondary);
    }

    /// <summary>深浅切换（主色/辅色保持）。</summary>
    public void Toggle() => Apply(!IsDarkTheme, Primary, Secondary);

    /// <summary>自由切换：深浅 + 主色 + 辅色。</summary>
    public void SetTheme(bool dark, PrimaryColor primary, SecondaryColor secondary) =>
        Apply(dark, primary, secondary);

    private void Apply(bool dark, PrimaryColor primary, SecondaryColor secondary)
    {
        IsDarkTheme = dark;
        Primary = primary;
        Secondary = secondary;

        var bundled = _bundledTheme ??= FindBundledTheme();
        if (bundled is null)
        {
            LastError = "未找到 BundledTheme，MD 控件未换肤";
        }
        else
        {
            bundled.BaseTheme = dark ? BaseTheme.Dark : BaseTheme.Light;
            bundled.PrimaryColor = primary;
            bundled.SecondaryColor = secondary;
            LastError = null;
        }

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public static BundledTheme? FindBundledTheme()
    {
        var app = Application.Current;
        if (app is null)
            return null;

        foreach (var dict in app.Resources.MergedDictionaries)
        {
            if (dict is BundledTheme bundled)
                return bundled;
            foreach (var inner in dict.MergedDictionaries)
            {
                if (inner is BundledTheme innerBundled)
                    return innerBundled;
            }
        }
        return null;
    }
}
