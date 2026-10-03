// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：PropertyItemViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections、System.ComponentModel、System.Reflection、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、WPFCustomControls.PropertyGrid.Core.Composition。导入不代表所有类型都会在运行时加载。
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFCustomControls.PropertyGrid.Core.Composition;
using WPFCustomControls.PropertyGrid.Core.Internal;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace WPFCustomControls.PropertyGrid.Core.ViewModels;

/// <summary>Binding state for one reflected property editor row.</summary>
/// <remarks>
/// PropertyItemViewModel 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
/// 继承或实现的契约：ObservableObject、IDisposable。具体约束按这些契约及各成员实现解释。
/// </remarks>
public partial class PropertyItemViewModel : ObservableObject, IDisposable
{
    private readonly PropertyValueAccessor _accessor;
    private readonly IUiDispatcher _dispatcher;
    private readonly IPortConfigurable? _portConfigurable;
    private readonly INotifyPropertyChanged? _notifier;
    private bool _disposed;

    /// <summary>
    /// 初始化 PropertyItemViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public PropertyItemViewModel(
        object target,
        PropertyInfo propertyInfo,
        NodePropertyAttribute? attribute,
        IPortConfigurable? portConfigurable = null)
        : this(target, propertyInfo, attribute, portConfigurable, NullUiDispatcher.Instance)
    {
    }

    /// <summary>
    /// 初始化 PropertyItemViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 ThrowIfNull；具体条件和先后次序见方法体。
    /// </summary>
    internal PropertyItemViewModel(
        object target,
        PropertyInfo propertyInfo,
        NodePropertyAttribute? attribute,
        IPortConfigurable? portConfigurable,
        IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(propertyInfo);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _accessor = new PropertyValueAccessor(target, propertyInfo, attribute);
        _dispatcher = dispatcher;
        _portConfigurable = portConfigurable;
        IsPortConfigurable = attribute?.PortConfigurable == true && portConfigurable != null;

        if (IsPortConfigurable)
            _portConfigurable!.ConfigChanged += OnPortConfigChanged;
        if (target is INotifyPropertyChanged notifier)
        {
            _notifier = notifier;
            notifier.PropertyChanged += OnTargetPropertyChanged;
        }
    }

    public string DisplayName => _accessor.DisplayName;
    public string PropertyName => _accessor.PropertyName;
    public Type PropertyType => _accessor.PropertyType;
    public Type UnderlyingType => _accessor.UnderlyingType;
    public string? Description => _accessor.Description;
    public bool IsReadOnly => _accessor.IsReadOnly;
    public bool IsEditable => !IsReadOnly;
    public bool IsCollection => _accessor.IsCollection;
    public bool IsNullable => _accessor.IsNullable;
    public bool IsBoolean => _accessor.IsBoolean;
    public bool IsNumeric => _accessor.IsNumeric;
    public double? NumericMin => _accessor.Minimum;
    public double? NumericMax => _accessor.Maximum;
    public double NumericStep => _accessor.Step;
    public bool HasNumericRange => _accessor.HasRange;
    public IReadOnlyList<EnumOption>? EnumOptions => _accessor.Options;
    public IEnumerable? CollectionSource => _accessor.CollectionSource;
    public int CollectionCount => CollectionSource is ICollection collection ? collection.Count : 0;
    public bool IsPortConfigurable { get; }

    public object? Value
    {
        get => _accessor.Value;
        set
        {
            if (_accessor.TrySetValue(value))
                OnPropertyChanged();
        }
    }

    public bool IsInputPort
    {
        get => _portConfigurable?.GetPortConfig(PropertyName).IsInput ?? false;
        set
        {
            if (_portConfigurable == null)
                return;
            var (_, isOutput) = _portConfigurable.GetPortConfig(PropertyName);
            _portConfigurable.SetPortConfig(PropertyName, value, isOutput);
            OnPropertyChanged();
        }
    }

    public bool IsOutputPort
    {
        get => _portConfigurable?.GetPortConfig(PropertyName).IsOutput ?? false;
        set
        {
            if (_portConfigurable == null)
                return;
            var (isInput, _) = _portConfigurable.GetPortConfig(PropertyName);
            _portConfigurable.SetPortConfig(PropertyName, isInput, value);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_portConfigurable != null)
            _portConfigurable.ConfigChanged -= OnPortConfigChanged;
        if (_notifier != null)
            _notifier.PropertyChanged -= OnTargetPropertyChanged;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 NotifyValueChanged。
    /// 正文中的关键协作调用包括 OnPropertyChanged、nameof；具体条件和先后次序见方法体。
    /// </summary>
    public void NotifyValueChanged() => OnPropertyChanged(nameof(Value));

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 NotifyCollectionChanged。
    /// 正文中的关键协作调用包括 OnPropertyChanged、nameof；具体条件和先后次序见方法体。
    /// </summary>
    public void NotifyCollectionChanged()
    {
        OnPropertyChanged(nameof(CollectionSource));
        OnPropertyChanged(nameof(CollectionCount));
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Increment。
    /// 正文中的关键协作调用包括 StepValue；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand]
    private void Increment() => StepValue(NumericStep);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Decrement。
    /// 正文中的关键协作调用包括 StepValue；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand]
    private void Decrement() => StepValue(-NumericStep);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 StepValue。
    /// 正文中的关键协作调用包括 TryStep、OnPropertyChanged、nameof；具体条件和先后次序见方法体。
    /// </summary>
    private void StepValue(double delta)
    {
        if (_accessor.TryStep(delta))
            OnPropertyChanged(nameof(Value));
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnTargetPropertyChanged。
    /// 正文中的关键协作调用包括 IsNullOrEmpty、Dispatch；具体条件和先后次序见方法体。
    /// </summary>
    private void OnTargetPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!string.IsNullOrEmpty(args.PropertyName) && args.PropertyName != PropertyName)
            return;
        Dispatch(NotifyTargetValueChanged);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 NotifyTargetValueChanged。
    /// 正文中的关键协作调用包括 NotifyValueChanged、NotifyCollectionChanged；具体条件和先后次序见方法体。
    /// </summary>
    private void NotifyTargetValueChanged()
    {
        NotifyValueChanged();
        if (IsCollection)
            NotifyCollectionChanged();
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnPortConfigChanged。
    /// 正文中的关键协作调用包括 Dispatch、OnPropertyChanged、nameof；具体条件和先后次序见方法体。
    /// </summary>
    private void OnPortConfigChanged() => Dispatch(() =>
    {
        OnPropertyChanged(nameof(IsInputPort));
        OnPropertyChanged(nameof(IsOutputPort));
    });

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Dispatch。
    /// 正文中的关键协作调用包括 CheckAccess、action、Post；具体条件和先后次序见方法体。
    /// </summary>
    private void Dispatch(Action action)
    {
        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.Post(action);
    }
}
