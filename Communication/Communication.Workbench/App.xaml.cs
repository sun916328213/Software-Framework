using Prism.DryIoc;
using System.Windows;

namespace Communication.Workbench;

public partial class App : PrismApplication
{
    protected override Window CreateShell()
    {
        return Container.Resolve<Views.MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // 服务注册
        containerRegistry.RegisterSingleton<Services.CommunicationSessionService>();
        containerRegistry.RegisterSingleton<Services.ILogService, Services.LogService>();
        containerRegistry.RegisterSingleton<Services.ThemeService>();

        // 对话框
        containerRegistry.RegisterDialog<Views.DeviceConfigDialog, ViewModels.DeviceConfigDialogViewModel>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // 必须在 shell（及任何视图）创建前注入别名画刷：
        // BAML 编译的资源字典会冻结 Freezable，改由代码创建可变实例
        Services.ThemeService.EnsureAliasBrushes();

        base.OnStartup(e);
    }

    protected override void OnInitialized()
    {
        // 同步别名画刷与 MaterialDesign 深色基础主题
        Container.Resolve<Services.ThemeService>().Initialize();
        base.OnInitialized();
    }
}
