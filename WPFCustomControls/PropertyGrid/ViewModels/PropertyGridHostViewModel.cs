using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFCustomControls.PropertyGrid.Models;
using WPFCustomControls.PropertyGrid.Services;

namespace WPFCustomControls.PropertyGrid.ViewModels;

/// <summary>
/// 属性面板根 ViewModel。
/// 所有构建逻辑下沉到 <see cref="IPropertyGridBuilderService"/>，对话框走 <see cref="IPortDialogService"/>，
/// 自身只负责把服务产出的分组 VM 树暴露给 View 绑定渲染，并提供搜索过滤。
/// </summary>
public partial class PropertyGridHostViewModel : ObservableObject
{
    private readonly IPropertyGridBuilderService _builderService;
    private readonly IPortDialogService _dialogService;

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

    public PropertyGridHostViewModel(
        IPropertyGridBuilderService builderService,
        IPortDialogService dialogService)
    {
        _builderService = builderService;
        _dialogService = dialogService;
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
            PortManagement = new PortManagementViewModel(manageable, bindable, _dialogService);
        }

        ApplyFilter();
    }

    private void UpdateExtraActions()
    {
        ExtraActions.Clear();
        if (_targetObject == null) return;

        var items = new List<PropertyGridActionItem>();
        foreach (var prop in _targetObject.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = prop.GetCustomAttribute<PropertyGridActionAttribute>(inherit: true);
            if (attr == null) continue;
            if (prop.GetValue(_targetObject) is not ICommand cmd) continue;
            items.Add(new PropertyGridActionItem(attr.DisplayName, cmd, attr.Order));
        }

        foreach (var item in items.OrderBy(i => i.Order))
            ExtraActions.Add(item);
    }
}
