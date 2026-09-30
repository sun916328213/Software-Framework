using System.Linq;
using System.Windows;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using ScriptEditor.Services;
using ScriptEditor.Views;

namespace ScriptEditor.Workbench;

/// <summary>
/// 脚本编辑器工作台入口（Prism 宿主）：只负责壳与模块组合，页面与服务都在 ScriptEditor.Controls。
/// </summary>
public partial class App : PrismApplication
{
    private readonly ScriptEditorModule _module = new();
    private bool _smoke;

    /// <summary>
    /// 壳窗口。这里就把 Content 装成 <see cref="ScriptManagerView"/>（而非等 OnInitialized 再补），
    /// 以免空壳先显示再填内容造成闪烁；为此 <see cref="RegisterTypes"/> 需提前触发模块注册。
    /// </summary>
    protected override Window CreateShell()
    {
        var window = new Window
        {
            Title = "ScriptEditor Workbench",
            Width = 1200,
            Height = 800,
            MinWidth = 640,
            MinHeight = 400,
            Content = Container.Resolve<ScriptManagerView>()
        };
        ScriptEditorView.SetEditorService(window, Container.Resolve<ScriptEditorService>());
        return window;
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // Prism 的正常顺序是「CreateShell → InitializeModules」，但壳要解析模块提供的
        // ScriptManagerView，故提前触发一次模块类型注册；RegisterTypes 内部有 IsRegistered
        // 幂等守卫，Prism 稍后再次调用不会重复注册。模块的 OnInitialized（资源合并 / 后台预热）
        // 仍由模块目录负责，恰好执行一次。
        _module.RegisterTypes(containerRegistry);
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        // 模块目录按类型登记；Prism 初始化模块时会另建实例再调一次 RegisterTypes（幂等，无副作用），
        // OnInitialized（资源合并 / 后台预热）由它负责，恰好执行一次。
        moduleCatalog.AddModule<ScriptEditorModule>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _smoke = e.Args.Contains("--smoke", StringComparer.OrdinalIgnoreCase);
        base.OnStartup(e);
    }

    protected override void OnInitialized()
    {
        base.OnInitialized(); // 壳与模块（含资源合并）此时均已就绪

        if (_smoke && MainWindow is not null)
            MainWindow.Loaded += (_, _) => MainWindow.Close();
    }
}