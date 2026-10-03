// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Controls.Services、SecsGem.Controls.ViewModels、System.Collections.Specialized、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using SecsGem.Controls.Services;
using SecsGem.Controls.ViewModels;
using System.Collections.Specialized;
using System.Windows.Controls;

namespace SecsGem.Controls.Views;

/// <summary>
/// SecsView 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// 继承或实现的契约：UserControl、IDisposable。具体约束按这些契约及各成员实现解释。
/// </summary>
public partial class SecsView : UserControl, IDisposable
{
    private readonly SecsViewModel _viewModel;
    private IDisposable? _subscription;
    private bool _disposed;
    /// <summary>
    /// 初始化 SecsView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public SecsView(SecsWorkspace workspace) : this(new SecsViewModel(workspace)) { }
    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 Create。
    /// </summary>
    public static SecsView Create(SecsViewModel viewModel) => new(viewModel);
    /// <summary>
    /// 初始化 SecsView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent、Subscribe、Detach；具体条件和先后次序见方法体。
    /// </summary>
    private SecsView(SecsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) =>
        {
            if (_disposed || _subscription != null) return;
            _subscription = _viewModel.Subscribe();
            _viewModel.Messages.CollectionChanged += OnMessagesChanged;
        };
        Unloaded += (_, _) => Detach();
    }
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnMessagesChanged。
    /// 正文中的关键协作调用包括 ScrollIntoView；具体条件和先后次序见方法体。
    /// </summary>
    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel.Messages.Count > 0) MessageLog.ScrollIntoView(_viewModel.Messages[^1]);
    }
    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Detach、Dispose；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Detach();
        _viewModel.Dispose();
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Detach。
    /// </summary>
    private void Detach()
    {
        _viewModel.Messages.CollectionChanged -= OnMessagesChanged;
        _subscription?.Dispose();
        _subscription = null;
    }
}
