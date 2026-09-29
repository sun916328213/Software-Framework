using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScriptEditor.Editors;
using ScriptEditor.Models;
using ScriptEditor.Services;
using ScriptEditor.ViewModels;

namespace ScriptEditor.Lifecycle;

/// <summary>
/// 编辑器生命周期控制器：接管原来 <c>ScriptEditorView.xaml.cs</c> 中的
/// Loaded/Unloaded、Tab 延迟初始化、DataContext 切页、脚本关联、
/// 断点存取、滚动记忆与快捷键绑定。
/// </summary>
/// <remarks>
/// P4 产物。运行状态本身仍由 <see cref="Contracts.IScriptSession"/> 独占，
/// 本类只做 UI 侧的关联与记忆（光标/滚动/断点按脚本独立保存），
/// 不实现编译/执行算法，不做文件持久化。
/// </remarks>
public sealed class EditorLifecycleController
{
    private readonly DependencyObject _viewRoot;
    private readonly Func<object?> _getDataContext;
    private readonly Func<ScriptEditorService?> _getService;
    private readonly Func<ScriptEditorViewModel?> _getViewModel;
    private readonly Func<ScriptItem?> _getAttachedItem;
    private readonly Action<ScriptItem?> _setAttachedItem;
    private readonly RoslynEditorAdapter _adapter;
    private readonly ScriptSessionBinder _binder;
    private readonly TextBox _outputBox;
    private readonly Action _invalidateMeasure;
    private readonly Func<bool> _getRoslynInitialized;
    private readonly Action<string> _setEditorText;
    private readonly Action<IEnumerable<string>> _setReferences;
    private readonly Action _closeLoadingWindow;
    private readonly Func<Task<bool>> _initializeCoreAsync;

    private bool _onLoadedHandled;
    private bool _initDeferralHooked;
    private bool _deferLayout = true;

    /// <summary>抑制断点集合变更同步到 ScriptItem 的标志（切页保存/恢复期间使用）。</summary>
    public bool SuppressBreakpointSync { get; set; }

    public bool DeferLayout
    {
        get => _deferLayout;
        set => _deferLayout = value;
    }

    public EditorLifecycleController(
        DependencyObject viewRoot,
        Func<object?> getDataContext,
        Func<ScriptEditorService?> getService,
        Func<ScriptEditorViewModel?> getViewModel,
        Func<ScriptItem?> getAttachedItem,
        Action<ScriptItem?> setAttachedItem,
        RoslynEditorAdapter adapter,
        ScriptSessionBinder binder,
        TextBox outputBox,
        Action invalidateMeasure,
        Func<bool> getRoslynInitialized,
        Action<string> setEditorText,
        Action<IEnumerable<string>> setReferences,
        Action closeLoadingWindow,
        Func<Task<bool>> initializeCoreAsync)
    {
        _viewRoot = viewRoot;
        _getDataContext = getDataContext;
        _getService = getService;
        _getViewModel = getViewModel;
        _getAttachedItem = getAttachedItem;
        _setAttachedItem = setAttachedItem;
        _adapter = adapter;
        _binder = binder;
        _outputBox = outputBox;
        _invalidateMeasure = invalidateMeasure;
        _getRoslynInitialized = getRoslynInitialized;
        _setEditorText = setEditorText;
        _setReferences = setReferences;
        _closeLoadingWindow = closeLoadingWindow;
        _initializeCoreAsync = initializeCoreAsync;
    }

    // ── Loaded / Unloaded ──────────────────────────────────────────

    public void OnLoaded()
    {
        _getViewModel()?.Attach();

        if (_onLoadedHandled) return;
        _onLoadedHandled = true;

        if (_getDataContext() is ScriptItem item)
            AttachToItem(item);

        EnsureDeferredInitialization();
    }

    public void OnUnloaded(System.Windows.Threading.Dispatcher dispatcher, Func<bool> isLoaded, Func<ScriptEditorViewModel?> getVm)
    {
        dispatcher.BeginInvoke(new Action(() =>
        {
            if (!isLoaded())
                getVm()?.Detach();
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    // ── DataContext（Tab 切页） ────────────────────────────────────

    /// <summary>
    /// DataContext 切换到新脚本时的同步切页。
    /// RoslynPad 已初始化时做轻量内容替换；首次走异步延迟初始化。
    /// 返回 true 表示已同步处理，false 表示已发起异步初始化。
    /// </summary>
    public bool OnDataContextChanged(
        ScriptItem newItem,
        Action<ScriptItem> beginAsyncInit)
    {
        if (_getAttachedItem() == newItem) return true;

        if (_getRoslynInitialized())
        {
            Debug.WriteLine("[ScriptEditor] RoslynPad initialized, doing synchronous tab switch");
            _binder.SuppressAutoScroll = true;
            try
            {
                SaveCaretToAttachedItem();
                _outputBox.ScrollToVerticalOffset(0);

                AttachToItem(newItem);
                _setEditorText(newItem.Code);
                if (_adapter.Editor != null)
                {
                    _adapter.Text = newItem.Code;
                    _adapter.CaretOffset = newItem.CaretOffset;
                }
                RestoreEditorScrollState(newItem);
                RestoreOutputScrollState(newItem);
            }
            finally
            {
                _binder.SuppressAutoScroll = false;
            }

            _invalidateMeasure();
            _closeLoadingWindow();
            return true;
        }

        _initDeferralHooked = false;
        _deferLayout = true;
        AttachToItem(newItem);
        beginAsyncInit(newItem);
        return false;
    }

    // ── 脚本关联 ──────────────────────────────────────────────────

    /// <summary>
    /// 将编辑器与脚本项关联：切换服务会话、加载引用、恢复断点。
    /// 运行状态不再播种——订阅会话即读到该脚本的运行事实。
    /// </summary>
    public void AttachToItem(ScriptItem item)
    {
        var current = _getAttachedItem();
        if (current == item) return;

        var vm = _getViewModel();
        if (current != null && vm != null)
        {
            current.Breakpoints.Clear();
            foreach (var bp in vm.Breakpoints)
                current.Breakpoints.Add(bp);
        }

        _setAttachedItem(item);

        var service = _getService();
        if (service != null)
        {
            service.AttachedItem = item;
            _binder.Bind(service.Session);
        }

        _setReferences(item.ReferencePaths);

        if (vm != null)
        {
            SuppressBreakpointSync = true;
            try
            {
                vm.Breakpoints.Clear();
                foreach (var bp in item.Breakpoints)
                    vm.Breakpoints.Add(bp);
            }
            finally
            {
                SuppressBreakpointSync = false;
            }
        }
    }

    public void OnBreakpointsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (SuppressBreakpointSync) return;
        var attached = _getAttachedItem();
        var vm = _getViewModel();
        if (attached == null || vm == null) return;
        if (!ReferenceEquals(attached, _getDataContext())) return;

        attached.Breakpoints.Clear();
        foreach (var bp in vm.Breakpoints)
            attached.Breakpoints.Add(bp);
    }

    public void OnViewModelSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScriptEditorViewModel.Session))
        {
            var service = _getService();
            if (service != null)
                _binder.Bind(service.Session);
        }
    }

    // ── 延迟初始化（Tab 懒加载） ───────────────────────────────────

    public void EnsureDeferredInitialization()
    {
        if (_initDeferralHooked) return;
        _initDeferralHooked = true;

        var tab = FindTabItem();
        if (tab == null)
        {
            _ = InitializeWhenSelectedAsync();
            return;
        }

        if (tab.IsSelected)
        {
            _ = InitializeWhenSelectedAsync();
            return;
        }

        DependencyPropertyDescriptor.FromProperty(TabItem.IsSelectedProperty, typeof(TabItem))
            .AddValueChanged(tab, OnTabSelectionChanged);
    }

    private void OnTabSelectionChanged(object? sender, EventArgs e)
    {
        var tab = FindTabItem();
        if (tab == null || !tab.IsSelected) return;

        DependencyPropertyDescriptor.FromProperty(TabItem.IsSelectedProperty, typeof(TabItem))
            .RemoveValueChanged(tab, OnTabSelectionChanged);

        _ = InitializeWhenSelectedAsync();
    }

    private async Task InitializeWhenSelectedAsync()
    {
        if (_deferLayout)
        {
            _deferLayout = false;
            _invalidateMeasure();
        }

        await _initializeCoreAsync().ConfigureAwait(true);
    }

    private TabItem? FindTabItem()
    {
        var tabControl = FindVisualAncestor<TabControl>(_viewRoot);
        return tabControl?.ItemContainerGenerator.ContainerFromItem(_getDataContext()) as TabItem;
    }

    private static T? FindVisualAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T found) return found;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    // ── 视图状态记忆（光标/滚动/输出滚动） ─────────────────────────

    public void SaveCaretToAttachedItem()
    {
        var attached = _getAttachedItem();
        if (attached != null && _adapter.Editor != null)
            attached.CaretOffset = _adapter.CaretOffset;
    }

    public void SaveEditorScrollState()
    {
        var attached = _getAttachedItem();
        if (attached == null) return;
        if (!ReferenceEquals(attached, _getDataContext())) return;
        attached.EditorScrollOffset = _adapter.VerticalOffset;
    }

    public void RestoreEditorScrollState(ScriptItem item)
    {
        _adapter.VerticalOffset = item.EditorScrollOffset;
        var dispatcher = (_viewRoot as UIElement)?.Dispatcher;
        var target = item.EditorScrollOffset;
        dispatcher?.BeginInvoke(new Action(() =>
        {
            if (ReferenceEquals(_getAttachedItem(), item))
                _adapter.VerticalOffset = target;
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void SaveOutputScrollState()
    {
        var attached = _getAttachedItem();
        if (attached == null || _outputBox == null) return;
        if (!ReferenceEquals(attached, _getDataContext())) return;
        attached.ScrollOffset = _outputBox.VerticalOffset;
    }

    public void RestoreOutputScrollState(ScriptItem item)
    {
        var targetOffset = item.ScrollOffset;
        _binder.SuppressAutoScroll = true;
        _outputBox.ScrollToVerticalOffset(targetOffset);

        var dispatcher = (_viewRoot as UIElement)?.Dispatcher;
        dispatcher?.BeginInvoke(new Action(() =>
        {
            try
            {
                if (ReferenceEquals(_getAttachedItem(), item))
                    _outputBox.ScrollToVerticalOffset(targetOffset);
            }
            finally
            {
                _binder.SuppressAutoScroll = false;
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // ── 快捷键 ────────────────────────────────────────────────────

    public static void SetupKeyBindings(UIElement view, ScriptEditorViewModel vm)
    {
        view.InputBindings.Clear();
        AddKeyBinding(view, vm.NewDocumentCommand, Key.N, ModifierKeys.Control);
        AddKeyBinding(view, vm.OpenDocumentCommand, Key.O, ModifierKeys.Control);
        AddKeyBinding(view, vm.SaveCommand, Key.S, ModifierKeys.Control);
        AddKeyBinding(view, vm.SaveAsCommand, Key.S, ModifierKeys.Control | ModifierKeys.Shift);
        AddKeyBinding(view, vm.CompileCommand, Key.B, ModifierKeys.Control);
        AddKeyBinding(view, vm.RunCommand, Key.F5, ModifierKeys.None);
        AddKeyBinding(view, vm.TerminateCommand, Key.F5, ModifierKeys.Shift);
        AddKeyBinding(view, vm.FormatDocumentCommand, Key.D, ModifierKeys.Control);
        AddKeyBinding(view, vm.CommentSelectionCommand, Key.K, ModifierKeys.Control);
        AddKeyBinding(view, vm.UncommentSelectionCommand, Key.U, ModifierKeys.Control);
        AddKeyBinding(view, vm.RenameSymbolCommand, Key.F2, ModifierKeys.None);
    }

    private static void AddKeyBinding(UIElement view, ICommand command, Key key, ModifierKeys modifiers)
    {
        view.InputBindings.Add(new KeyBinding(command, new KeyGesture(key, modifiers)));
    }
}
