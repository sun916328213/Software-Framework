// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ScriptLoadingWindowRegistry。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Controls、ScriptEditor.Runtime.Services、System.Runtime.CompilerServices、System.Windows.Threading、ScriptEditor.Controls.Models、ScriptEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Controls;
using ScriptEditor.Runtime.Services;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using ScriptEditor.Controls.Models;
using ScriptEditor.Core.Models;
using WPFCustomControls.Window.Views;

namespace ScriptEditor.Controls.Services;

/// <summary>
/// ScriptLoadingWindowRegistry 的类型定义。ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
/// </summary>
internal static class ScriptLoadingWindowRegistry
{
    private static readonly ConditionalWeakTable<ScriptItem, FileLoading> Windows = new();

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 Set。
    /// 正文中的关键协作调用包括 Remove、Add；具体条件和先后次序见方法体。
    /// </summary>
    public static void Set(ScriptItem item, FileLoading? window)
    {
        Windows.Remove(item);
        if (window != null) Windows.Add(item, window);
    }

    /// <summary>
    /// 执行关闭步骤；对应入口为 Close。
    /// 正文中的关键协作调用包括 TryGetValue、Remove、BeginInvoke、Close、BeginInvokeShutdown；具体条件和先后次序见方法体。
    /// </summary>
    public static void Close(ScriptItem item)
    {
        if (!Windows.TryGetValue(item, out var window)) return;
        Windows.Remove(item);
        window.Dispatcher.BeginInvoke(() =>
        {
            window.Close();
            window.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
        });
    }
}
