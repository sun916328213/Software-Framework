// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ScriptSessionBinder。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Controls、System.ComponentModel、System.Windows、ScriptEditor.Core.Contracts。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Controls;
using System.ComponentModel;
using System.Windows;
using ScriptEditor.Core.Contracts;

namespace ScriptEditor.Controls.Lifecycle;

/// <summary>
/// 会话绑定器：把 <see cref="IScriptSession"/>（运行状态唯一事实源）
/// 同步到视图依赖属性（Output/Status/IsRunning/IsLooping/LoopInterval）。
/// </summary>
/// <remarks>
/// P4 产物：原来 <c>ScriptEditorView</c> 中的 <c>SubscribeSession/SyncStateFromSession/
/// OnSessionPropertyChanged/_suppress*</c> 逻辑集中到此。视图只提供写入委托，
/// 不再自行实现会话订阅与回环抑制。
/// </remarks>
public sealed class ScriptSessionBinder
{
    private IScriptSession? _session;

    private readonly Action<string> _setOutput;
    private readonly Action<string> _setStatus;
    private readonly Action<bool> _setRunning;
    private readonly Action<bool> _setLooping;
    private readonly Action<int> _setLoopInterval;
    private readonly Func<string> _getOutput;
    private readonly Func<int> _getLoopInterval;
    private readonly Action _requestAutoScroll;

    /// <summary>切页全量同步期间抑制输出自动滚动。</summary>
    public bool SuppressAutoScroll { get; set; }

    private bool _suppressOutputEcho;
    private bool _suppressLoopIntervalEcho;

    public IScriptSession? Session => _session;

    /// <summary>
    /// 初始化 ScriptSessionBinder 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public ScriptSessionBinder(
        Action<string> setOutput,
        Action<string> setStatus,
        Action<bool> setRunning,
        Action<bool> setLooping,
        Action<int> setLoopInterval,
        Func<string> getOutput,
        Func<int> getLoopInterval,
        Action requestAutoScroll)
    {
        _setOutput = setOutput;
        _setStatus = setStatus;
        _setRunning = setRunning;
        _setLooping = setLooping;
        _setLoopInterval = setLoopInterval;
        _getOutput = getOutput;
        _getLoopInterval = getLoopInterval;
        _requestAutoScroll = requestAutoScroll;
    }

    /// <summary>切换订阅到新会话并做一次全量同步（切页/初始化时调用）。</summary>
    public void Bind(IScriptSession? session)
    {
        if (ReferenceEquals(session, _session)) return;

        if (_session != null)
            _session.PropertyChanged -= OnSessionPropertyChanged;

        _session = session;

        if (_session != null)
            _session.PropertyChanged += OnSessionPropertyChanged;

        SyncAll();
    }

    /// <summary>把会话当前状态一次性同步到视图（首次初始化/切页时调用）。</summary>
    public void SyncAll()
    {
        if (_session == null) return;

        SuppressAutoScroll = true;
        _suppressOutputEcho = true;
        _suppressLoopIntervalEcho = true;
        try
        {
            _setOutput(_session.Output);
            _setStatus(_session.StatusText);
            _setRunning(_session.IsRunning);
            _setLooping(_session.IsLooping);
            _setLoopInterval(_session.LoopInterval);
        }
        finally
        {
            _suppressOutputEcho = false;
            _suppressLoopIntervalEcho = false;
            SuppressAutoScroll = false;
        }
    }

    /// <summary>
    /// 外部（宿主绑定）写入 OutputText 时转发给会话。
    /// 视图自身同步时由内部抑制标记防回环，调用前检查即可。
    /// </summary>
    public bool TryForwardOutputToSession(string text)
    {
        if (_suppressOutputEcho || _session == null) return false;
        if (string.Equals(_session.Output, text, StringComparison.Ordinal)) return false;
        _session.SetOutput(text);
        return true;
    }

    /// <summary>外部写入循环间隔时转发给会话。</summary>
    public bool TryForwardLoopIntervalToSession(int interval)
    {
        if (_suppressLoopIntervalEcho || _session == null) return false;
        _session.LoopInterval = interval;
        return true;
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnSessionPropertyChanged。
    /// 正文中的关键协作调用包括 CheckAccess、BeginInvoke、OnSessionPropertyChanged、nameof、_setOutput；具体条件和先后次序见方法体。
    /// </summary>
    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 会话可能在后台线程触发，封送到 UI 线程再写依赖属性。
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(() => OnSessionPropertyChanged(sender, e)));
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(IScriptSession.Output):
                _suppressOutputEcho = true;
                try { _setOutput(_session?.Output ?? string.Empty); }
                finally { _suppressOutputEcho = false; }
                _requestAutoScroll();
                break;
            case nameof(IScriptSession.StatusText):
                _setStatus(_session?.StatusText ?? "就绪");
                break;
            case nameof(IScriptSession.IsRunning):
                _setRunning(_session?.IsRunning == true);
                break;
            case nameof(IScriptSession.IsLooping):
                _setLooping(_session?.IsLooping == true);
                break;
            case nameof(IScriptSession.LoopInterval):
                _suppressLoopIntervalEcho = true;
                try { _setLoopInterval(_session?.LoopInterval ?? _getLoopInterval()); }
                finally { _suppressLoopIntervalEcho = false; }
                break;
        }
    }
}
