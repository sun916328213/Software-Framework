using System.IO;
using System.Windows;
using Vision.Core.Engine;
using Extension.UI.Theme;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using Vision.Controls;
using WPFCustomControls.Workbench;

namespace Vision.Workbench;

/// <summary>独立视觉流程宿主，只启动、注册模块并导航Controls页面。</summary>
public partial class App : PrismApplication
{
    private WorkbenchLifetime? _lifetime;
    private VisionEngineSelection? _engineSelection;
    private IAsyncDisposable? _engineRegistration;
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            _engineSelection = VisionEngineConfiguration.Load(Path.Combine(AppContext.BaseDirectory, VisionEngineConfiguration.FileName));
            ThemeService.EnsureAliasBrushes();
            base.OnStartup(e);
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "视觉工作台启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1); // 不忽略错误配置，不切回默认后端。
        }
    }
    protected override Window CreateShell() => Container.Resolve<MainWindow>();
    protected override void RegisterTypes(IContainerRegistry registry)
    {
        registry.RegisterSingleton<ThemeService>();
        registry.Register<ViewModels.MainWindowViewModel>();
        registry.RegisterSingleton<MainWindow>();
        _engineRegistration = registry.RegisterVisionEngine(_engineSelection
            ?? throw new InvalidOperationException("启动配置尚未读取。"));
        registry.RegisterVisionFlow(VisionCatalogFactory.CreateCatalog);
    }
    protected override void ConfigureModuleCatalog(IModuleCatalog catalog)
    {
        catalog.AddModule<WPFCustomControls.WPFCustomControlsModule>();
        catalog.AddModule<VisionControlsModule>();
        catalog.AddModule<FlowEditor.Controls.FlowEditorModule>();
    }
    protected override void OnInitialized()
    {
        Container.Resolve<ThemeService>().Initialize();
        base.OnInitialized();
        _lifetime = new WorkbenchLifetime(MainWindow,
            () => { (MainWindow.DataContext as IDisposable)?.Dispose(); return ValueTask.CompletedTask; },
            () => WorkbenchLifetime.CloseRegionAsync(Container.Resolve<IRegionManager>(), ViewModels.MainWindowViewModel.MainRegion),
            () => _engineRegistration?.DisposeAsync() ?? ValueTask.CompletedTask);
        (MainWindow.DataContext as ViewModels.MainWindowViewModel)?.SelectFirst();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        if (_lifetime != null) _ = _lifetime.BeginCleanup();
        else if (_engineRegistration != null) _ = _engineRegistration.DisposeAsync();
        base.OnExit(e);
    }
}
