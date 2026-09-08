using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace CommunicationTestApp.Services;

/// <summary>
/// 黑白主题切换服务。
/// 调用 PaletteHelper.SetTheme 切换 MaterialDesign 基础主题，
/// 并把 MD 的中性色/主色同步写入 Theme.xaml 中的别名画刷实例
/// （未冻结的 SolidColorBrush），使所有 StaticResource 引用即时跟随。
/// </summary>
public sealed class ThemeService
{
    private readonly PaletteHelper _palette = new();

    /// <summary>当前是否深色主题。</summary>
    public bool IsDarkTheme { get; private set; } = true;

    public event EventHandler? ThemeChanged;

    /// <summary>应用启动时调用：App.xaml 的 BundledTheme 初始为深色，同步别名画刷。</summary>
    public void Initialize()
    {
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

        var theme = _palette.GetTheme();
        theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);
        _palette.SetTheme(theme);

        SyncAliasBrushes(theme, dark);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 启动时（CreateShell 之前）注入别名画刷。
    /// BAML 编译的资源字典会把 Freezable 冻结（po:Freeze="False" 也无法阻止），
    /// 因此 Brush.* 必须由代码创建为未冻结实例，ThemeService 才能运行时改 Color。
    /// </summary>
    public static void EnsureAliasBrushes()
    {
        var res = Application.Current.Resources;
        if (res.Contains("Brush.Bg.0"))
        {
            return;
        }

        // 中性色（深色初始值，启动后由 Initialize 同步为 MD 实际值）
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

    /// <summary>把 MD 主题的中性色/主色写入 Theme.xaml 别名画刷实例。</summary>
    private static void SyncAliasBrushes(Theme theme, bool dark)
    {
        // 中性色：窗口/表面/边框/文字
        SetBrush("Brush.Bg.0", theme.Background);
        SetBrush("Brush.Bg.1", dark ? Lift(theme.Background, 0.06) : Lift(theme.Background, -0.03));
        SetBrush("Brush.Bg.2", dark ? Lift(theme.Background, 0.03) : Lift(theme.Background, -0.015));
        SetBrush("Brush.Bg.3", dark ? Lift(theme.Background, 0.09) : Lift(theme.Background, -0.05));
        SetBrush("Brush.Border.Subtle", dark ? Lift(theme.Background, 0.12) : Darken(theme.Background, 0.86));
        SetBrush("Brush.Border.Default", dark ? Lift(theme.Background, 0.18) : Darken(theme.Background, 0.72));
        SetBrush("Brush.Fg.Primary", theme.Foreground);
        SetBrush("Brush.Fg.Secondary", dark ? Lift(theme.Background, 0.62) : Darken(theme.Background, 0.45));
        SetBrush("Brush.Fg.Muted", dark ? Lift(theme.Background, 0.46) : Darken(theme.Background, 0.62));

        // 强调色：跟随 MD Primary
        var primary = theme.PrimaryMid.Color;
        SetBrush("Brush.Accent", primary);
        SetBrush("Brush.Accent.Hover", dark ? Lift(primary, 0.12) : Darken(primary, 0.88));
        SetBrush("Brush.Accent.Soft", WithAlpha(primary, 0x1F));

        // 语义色（深浅主题下保持可辨识）
        var success = Color.FromRgb(0x22, 0xC5, 0x5E);
        var warning = Color.FromRgb(dark ? (byte)0xF5 : (byte)0xC7, 0x9E, dark ? (byte)0x0B : (byte)0x8A);
        var danger = Color.FromRgb(0xEF, 0x44, 0x44);
        var info = dark ? Color.FromRgb(0x38, 0xBD, 0xF8) : Color.FromRgb(0x02, 0x86, 0xC4);
        SetBrush("Brush.Success", success);
        SetBrush("Brush.Success.Soft", WithAlpha(success, 0x1F));
        SetBrush("Brush.Warning", warning);
        SetBrush("Brush.Warning.Soft", WithAlpha(warning, 0x1F));
        SetBrush("Brush.Danger", danger);
        SetBrush("Brush.Danger.Soft", WithAlpha(danger, 0x1F));
        SetBrush("Brush.Info", info);
        SetBrush("Brush.Info.Soft", WithAlpha(info, 0x1F));
    }

    /// <summary>修改别名片刷实例的 Color（画刷由代码创建、未冻结，所有引用即时生效）。</summary>
    private static void SetBrush(string key, Color color)
    {
        if (Application.Current?.Resources[key] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
        }
    }

    /// <summary>向白色方向提升亮度。</summary>
    private static Color Lift(Color color, double ratio)
    {
        return Color.FromRgb(
            (byte)(color.R + (255 - color.R) * ratio),
            (byte)(color.G + (255 - color.G) * ratio),
            (byte)(color.B + (255 - color.B) * ratio));
    }

    /// <summary>向黑色方向压暗（ratio 为保留亮度比例）。</summary>
    private static Color Darken(Color color, double ratio)
    {
        return Color.FromRgb(
            (byte)(color.R * ratio),
            (byte)(color.G * ratio),
            (byte)(color.B * ratio));
    }

    private static Color WithAlpha(Color color, byte alpha) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);
}
