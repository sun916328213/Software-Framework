using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using WPFCustomControls.PropertyGrid.Models;
using WPFCustomControls.PropertyGrid.ViewModels;

namespace WPFCustomControls.PropertyGrid.Internal;

/// <summary>
/// Owns the reflection metadata and two-way synchronization between an arbitrary port
/// model and one row ViewModel. Disposing the session releases every event subscription.
/// </summary>
internal sealed class PortRowSession : IDisposable
{
    private static readonly ConcurrentDictionary<Type, PortProperties> Properties = new();

    private readonly object _model;
    private readonly IPortManageable _owner;
    private readonly PortProperties _properties;
    private readonly INotifyPropertyChanged? _notifier;
    private bool _synchronizing;
    private bool _disposed;

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

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Row.PropertyChanged -= OnRowPropertyChanged;
        if (_notifier != null)
            _notifier.PropertyChanged -= OnModelPropertyChanged;
    }

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

    private void Set(PropertyInfo? property, object? value) => property?.SetValue(_model, value);

    private sealed record PortProperties(
        PropertyInfo? Id,
        PropertyInfo? Name,
        PropertyInfo? PropertyName,
        PropertyInfo? Value)
    {
        public static PortProperties Create(Type type) => new(
            type.GetProperty("Id"),
            type.GetProperty("Name"),
            type.GetProperty("PropertyName"),
            type.GetProperty("Value"));
    }
}
