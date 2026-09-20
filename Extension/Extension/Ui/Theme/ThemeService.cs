using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Extension.Core.Conversion;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;

namespace Extension.UI.Theme
{
    /// <summary>
    /// 全框架统一黑白主题服务（MaterialDesign 官方做法）。
    /// 单一真相源：只改 App.xaml 里那个 <see cref="BundledTheme"/> 实例
    /// （BaseTheme + PrimaryColor + SecondaryColor），MD 控件全局换肤。
    /// 配色基准：浅色以纯白 #FFFFFF 为基准逐级压暗，文字以纯黑为基准；
    /// 深色以纯黑 #000000 为基准逐级提亮，文字以纯白为基准。
    /// 自家别名画刷（Brush.*）切换时替换实例，DynamicResource 即时生效。
    /// 默认值 = App.xaml 里 BundledTheme 的当前值；用户改过的选择持久化到独立的
    /// <see cref="ConfigPath"/>（默认 程序目录/Config/Theme.json），
    /// 不再寄居在宿主的单文件多节配置里。
    /// </summary>
    public class ThemeService
    {
        /// <summary>主题引擎版本标记（启动日志自证，防止旧 exe 误报）。</summary>
        public const string EngineTag = "v4-white-black-base";

        /// <summary>
        /// 主题持久化文件。默认 程序目录/Config/Theme.json（约定默认值），
        /// 宿主可在构造 <see cref="ThemeService"/> 之前改写为自有路径。
        /// </summary>
        public static string ConfigPath { get; set; } =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Theme.json");

        private BundledTheme? _bundledTheme;

        /// <summary>
        /// 无参构造：读持久化配置，无配置时用 App.xaml 里 BundledTheme 的当前值。
        /// 拿不到 BundledTheme 时回退浅色（白基准）。
        /// </summary>
        public ThemeService() : this(defaultDark: false)
        {
        }

        /// <summary>
        /// 无持久化配置时的回退深浅（DataManager 传 false，Communication 传 true）。
        /// 只在既没有持久化配置、又找不到 App.xaml 里的 BundledTheme 时才用得上。
        /// </summary>
        public ThemeService(bool defaultDark)
        {
            // 默认值以宿主 App.xaml 的 BundledTheme 为准 —— 那是主题的唯一真相源。
            // 曾经把 Indigo/Teal 写死在这里，导致 App.xaml 设的 Teal/Amber 首次启动就被顶掉。
            var bundled = FindBundledTheme();
            var saved = JsonEx.TryRead<ThemeSettings>(ConfigPath);

            IsDark = saved?.IsDark
                ?? (bundled is null ? defaultDark : bundled.BaseTheme == BaseTheme.Dark);
            Primary = ParsePrimary(saved?.Primary, bundled?.PrimaryColor ?? PrimaryColor.Indigo);
            Secondary = ParseSecondary(saved?.Secondary, bundled?.SecondaryColor ?? SecondaryColor.Teal);
        }

        /// <summary>当前是否深色主题。</summary>
        public bool IsDark { get; private set; }

        /// <summary>当前是否深色主题（Communication 旧命名，别名）。</summary>
        public bool IsDarkTheme => IsDark;

        /// <summary>当前主色。</summary>
        public PrimaryColor Primary { get; private set; }

        /// <summary>当前辅色。</summary>
        public SecondaryColor Secondary { get; private set; }

        /// <summary>最近一次切换的诊断信息：为 null 表示 MD 与别名画刷都已同步。</summary>
        public string? LastError { get; private set; }

        /// <summary>主题应用完成后触发。</summary>
        public event EventHandler? ThemeChanged;

        /// <summary>
        /// 启动时调用：把已持久化的主题应用到 BundledTheme 实例（含别名画刷）。
        /// 刻意不写回配置：没有配置时保持 App.xaml 的默认值不被固化，
        /// 这样之后改 App.xaml 的 BaseTheme/PrimaryColor/SecondaryColor 仍然生效。
        /// </summary>
        public void Initialize() => Apply(IsDark, Primary, Secondary, persist: false);

        /// <summary>启动时调用：直接指定初始值（供宿主一次性给定深浅 + 主色 + 辅色），同样不写回配置。</summary>
        public void Initialize(bool dark, PrimaryColor primary, SecondaryColor secondary) =>
            Apply(dark, primary, secondary, persist: false);

        /// <summary>深浅切换（主色/辅色保持不变），等价官方教程里改 BaseTheme 的那一步。</summary>
        public void Toggle() => SetTheme(!IsDark, Primary, Secondary);

        /// <summary>自由切换：深浅 + 主色 + 辅色，一次改完 BundledTheme 三个属性，并持久化。</summary>
        public void SetTheme(bool dark, PrimaryColor primary, SecondaryColor secondary) =>
            Apply(dark, primary, secondary, persist: true);

        private void Apply(bool dark, PrimaryColor primary, SecondaryColor secondary, bool persist)
        {
            IsDark = dark;
            Primary = primary;
            Secondary = secondary;

            var bundled = _bundledTheme ??= FindBundledTheme();
            var expected = dark ? BaseTheme.Dark : BaseTheme.Light;
            if (bundled is null)
            {
                LastError = "未找到 BundledTheme，MD 控件未换肤（别名画刷已切换）";
            }
            else
            {
                bundled.BaseTheme = expected;
                bundled.PrimaryColor = primary;
                bundled.SecondaryColor = secondary;
                LastError = bundled.BaseTheme != expected
                    ? $"BundledTheme 回读为 {bundled.BaseTheme}，MD 控件可能未换肤"
                    : null;
            }

            SyncAliasBrushes(dark);

            if (persist)
                JsonEx.Save(new ThemeSettings(dark, primary.ToString(), secondary.ToString()), ConfigPath);

            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        public static BundledTheme? FindBundledTheme()
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
        /// ThemeService 才能运行时替换。初始值为黑基准，启动后由 Initialize 同步。
        /// </summary>
        public static void EnsureAliasBrushes()
        {
            var app = Application.Current;
            if (app is null || app.Resources.Contains("Brush.Bg.0"))
            {
                return;
            }

            var res = app.Resources;
            // 黑基准初始值
            res["Brush.Bg.0"] = NewBrush(0xFF000000);
            res["Brush.Bg.1"] = NewBrush(0xFF0A0A0A);
            res["Brush.Bg.2"] = NewBrush(0xFF141414);
            res["Brush.Bg.3"] = NewBrush(0xFF1F1F1F);
            res["Brush.Border.Subtle"] = NewBrush(0xFF262626);
            res["Brush.Border.Default"] = NewBrush(0xFF404040);
            res["Brush.Fg.Primary"] = NewBrush(0xFFFFFFFF);
            res["Brush.Fg.Secondary"] = NewBrush(0xFFD4D4D4);
            res["Brush.Fg.Muted"] = NewBrush(0xFFA3A3A3);

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
            // Color 别名：给 DropShadowEffect.Color 这类只要 Color、不要 Brush 的属性用
            //（Brush 实例的字符串形式 "#FF…" 转不成 Color，直接引用会抛 XamlParseException）。
            res["Brush.Success.Color"] = NewColor(0xFF22C55E);
            res["Brush.Warning.Color"] = NewColor(0xFFF59E0B);
            res["Brush.Danger.Color"] = NewColor(0xFFEF4444);
            res["Brush.Info.Color"] = NewColor(0xFF38BDF8);
        }

        private static Color NewColor(uint argb) =>
            Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

        private static SolidColorBrush NewBrush(uint argb) =>
            new(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));

        /// <summary>
        /// 按深浅两套固定色同步别名画刷。
        /// 浅色以纯白为基准逐级压暗、文字以纯黑为基准；
        /// 深色以纯黑为基准逐级提亮、文字以纯白为基准。
        /// </summary>
        private static void SyncAliasBrushes(bool dark)
        {
            // 中性色：窗口/表面/边框/文字
            SetBrush("Brush.Bg.0", dark ? Rgb(0x00, 0x00, 0x00) : Rgb(0xFF, 0xFF, 0xFF));
            SetBrush("Brush.Bg.1", dark ? Rgb(0x0A, 0x0A, 0x0A) : Rgb(0xFF, 0xFF, 0xFF));
            SetBrush("Brush.Bg.2", dark ? Rgb(0x14, 0x14, 0x14) : Rgb(0xF7, 0xF7, 0xF7));
            SetBrush("Brush.Bg.3", dark ? Rgb(0x1F, 0x1F, 0x1F) : Rgb(0xED, 0xED, 0xED));
            SetBrush("Brush.Border.Subtle", dark ? Rgb(0x26, 0x26, 0x26) : Rgb(0xE5, 0xE5, 0xE5));
            SetBrush("Brush.Border.Default", dark ? Rgb(0x40, 0x40, 0x40) : Rgb(0xD4, 0xD4, 0xD4));
            SetBrush("Brush.Fg.Primary", dark ? Rgb(0xFF, 0xFF, 0xFF) : Rgb(0x00, 0x00, 0x00));
            SetBrush("Brush.Fg.Secondary", dark ? Rgb(0xD4, 0xD4, 0xD4) : Rgb(0x40, 0x40, 0x40));
            SetBrush("Brush.Fg.Muted", dark ? Rgb(0xA3, 0xA3, 0xA3) : Rgb(0x73, 0x73, 0x73));

            // 强调色：跟随 MD Primary（Indigo），深浅共用
            var primary = Rgb(0x3F, 0x51, 0xB5);
            SetBrush("Brush.Accent", primary);
            SetBrush("Brush.Accent.Hover", dark ? Rgb(0x5C, 0x6B, 0xC0) : Rgb(0x30, 0x3D, 0x8C));
            SetBrush("Brush.Accent.Soft", WithAlpha(primary, 0x1F));

            // 语义色：浅色压暗保证白底可读，深色提亮保证黑底可读
            var success = dark ? Rgb(0x22, 0xC5, 0x5E) : Rgb(0x15, 0x80, 0x3D);
            var warning = dark ? Rgb(0xF5, 0x9E, 0x0B) : Rgb(0xB4, 0x53, 0x09);
            var danger = dark ? Rgb(0xEF, 0x44, 0x44) : Rgb(0xDC, 0x26, 0x26);
            var info = dark ? Rgb(0x38, 0xBD, 0xF8) : Rgb(0x02, 0x86, 0xC4);
            SetBrush("Brush.Success", success);
            SetBrush("Brush.Success.Soft", WithAlpha(success, 0x1F));
            SetBrush("Brush.Warning", warning);
            SetBrush("Brush.Warning.Soft", WithAlpha(warning, 0x1F));
            SetBrush("Brush.Danger", danger);
            SetBrush("Brush.Danger.Soft", WithAlpha(danger, 0x1F));
            SetBrush("Brush.Info", info);
            SetBrush("Brush.Info.Soft", WithAlpha(info, 0x1F));
            // Color 别名与画刷同值同步，深浅切换时一起换。
            SetColor("Brush.Success.Color", success);
            SetColor("Brush.Warning.Color", warning);
            SetColor("Brush.Danger.Color", danger);
            SetColor("Brush.Info.Color", info);
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

        /// <summary>
        /// 用装箱后的 <see cref="Color"/> 结构体替换旧值（Color 不是 Freezable，不存在冻结问题）。
        /// </summary>
        private static void SetColor(string key, Color color)
        {
            if (Application.Current is null)
            {
                return;
            }

            Application.Current.Resources[key] = color;
        }

        private static Color WithAlpha(Color color, byte alpha) =>
            Color.FromArgb(alpha, color.R, color.G, color.B);

        private static PrimaryColor ParsePrimary(string? name, PrimaryColor fallback) =>
            Enum.TryParse<PrimaryColor>(name, out var v) ? v : fallback;

        private static SecondaryColor ParseSecondary(string? name, SecondaryColor fallback) =>
            Enum.TryParse<SecondaryColor>(name, out var v) ? v : fallback;
    }
}
