using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace Communication.Workbench.Services;

/// <summary>
/// 黑白主题切换服务。
/// 说明：Prism 没有换肤功能，主题切换是 MaterialDesign 的职责。
/// MaterialDesign 5.x 的正解是直接改 <see cref="BundledTheme"/>.BaseTheme；
/// 之前用的 PaletteHelper 是旧 API，与 BundledTheme 混用会导致切换无效果，
/// 因此改用 BundledTheme，并把深浅两套颜色写死，不再从旧 API 取色。
/// 自家别名画刷（Brush.*）切换时直接替换实例，DynamicResource 即时生效。
/// </summary>
public sealed class ThemeService
{
    /// <summary>主题引擎版本标记（启动日志自证，防止旧 exe 误报）。</summary>
    public const string EngineTag = "v3-BundledTheme-Replace";

    private BundledTheme? _bundledTheme;

    /// <summary>当前是否深色主题。</summary>
    public bool IsDarkTheme { get; private set; } = true;

    /// <summary>最近一次切换的诊断信息：为 null 表示 MD 与别名画刷都已同步。</summary>
    public string? LastError { get; private set; }

    public event EventHandler? ThemeChanged;

    /// <summary>应用启动时调用：找到 App.xaml 中的 BundledTheme，同步深色初始值。</summary>
    public void Initialize()
    {
        _bundledTheme = FindBundledTheme();
        Apply(IsDarkTheme);
    }

    /// <summary>在深色/浅色之间切换。</summary>
    public void Toggle()
    {
        Apply(!IsDarkTheme);
    }

    private void Apply(bool dark)
    {
        IsDarkTheme = dark;

        // MaterialDesign 5.x：改 BundledTheme.BaseTheme 即全局换肤。
        var bundled = _bundledTheme ??= FindBundledTheme();
        var expected = dark ? BaseTheme.Dark : BaseTheme.Light;
        if (bundled is null)
        {
            LastError = "未找到 BundledTheme，MD 控件未换肤（别名画刷已切换）";
        }
        else
        {
            bundled.BaseTheme = expected;
            LastError = bundled.BaseTheme != expected
                ? $"BundledTheme 回读为 {bundled.BaseTheme}，MD 控件可能未换肤"
                : null;
        }

        SyncAliasBrushes(dark);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static BundledTheme? FindBundledTheme()
    {
        var app = Application.Current;
        if (app is null)
        {
            return null;
        }

        foreach (var dict in app.Resources.MergedDictionaries)
        {
            if (dict is BundledTheme bundled)
            {
                return bundled;
            }
            foreach (var inner in dict.MergedDictionaries)
            {
                if (inner is BundledTheme innerBundled)
                {
                    return innerBundled;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 启动时（CreateShell 之前）注入别名画刷。
    /// BAML 编译的资源字典会把 Freezable 冻结，因此 Brush.* 必须由代码创建为未冻结实例，
    /// ThemeService 才能运行时改 Color。
    /// </summary>
    public static void EnsureAliasBrushes()
    {
        var res = Application.Current.Resources;
        if (res.Contains("Brush.Bg.0"))
        {
            return;
        }

        // 中性色（深色初始值，启动后由 Initialize 同步）
        res["Brush.Bg.0"] = NewBrush(0xFF1E1E1E);
        res["Brush.Bg.1"] = NewBrush(0xFF2D2D2D);
        res["Brush.Bg.2"] = NewBrush(0xFF262626);
        res["Brush.Bg.3"] = NewBrush(0xFF333333);
        res["Brush.Border.Subtle"] = NewBrush(0xFF3D3D3D);
        res["Brush.Border.Default"] = NewBrush(0xFF4E4E4E);
        res["Brush.Fg.Primary"] = NewBrush(0xFFF2F2F2);
        res["Brush.Fg.Secondary"] = NewBrush(0xFFB2B2B2);
        res["Brush.Fg.Muted"] = NewBrush(0xFF8A8A8A);

        // 强调色 / 语义色
        res["Brush.Accent"] = NewBrush(0xFF3F51B5);
        res["Brush.Accent.Hover"] = NewBrush(0xFF5C6BC0);
        res["Brush.Accent.Soft"] = NewBrush(0x1F3F51B5);
        res["Brush.Success"] = NewBrush(0xFF22C55E);
        res["Brush.Success.Soft"] = NewBrush(0x1F22C55E);
        res["Brush.Warning"] = NewBrush(0xFFF59E0B);
        res["Brush.Warning.Soft"] = NewBrush(0x1FF59E0B);
        res["Brush.Danger"] = NewBrush(0xFFEF4444);
        res["Brush.Danger.Soft"] = NewBrush(0x1FEF4444);
        res["Brush.Info"] = NewBrush(0xFF38BDF8);
        res["Brush.Info.Soft"] = NewBrush(0x1F38BDF8);
    }

    private static SolidColorBrush NewBrush(uint argb) =>
        new(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));

    /// <summary>按深浅两套固定色同步别名画刷（不再依赖旧调色板 API，保证可预测）。</summary>
    private static void SyncAliasBrushes(bool dark)
    {
        // 中性色：窗口/表面/边框/文字
        SetBrush("Brush.Bg.0", dark ? Rgb(0x1E, 0x1E, 0x1E) : Rgb(0xFA, 0xFA, 0xFA));
        SetBrush("Brush.Bg.1", dark ? Rgb(0x2D, 0x2D, 0x2D) : Rgb(0xFF, 0xFF, 0xFF));
        SetBrush("Brush.Bg.2", dark ? Rgb(0x26, 0x26, 0x26) : Rgb(0xF5, 0xF5, 0xF5));
        SetBrush("Brush.Bg.3", dark ? Rgb(0x33, 0x33, 0x33) : Rgb(0xEE, 0xEE, 0xEE));
        SetBrush("Brush.Border.Subtle", dark ? Rgb(0x3D, 0x3D, 0x3D) : Rgb(0xE0, 0xE0, 0xE0));
        SetBrush("Brush.Border.Default", dark ? Rgb(0x4E, 0x4E, 0x4E) : Rgb(0xBD, 0xBD, 0xBD));
        SetBrush("Brush.Fg.Primary", dark ? Rgb(0xF2, 0xF2, 0xF2) : Rgb(0x21, 0x21, 0x21));
        SetBrush("Brush.Fg.Secondary", dark ? Rgb(0xB2, 0xB2, 0xB2) : Rgb(0x61, 0x61, 0x61));
        SetBrush("Brush.Fg.Muted", dark ? Rgb(0x8A, 0x8A, 0x8A) : Rgb(0x9E, 0x9E, 0x9E));

        // 强调色：跟随 MD Primary（Indigo），深浅共用
        var primary = Rgb(0x3F, 0x51, 0xB5);
        SetBrush("Brush.Accent", primary);
        SetBrush("Brush.Accent.Hover", dark ? Rgb(0x5C, 0x6B, 0xC0) : Rgb(0x30, 0x3D, 0x8C));
        SetBrush("Brush.Accent.Soft", WithAlpha(primary, 0x1F));

        // 语义色（浅色下压暗，保证文字可读）
        var success = dark ? Rgb(0x22, 0xC5, 0x5E) : Rgb(0x16, 0x82, 0x3D);
        var warning = dark ? Rgb(0xF5, 0x9E, 0x0B) : Rgb(0xB4, 0x53, 0x09);
        var danger = Rgb(0xEF, 0x44, 0x44);
        var info = dark ? Rgb(0x38, 0xBD, 0xF8) : Rgb(0x02, 0x86, 0xC4);
        SetBrush("Brush.Success", success);
        SetBrush("Brush.Success.Soft", WithAlpha(success, 0x1F));
        SetBrush("Brush.Warning", warning);
        SetBrush("Brush.Warning.Soft", WithAlpha(warning, 0x1F));
        SetBrush("Brush.Danger", danger);
        SetBrush("Brush.Danger.Soft", WithAlpha(danger, 0x1F));
        SetBrush("Brush.Info", info);
        SetBrush("Brush.Info.Soft", WithAlpha(info, 0x1F));
    }

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    /// <summary>
    /// 用新画刷实例替换旧值。DynamicResource 遇到字典替换必重新求值，
    /// 即使旧画刷曾被冻结也能生效；改旧实例 Color 在被冻结时会静默失败。
    /// </summary>
    private static void SetBrush(string key, Color color)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.Resources[key] = new SolidColorBrush(color);
    }

    private static Color WithAlpha(Color color, byte alpha) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);
}
