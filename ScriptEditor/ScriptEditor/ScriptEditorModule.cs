using Extension.Core.Reflection;
using Prism.Ioc;
using Prism.Modularity;
using ScriptEditor.Services;
using ScriptEditor.ViewModels;
using ScriptEditor.Views;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace ScriptEditor;

/// <summary>
/// ScriptEditor 的 Prism 模块类。
/// 负责注册脚本编辑器相关的视图和服务。
/// </summary>
public class ScriptEditorModule : IModule
{
    /// <summary>
    /// 后台预热任务（RoslynHost + 框架引用扫描），供 App 层在 UI 交互前等待完成。
    /// </summary>
    public static Task? WarmupTask { get; private set; }

    /// <summary>模块初始化完成后调用。</summary>
    public void OnInitialized(IContainerProvider containerProvider)
    {
        MergeResourceDictionaries();
        // 后台预热 RoslynHost 及框架引用扫描，避免用户首次新建脚本时才触发初始化造成卡顿。
        // CustomRoslynHost 构建约 500ms，框架 DLL 扫描约 1700ms，合计约 2.2s。
        // 返回 Task 供 App 层等待，确保 UI 线程首次访问 ScriptHostProvider.Shared 时
        // Lazy.Value 已完成初始化，不会阻塞 UI 线程。
        WarmupTask = Task.Run(() =>
        {
            _ = ScriptHostProvider.Shared;
            _ = CustomRoslynHost.GetAdditionalDefaultReferences();
        });
    }

    /// <summary>注册模块的类型。</summary>
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        //// 批量注册导航视图（ScriptEditor.Views 命名空间下的 UserControl，单例 + (object, 导航名) 单例）。
        //// ScriptManagerView 除外：在 Packing 中启动/关闭是两个独立流程，注册为单例会共享同一视图实例，
        //// 因此单独注册为瞬时（见下方），保证每次解析都是新实例。
        //containerRegistry.RegisterNavigationViewsFromNamespace(
        //    Assembly.GetExecutingAssembly(),
        //    "ScriptEditor.Views",
        //    t => t != typeof(ScriptManagerView));

        //// 显式 IOC 注册服务（服务数量固定、生命周期各不相同，比反射批量注册更清晰）。
        //// FileService：文件对话框，无状态，单例共享。
        //// ScriptEditorService：全局引用管理 + RoslynHost，重量级且全局共享，保持单例。
        //// ScriptManagerService：瞬时，Packing 启动/关闭流程各自持有独立脚本状态。
        //// 其余类型（CustomRoslynHost / ScriptEngine / 补全提供器）均在内部 new 创建，无需注册。
        //containerRegistry.RegisterSingleton<FileService>();
        //containerRegistry.RegisterSingleton<ScriptEditorService>();
        //containerRegistry.Register<ScriptManagerService>();

        //// 注册 ViewModel（瞬时模式，每次 Resolve 创建新实例，供多个 Flow 界面独立使用）
        //containerRegistry.Register<ScriptManagerViewModel>();

        //// ScriptManagerView 注册为瞬时，使 Packing 启动/关闭流程各自持有独立的视图实例。
        //// 同时保留 (object, 导航名) 导航注册，EditorDemo 等按导航名 RequestNavigate 时每次解析新实例。
        //containerRegistry.Register<ScriptManagerView>();
        //containerRegistry.Register(typeof(object), typeof(ScriptManagerView), nameof(ScriptManagerView));
    }

    /// <summary>合并 ScriptEditor 所需的资源字典。</summary>
    private void MergeResourceDictionaries()
    {
        try
        {
            var app = Application.Current;
            if (app == null) return;

            // 合并 WPFControl 公共资源（ScriptEditor 视图依赖这些样式）
            var resources = new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/WPFCustomControls;component/Resources/Resources.xaml",
                    UriKind.Absolute)
            };
            app.Resources.MergedDictionaries.Add(resources);

            // 合并查找/替换面板样式，避免继承 MaterialDesign 隐式样式导致 UI 混乱
            var searchReplacePanelSource = new Uri(
                "pack://application:,,,/ScriptEditor;component/Resources/SearchReplacePanel.xaml",
                UriKind.Absolute);
            if (!app.Resources.MergedDictionaries.Any(d => d.Source != null &&
                       string.Equals(d.Source.AbsoluteUri, searchReplacePanelSource.AbsoluteUri, StringComparison.OrdinalIgnoreCase)))
            {
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = searchReplacePanelSource });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"合并 ScriptEditor 资源字典时出错: {ex.Message}");
        }
    }
}