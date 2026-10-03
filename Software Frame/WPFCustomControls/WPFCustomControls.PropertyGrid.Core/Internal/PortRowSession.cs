// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：PortRowSession、PortProperties。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.Concurrent、System.ComponentModel、System.Reflection、WPFCustomControls.PropertyGrid.Core.Models、WPFCustomControls.PropertyGrid.Core.ViewModels。导入不代表所有类型都会在运行时加载。
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using WPFCustomControls.PropertyGrid.Core.Models;
using WPFCustomControls.PropertyGrid.Core.ViewModels;

namespace WPFCustomControls.PropertyGrid.Core.Internal;

/// <summary>
/// Owns the reflection metadata and two-way synchronization between an arbitrary port
/// model and one row ViewModel. Disposing the session releases every event subscription.
/// </summary>
/// <remarks>
/// PortRowSession 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
/// 继承或实现的契约：IDisposable。具体约束按这些契约及各成员实现解释。
/// </remarks>
internal sealed class PortRowSession : IDisposable
{
    private static readonly ConcurrentDictionary<Type, PortProperties> Properties = new();

    private readonly object _model;
    private readonly IPortManageable _owner;
    private readonly PortProperties _properties;
    private readonly INotifyPropertyChanged? _notifier;
    private bool _synchronizing;
    private bool _disposed;

    /// <summary>
    /// 初始化 PortRowSession 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 GetOrAdd、GetType、AddRange、NotifyPropertiesReady；具体条件和先后次序见方法体。
    /// </summary>
    public PortRowSession(
        object model,
        bool isInput,
        bool isReadOnly,
        IEnumerable<string> availableProperties,
        IPortManageable owner)
    {
        _model = model;
        _owner = owner;
        _properties = Properties.GetOrAdd(model.GetType(), PortProperties.Create);

        Row = new PortRowViewModel(Id, Name, isInput, BoundPropertyName)
        {
            Value = _properties.Value?.GetValue(model),
            IsReadOnly = isReadOnly
        };
        Row.AvailableProperties.AddRange(availableProperties);
        Row.NotifyPropertiesReady();
        Row.PropertyChanged += OnRowPropertyChanged;

        if (model is INotifyPropertyChanged notifier)
        {
            _notifier = notifier;
            notifier.PropertyChanged += OnModelPropertyChanged;
        }
    }

    public PortRowViewModel Row { get; }
    public string Id => _properties.Id?.GetValue(_model) as string ?? string.Empty;
    public string Name => _properties.Name?.GetValue(_model) as string ?? string.Empty;
    public string? BoundPropertyName => _properties.PropertyName?.GetValue(_model) as string;
    public string BindingMatchName => BoundPropertyName ?? Name;

    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Row.PropertyChanged -= OnRowPropertyChanged;
        if (_notifier != null)
            _notifier.PropertyChanged -= OnModelPropertyChanged;
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnModelPropertyChanged。
    /// </summary>
    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_synchronizing || args.PropertyName != "Value")
            return;

        _synchronizing = true;
        try
        {
            Row.Value = _properties.Value?.GetValue(_model);
        }
        finally
        {
            _synchronizing = false;
        }
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnRowPropertyChanged。
    /// 正文中的关键协作调用包括 nameof、Set、InvalidatePorts；具体条件和先后次序见方法体。
    /// </summary>
    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_synchronizing)
            return;

        _synchronizing = true;
        try
        {
            switch (args.PropertyName)
            {
                case nameof(PortRowViewModel.Name):
                    Set(_properties.Name, Row.Name);
                    break;
                case nameof(PortRowViewModel.BoundPropertyName):
                    Set(_properties.PropertyName, Row.BoundPropertyName);
                    Set(_properties.Name, Row.BoundPropertyName ?? string.Empty);
                    break;
                case nameof(PortRowViewModel.Value):
                    Set(_properties.Value, Row.Value);
                    break;
                default:
                    return;
            }
            _owner.InvalidatePorts();
        }
        finally
        {
            _synchronizing = false;
        }
    }

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 Set。
    /// </summary>
    private void Set(PropertyInfo? property, object? value) => property?.SetValue(_model, value);

    /// <summary>
    /// PortProperties 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
    /// </summary>
    private sealed record PortProperties(
        PropertyInfo? Id,
        PropertyInfo? Name,
        PropertyInfo? PropertyName,
        PropertyInfo? Value)
    {
        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 Create。
        /// 正文中的关键协作调用包括 GetProperty；具体条件和先后次序见方法体。
        /// </summary>
        public static PortProperties Create(Type type) => new(
            type.GetProperty("Id"),
            type.GetProperty("Name"),
            type.GetProperty("PropertyName"),
            type.GetProperty("Value"));
    }
}
