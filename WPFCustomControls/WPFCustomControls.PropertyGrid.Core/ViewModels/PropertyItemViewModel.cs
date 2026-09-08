using System.Collections;
using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFCustomControls.PropertyGrid.Composition;
using WPFCustomControls.PropertyGrid.Internal;
using WPFCustomControls.PropertyGrid.Models;

namespace WPFCustomControls.PropertyGrid.ViewModels;

/// <summary>Binding state for one reflected property editor row.</summary>
public partial class PropertyItemViewModel : ObservableObject, IDisposable
{
    private readonly PropertyValueAccessor _accessor;
    private readonly IPortConfigurable? _portConfigurable;
    private readonly INotifyPropertyChanged? _notifier;
    private bool _disposed;

    public PropertyItemViewModel(
        object target,
        PropertyInfo propertyInfo,
        NodePropertyAttribute? attribute,
        IPortConfigurable? portConfigurable = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(propertyInfo);

        _accessor = new PropertyValueAccessor(target, propertyInfo, attribute);
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

    public void NotifyValueChanged() => OnPropertyChanged(nameof(Value));

    public void NotifyCollectionChanged()
    {
        OnPropertyChanged(nameof(CollectionSource));
        OnPropertyChanged(nameof(CollectionCount));
    }

    [RelayCommand]
    private void Increment() => StepValue(NumericStep);

    [RelayCommand]
    private void Decrement() => StepValue(-NumericStep);

    private void StepValue(double delta)
    {
        if (_accessor.TryStep(delta))
            OnPropertyChanged(nameof(Value));
    }

    private void OnTargetPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!string.IsNullOrEmpty(args.PropertyName) && args.PropertyName != PropertyName)
            return;
        Dispatch(NotifyTargetValueChanged);
    }

    private void NotifyTargetValueChanged()
    {
        NotifyValueChanged();
        if (IsCollection)
            NotifyCollectionChanged();
    }

    private void OnPortConfigChanged() => Dispatch(() =>
    {
        OnPropertyChanged(nameof(IsInputPort));
        OnPropertyChanged(nameof(IsOutputPort));
    });

    private static void Dispatch(Action action)
    {
        if (UiDispatcher.Current.CheckAccess())
            action();
        else
            UiDispatcher.Current.Post(action);
    }
}
