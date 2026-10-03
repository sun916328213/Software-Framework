// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：ThemePickerViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、WPFCustomControls.Controls、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、Extension.UI.Theme、MaterialDesignColors。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using WPFCustomControls.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Extension.UI.Theme;
using MaterialDesignColors;
using WPFCustomControls.ValueConvert;

namespace WPFCustomControls.Controls.ViewModels;

/// <summary>
/// 主题选择控件的视图模型：主色 + 辅色 + 浅色/深色。
/// <para>
/// 与框架的唯一耦合点是一个 <see cref="ThemeService"/> 实例。宿主应当注入自己
/// DI 容器里的那个单例（见 <c>ThemePickerView.Theme</c> 依赖属性），
/// 这样「改一处、全应用换肤」——所有消费方共享同一个 BundledTheme 实例；
/// 宿主没接 DI 时控件会自建一个兜底实例，至少能独立工作（读写同一份 Config/Theme.json）。
/// </para>
/// 换肤逻辑一律委托给 <see cref="ThemeService"/>，本视图模型不持有任何配色常量。
/// </summary>
public partial class ThemePickerViewModel : ObservableObject
{
    private ThemeService? _theme;

    /// <summary>防重入：ThemeService 回灌选中项时，不要再回头调一次 SetTheme 造成乒乓。</summary>
    private bool _pulling;

    [ObservableProperty]
    private PrimaryColor selectedPrimary;

    [ObservableProperty]
    private SecondaryColor selectedSecondary;

    /// <summary>
    /// 可选主色（MaterialDesign 枚举自由切换）。
    /// 只保留能取到色板色的值 —— 枚举里还有 <c>Inherit</c> 这种「跟随上层」的占位值，
    /// 它没有颜色、画不出预览，给用户选只会得到一块空白。
    /// </summary>
    public IReadOnlyList<PrimaryColor> PrimaryColors { get; } = Selectable<PrimaryColor>();

    /// <summary>可选辅色（同上，过滤掉没有颜色的占位值）。</summary>
    public IReadOnlyList<SecondaryColor> SecondaryColors { get; } = Selectable<SecondaryColor>();

    /// <summary>当前是否深色主题（「浅色 / 深色」两个按钮靠它决定谁高亮）。</summary>
    public bool IsDark => _theme?.IsDark ?? false;

    /// <summary>
    /// 主题服务。赋 null 时自建一个默认实例（<see cref="ThemeService()"/>，读同一份
    /// Config/Theme.json），使控件放进任意窗口都能独立运行，不必先接 DI。
    /// </summary>
    public ThemeService? Theme
    {
        get => _theme;
        set
        {
            // 注意：不能写成 ReferenceEquals(_theme, value) —— 初次赋值时两者都是 null，
            // 会误判成「没变化」而跳过兜底实例的创建。
            if (_theme is not null && ReferenceEquals(_theme, value))
                return;

            if (_theme is not null)
                _theme.ThemeChanged -= OnThemeChanged;

            _theme = value ?? new ThemeService();
            _theme.ThemeChanged += OnThemeChanged;

            PullFromTheme();
        }
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnSelectedPrimaryChanged。
    /// 正文中的关键协作调用包括 SetTheme；具体条件和先后次序见方法体。
    /// </summary>
    partial void OnSelectedPrimaryChanged(PrimaryColor value)
    {
        // 回灌期间（_pulling）跳过：那是 ThemeService 通知的结果，不是用户操作。
        if (_pulling || _theme is null)
            return;

        _theme.SetTheme(_theme.IsDark, value, SelectedSecondary);
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnSelectedSecondaryChanged。
    /// 正文中的关键协作调用包括 SetTheme；具体条件和先后次序见方法体。
    /// </summary>
    partial void OnSelectedSecondaryChanged(SecondaryColor value)
    {
        if (_pulling || _theme is null)
            return;

        _theme.SetTheme(_theme.IsDark, SelectedPrimary, value);
    }

    /// <summary>切到浅色主题（主色 / 辅色保持不变）。</summary>
    [RelayCommand]
    private void SetLightTheme() => ApplyDark(false);

    /// <summary>切到深色主题（主色 / 辅色保持不变）。</summary>
    [RelayCommand]
    private void SetDarkTheme() => ApplyDark(true);

    /// <summary>
    /// 只改黑白，主色/辅色取服务当前值（不取 Selected*，避免把界面上可能滞后的选中值又推回去）。
    /// 已经是目标态就什么都不做，省掉一次多余的落盘。
    /// </summary>
    private void ApplyDark(bool dark)
    {
        if (_theme is null || _theme.IsDark == dark)
            return;

        _theme.SetTheme(dark, _theme.Primary, _theme.Secondary);
    }

    /// <summary>主题在别处被改（如宿主/测试直调 ThemeService）时，把选中项与黑白态拉回一致。</summary>
    private void OnThemeChanged(object? sender, EventArgs e) => PullFromTheme();

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 PullFromTheme。
    /// 正文中的关键协作调用包括 OnPropertyChanged、nameof；具体条件和先后次序见方法体。
    /// </summary>
    private void PullFromTheme()
    {
        _pulling = true;
        try
        {
            SelectedPrimary = _theme!.Primary;
            SelectedSecondary = _theme.Secondary;
        }
        finally
        {
            _pulling = false;
        }

        OnPropertyChanged(nameof(IsDark));
    }

    /// <summary>枚举里能取到色板色的值（复用转换器的取色逻辑，保证「列出来的」都能画出预览）。</summary>
    private static IReadOnlyList<T> Selectable<T>() where T : struct, Enum =>
        Enum.GetValues<T>()
            .Where(v => ThemeColorToBrushConverter.Resolve(v) is not null)
            .ToArray();
}
