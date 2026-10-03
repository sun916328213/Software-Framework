// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：IScriptSession。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.ComponentModel。导入不代表所有类型都会在运行时加载。
using System.ComponentModel;

namespace ScriptEditor.Core.Contracts;

/// <summary>
/// 一次脚本运行/编辑会话：脚本运行状态的**唯一事实源**。
/// </summary>
/// <remarks>
/// <para>会话独占持有 <see cref="IsRunning"/>、<see cref="IsLooping"/>、<see cref="Output"/>、
/// <see cref="StatusText"/>、<see cref="CompilationState"/>、取消令牌与编译缓存。
/// UI 层（View / ViewModel）只读取本接口，不再各自保存一份运行状态副本。</para>
/// <para>会话与脚本数据的关联由 <see cref="IScriptRuntime"/> 维护；
/// 脚本数据对象（<c>ScriptItem</c>）本身不持有会话或运行时实例。</para>
/// <para>实现须保证属性变更在 UI 线程之外触发时，订阅方（WPF 绑定）自行封送。</para>
/// </remarks>
public interface IScriptSession : INotifyPropertyChanged
{
    /// <summary>会话对应的脚本永久标识。</summary>
    Guid ScriptId { get; }

    /// <summary>会话显示名（脚本标题）。</summary>
    string Title { get; }

    /// <summary>是否正在执行（含循环执行中的每一次迭代）。</summary>
    bool IsRunning { get; }

    /// <summary>是否处于循环执行模式。</summary>
    bool IsLooping { get; }

    /// <summary>输出面板文本（已截断的超长内容保留最多 200 行）。</summary>
    string Output { get; }

    /// <summary>状态栏文本（就绪 / 编译中 / 编译完成 / 执行成功 / 执行失败 / 已取消 ...）。</summary>
    string StatusText { get; }

    /// <summary>最近一次执行或编译的耗时文本（"xx.xx ms"）。</summary>
    string ExecutionTime { get; }

    /// <summary>循环执行间隔（毫秒）。</summary>
    int LoopInterval { get; set; }

    /// <summary>编译阶段状态。</summary>
    ScriptCompilationState CompilationState { get; }

    /// <summary>最近一次脚本执行的原始返回值。</summary>
    object? LastResult { get; }

    /// <summary>综合状态变更（IsRunning / IsLooping / StatusText / CompilationState）。</summary>
    event Action? StateChanged;

    /// <summary>执行脚本，返回是否成功。</summary>
    Task<bool> RunAsync();

    /// <summary>循环执行脚本，直到 <see cref="StopLoop"/> 或 <see cref="Terminate"/>。</summary>
    Task LoopRunAsync();

    /// <summary>仅编译不执行，返回是否编译成功。</summary>
    Task<bool> CompileAsync();

    /// <summary>后台静默预编译（填充编译缓存，不改变可见状态）。</summary>
    Task PreCompileAsync(string code);

    /// <summary>终止当前执行（不退出循环模式）。</summary>
    void Terminate();

    /// <summary>停止循环执行。</summary>
    void StopLoop();

    /// <summary>设置脚本执行时注入的输入数据（作为 globals 传入）。</summary>
    void SetInputData(object? data);

    /// <summary>将内部输出队列冲刷进 <see cref="Output"/>（由 UI 定时器或执行结束时调用）。</summary>
    void FlushOutput();

    /// <summary>编辑器级消息写入输出面板（保存/打开/初始化提示等，替换当前输出）。</summary>
    void SetOutput(string output);

    /// <summary>编辑器级状态消息写入状态栏。</summary>
    void SetStatus(string statusText);

    /// <summary>
    /// 所属任务上下文（P5）：本次编译使用的隐式 using 与引用基准目录。
    /// null 时使用进程默认选项（与 P5 前行为一致）；设置后编译缓存失效一次。
    /// </summary>
    ScriptEditor.Core.Options.ScriptProjectContext? ProjectContext { get; set; }
}
