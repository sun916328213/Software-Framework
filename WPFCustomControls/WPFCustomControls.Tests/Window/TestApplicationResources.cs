using System;
using System.Windows;
using System.Diagnostics;

using SW = System.Windows;

namespace WPFCustomControls.Tests.Window;

/// <summary>
/// 为 WPF Window 派生类的单元测试提供必要的合并资源。
///
/// <para>
/// 主项目 <c>WPFCustomControls</c> 的窗口 XAML 引用了
/// <c>Color_White</c>、<c>MaterialDesign.Brush.Primary</c>、
/// <c>DialogWindowStyle</c> 等资源 — 这些在生产环境由消费方应用合并
/// <c>Resources/Resources.xaml</c> 加载。
/// </para>
///
/// <para>
/// 测试 exe 不是 ApplicationDefinition,WPF 的 pack:// BAML 解析在加载
/// <c>MaterialDesignThemes.Wpf</c> 时其内部 <c>StaticResource</c> 解析失败;
/// 因此测试用 <c>Resources/MaterialDesign.Stubs.xaml</c> 提供 Window 构造期
/// 所需键的最小占位。测试只验证 DP / 属性,样式细节无影响。
/// </para>
/// </summary>
internal static class TestApplicationResources
{
    private static int _initialized;

    /// <summary>
    /// 在当前 STA 线程上确保 <see cref="Application.Current"/> 存在且
    /// 合并资源就绪(幂等)。
    /// </summary>
    public static void EnsureInitialized()
    {
        // 1) Application 单例 — WPF 要求 STA 线程上必须有 Current。
        if (Application.Current == null)
        {
            new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        }

        // 2) 同一进程内只合并一次。
        if (System.Threading.Interlocked.Exchange(ref _initialized, 1) == 1)
            return;

        var app = Application.Current;
        var resources = app!.Resources;

        // 3) 主项目 ThemeColors.xaml(Color_* 系列)
        TryAdd(resources,
            new Uri("/WPFCustomControls;component/Resources/ThemeColors.xaml", UriKind.Relative),
            "ThemeColors");

        // 4) 测试本地 MD stubs(Brush.Primary / Headline TextBlock / Button / Separator / ProgressBar 占位)
        TryAdd(resources,
            new Uri("/WPFCustomControls.Tests;component/Resources/MaterialDesign.Stubs.xaml", UriKind.Relative),
            "MaterialDesign.Stubs");
    }

    private static void TryAdd(ResourceDictionary target, Uri uri, string label)
    {
        try
        {
            target.MergedDictionaries.Add(new ResourceDictionary { Source = uri });
            Debug.WriteLine($"[TestResources] {label} loaded.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TestResources] {label} FAILED: {ex.Message}");
        }
    }
}