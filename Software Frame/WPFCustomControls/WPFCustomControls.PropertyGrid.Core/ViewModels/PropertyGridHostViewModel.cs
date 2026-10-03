// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：PropertyGridHostViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.ObjectModel、System.Reflection、System.Windows.Input、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、WPFCustomControls.PropertyGrid.Core.Composition。导入不代表所有类型都会在运行时加载。
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFCustomControls.PropertyGrid.Core.Composition;
using WPFCustomControls.PropertyGrid.Core.Models;
using WPFCustomControls.PropertyGrid.Core.Services;

namespace WPFCustomControls.PropertyGrid.Core.ViewModels;

/// <summary>
/// 属性面板根 ViewModel。
/// 所有构建逻辑下沉到 <see cref="IPropertyGridBuilderService"/>，对话框走 <see cref="IPortDialogService"/>，
/// 自身只负责把服务产出的分组 VM 树暴露给 View 绑定渲染，并提供搜索过滤。
/// </summary>
public partial class PropertyGridHostViewModel : ObservableObject
{
    private readonly IPropertyGridBuilderService _builderService;
    private readonly IPortDialogService _dialogService;
    private readonly IUiDispatcher _dispatcher;

    /// <summary>全量分组母版（过滤只影响 <see cref="Groups"/>，不重建属性行，避免反复订阅/释放）。</summary>
    private readonly List<PropertyGroupViewModel> _allGroups = new();

    public ObservableCollection<PropertyGridActionItem> ExtraActions { get; } = new();

    public ObservableCollection<PropertyGroupViewModel> Groups { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchText))]
    private string _searchText = string.Empty;

    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

    private PortManagementViewModel? _portManagement;
    public PortManagementViewModel? PortManagement
    {
        get => _portManagement;
        private set => SetProperty(ref _portManagement, value);
    }

    private object? _targetObject;
    public object? TargetObject
    {
        get => _targetObject;
        set
        {
            if (SetProperty(ref _targetObject, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                UpdateExtraActions();
                RebuildContent();
            }
        }
    }

    public bool HasSelection => _targetObject != null;

    /// <summary>
    /// 初始化 PropertyGridHostViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public PropertyGridHostViewModel(
        IPropertyGridBuilderService builderService,
        IPortDialogService dialogService)
        : this(builderService, dialogService, NullUiDispatcher.Instance)
    {
    }

    /// <summary>
    /// 初始化 PropertyGridHostViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 ThrowIfNull；具体条件和先后次序见方法体。
    /// </summary>
    public PropertyGridHostViewModel(
        IPropertyGridBuilderService builderService,
        IPortDialogService dialogService,
        IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(builderService);
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(dispatcher);
        _builderService = builderService;
        _dialogService = dialogService;
        _dispatcher = dispatcher;
    }

    /// <summary>搜索关键字变更时刷新可见分组/属性行。</summary>
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    /// <summary>清除搜索关键字。</summary>
    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
        ApplyFilter();
    }

    /// <summary>根据搜索关键字重建可见分组列表。</summary>
    private void ApplyFilter()
    {
        Groups.Clear();
        if (_targetObject == null) return;

        var keyword = SearchText?.Trim();
        foreach (var group in _allGroups)
        {
            group.ApplyFilter(keyword);
            if (group.ItemCount > 0)
                Groups.Add(group);
        }
        OnPropertyChanged(nameof(HasSearchText));
    }

    /// <summary>根据 TargetObject 重建属性分组与自定义端口管理区。</summary>
    private void RebuildContent()
    {
        foreach (var group in _allGroups)
            group.Dispose();
        _allGroups.Clear();
        Groups.Clear();
        PortManagement?.Dispose();
        PortManagement = null;

        if (_targetObject == null) return;

        var portConfig = _targetObject as IPortConfigurable;
        foreach (var group in _builderService.BuildGroups(_targetObject, portConfig))
            _allGroups.Add(group);

        if (_targetObject is IPortManageable manageable)
        {
            var bindable = _builderService.CollectBindableProperties(_targetObject.GetType());
            PortManagement = new PortManagementViewModel(manageable, bindable, _dialogService, _dispatcher);
        }

        ApplyFilter();
    }

    /// <summary>
    /// 更新当前实现管理的数据或状态；对应入口为 UpdateExtraActions。
    /// 正文中的关键协作调用包括 Clear、GetProperties、GetType、GetCustomAttribute、GetValue；具体条件和先后次序见方法体。
    /// </summary>
    private void UpdateExtraActions()
    {
        ExtraActions.Clear();
        if (_targetObject == null) return;

        var items = new List<PropertyGridActionItem>();
        foreach (var prop in _targetObject.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = prop.GetCustomAttribute<PropertyGridActionAttribute>(inherit: true);
            if (attr == null) continue;

            var value = prop.GetValue(_targetObject);
            if (value is null) continue;

            if (value is not ICommand cmd) continue;
            items.Add(new PropertyGridActionItem(attr.DisplayName, cmd, attr.Order));
        }

        foreach (var item in items.OrderBy(i => i.Order))
            ExtraActions.Add(item);
    }
}
