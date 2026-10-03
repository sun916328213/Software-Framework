// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：ScriptEditorModule。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.Integration.Prism、Prism.Ioc、Prism.Modularity、ScriptEditor.Core.Contracts、ScriptEditor.Controls.Services、ScriptEditor.Runtime.Services。导入不代表所有类型都会在运行时加载。
using Extension.Integration.Prism;
using Prism.Ioc;
using Prism.Modularity;
using ScriptEditor.Core.Contracts;
using ScriptEditor.Controls.Services;
using ScriptEditor.Runtime.Services;
using ScriptEditor.Controls.ViewModels;
using ScriptEditor.Controls.Views;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace ScriptEditor.Controls;

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
        WarmupTask ??= Task.Run(() =>
        {
            _ = ScriptHostProvider.Shared;
            _ = CustomRoslynHost.GetAdditionalDefaultReferences();
        });
    }

    /// <summary>注册模块的类型。</summary>

    /// <summary>Registers shared services and transient editor pages without replacing host defaults.</summary>
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        ArgumentNullException.ThrowIfNull(containerRegistry);
        // 文件及编辑器共享服务保留单例；每个脚本管理页面使用独立状态。
        if (!containerRegistry.IsRegistered<FileService>())
            containerRegistry.RegisterSingleton<FileService>();
        if (!containerRegistry.IsRegistered<ScriptEditorService>())
            containerRegistry.RegisterSingleton<ScriptEditorService>();
        // 共享运行时必须注册 Shared 实例（而非新建）：编辑器服务与管理器
        // 经同一运行时取会话，否则同一脚本的运行状态会被拆到两个运行时。
        if (!containerRegistry.IsRegistered<IScriptRuntime>())
            containerRegistry.RegisterInstance<IScriptRuntime>(RoslynScriptRuntime.Shared);
        if (!containerRegistry.IsRegistered<ScriptManagerService>())
            containerRegistry.Register<ScriptManagerService>();

        //// 注册 ViewModel（瞬时模式，每次 Resolve 创建新实例，供多个 Flow 界面独立使用）
        if (!containerRegistry.IsRegistered<ScriptManagerViewModel>())
            containerRegistry.Register<ScriptManagerViewModel>();

        //// ScriptManagerView 注册为瞬时，使 Packing 启动/关闭流程各自持有独立的视图实例。
        //// 同时保留 (object, 导航名) 导航注册，EditorDemo 等按导航名 RequestNavigate 时每次解析新实例。
        if (!containerRegistry.IsRegistered<ScriptManagerView>())
            containerRegistry.Register<ScriptManagerView>();
        // Also wire direct typed resolution, even when the host supplies the named navigation entry.
        Prism.Mvvm.ViewModelLocationProvider.Register<ScriptManagerView, ScriptManagerViewModel>();
        if (!containerRegistry.IsRegistered(typeof(object), nameof(ScriptManagerView)))
            containerRegistry.RegisterForNavigation<ScriptManagerView, ScriptManagerViewModel>();
    }

    /// <summary>合并 ScriptEditor 所需的资源字典。</summary>
    private void MergeResourceDictionaries()
    {
        try
        {
            var app = Application.Current;
            if (app == null) return;

            // 合并 WPFControls 公共资源（ScriptEditor 视图依赖这些样式）
            var commonSource = new Uri(
                "pack://application:,,,/WPFCustomControls;component/Resources/Resources.xaml",
                UriKind.Absolute);
            if (!app.Resources.MergedDictionaries.Any(dictionary => dictionary.Source == commonSource))
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = commonSource });

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
