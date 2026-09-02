using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFCustomControls.PropertyGrid.Models;

namespace WPFCustomControls.PropertyGrid.ViewModels;

public partial class PropertyItemViewModel : ObservableObject, IDisposable
{
    private static readonly Type[] NumericTypes =
    {
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(float), typeof(double), typeof(decimal)
    };

    private readonly object _target;
    private readonly PropertyInfo _propertyInfo;
    private readonly IPortConfigurable? _portConfigurable;
    private readonly INotifyPropertyChanged? _notifier;
    private readonly bool _isFloating;
    private bool _disposed;

    public string DisplayName { get; }
    public string PropertyName => _propertyInfo.Name;
    public Type PropertyType { get; }
    public Type UnderlyingType { get; }
    public string? Description { get; }
    public bool IsReadOnly { get; }
    public bool IsCollection { get; }
    public bool IsNullable { get; }
    public bool IsEditable => !IsReadOnly;
    public bool IsBoolean { get; }
    public bool IsNumeric { get; }
    public double? NumericMin { get; }
    public double? NumericMax { get; }
    public double NumericStep { get; }
    public bool HasNumericRange => NumericMin.HasValue || NumericMax.HasValue;
    public IReadOnlyList<EnumOption>? EnumOptions { get; }
    public IEnumerable? CollectionSource => IsCollection ? _propertyInfo.GetValue(_target) as IEnumerable : null;
    public int CollectionCount => CollectionSource is ICollection c ? c.Count : 0;
    public bool IsPortConfigurable { get; }

    public bool IsInputPort
    {
        get
        {
            if (_portConfigurable == null) return false;
            var (isIn, _) = _portConfigurable.GetPortConfig(PropertyName);
            return isIn;
        }
        set
        {
            if (_portConfigurable == null) return;
            var (_, isOut) = _portConfigurable.GetPortConfig(PropertyName);
            _portConfigurable.SetPortConfig(PropertyName, value, isOut);
            OnPropertyChanged();
        }
    }

    public bool IsOutputPort
    {
        get
        {
            if (_portConfigurable == null) return false;
            var (_, isOut) = _portConfigurable.GetPortConfig(PropertyName);
            return isOut;
        }
        set
        {
            if (_portConfigurable == null) return;
            var (isIn, _) = _portConfigurable.GetPortConfig(PropertyName);
            _portConfigurable.SetPortConfig(PropertyName, isIn, value);
            OnPropertyChanged();
        }
    }

    public object? Value
    {
        get => _propertyInfo.GetValue(_target);
        set
        {
            if (IsReadOnly) return;
            if (!TryConvert(value, out var converted)) return;
            converted = ClampNumeric(converted);
            _propertyInfo.SetValue(_target, converted);
            OnPropertyChanged();
        }
    }

    public PropertyItemViewModel(object target, PropertyInfo propInfo,
        NodePropertyAttribute? attr, IPortConfigurable? portConfigurable = null)
    {
        _target = target;
        _propertyInfo = propInfo;
        _portConfigurable = portConfigurable;

        PropertyType = propInfo.PropertyType;
        IsCollection = IsCollectionType(propInfo.PropertyType);
        IsNullable = Nullable.GetUnderlyingType(propInfo.PropertyType) != null;
        UnderlyingType = Nullable.GetUnderlyingType(propInfo.PropertyType) ?? propInfo.PropertyType;

        DisplayName = string.IsNullOrEmpty(attr?.DisplayName) ? propInfo.Name : attr.DisplayName;
        Description = attr?.Description;
        IsReadOnly = attr?.ReadOnly switch
        {
            1 => true, -1 => false,
            _ => propInfo.SetMethod == null || !propInfo.SetMethod.IsPublic
        };
        IsPortConfigurable = attr?.PortConfigurable == true && portConfigurable != null;

        IsBoolean = UnderlyingType == typeof(bool);
        IsNumeric = NumericTypes.Contains(UnderlyingType);
        _isFloating = UnderlyingType == typeof(float)
                      || UnderlyingType == typeof(double)
                      || UnderlyingType == typeof(decimal);

        if (!double.IsNaN(attr?.Min ?? double.NaN)) NumericMin = attr!.Min;
        if (!double.IsNaN(attr?.Max ?? double.NaN)) NumericMax = attr!.Max;
        NumericStep = attr is { Step: > 0 } ? attr.Step : (_isFloating ? 0.01 : 1);

        if (UnderlyingType.IsEnum)
            EnumOptions = BuildEnumOptions(UnderlyingType, IsNullable);
        else if (!string.IsNullOrEmpty(attr?.ItemsSource))
            EnumOptions = ResolveItemsSource(target, attr.ItemsSource);

        if (IsPortConfigurable && _portConfigurable != null)
            _portConfigurable.ConfigChanged += OnPortConfigChanged;

        // 订阅目标对象的属性变更：执行引擎等外部写值后自动刷新本行 UI
        if (target is INotifyPropertyChanged notifier)
        {
            _notifier = notifier;
            notifier.PropertyChanged += OnTargetPropertyChanged;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
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

    /// <summary>数值属性加一步（受 Step / Min / Max 约束）。</summary>
    [RelayCommand]
    private void Increment() => StepValue(NumericStep);

    /// <summary>数值属性减一步（受 Step / Min / Max 约束）。</summary>
    [RelayCommand]
    private void Decrement() => StepValue(-NumericStep);

    private void StepValue(double delta)
    {
        if (!IsNumeric || IsReadOnly) return;
        object? current;
        try
        {
            current = _propertyInfo.GetValue(_target);
        }
        catch
        {
            return;
        }

        double d = current == null ? 0d : Convert.ToDouble(current, CultureInfo.InvariantCulture);
        d = Math.Round(d + delta, 6);
        if (NumericMin.HasValue && d < NumericMin.Value) d = NumericMin.Value;
        if (NumericMax.HasValue && d > NumericMax.Value) d = NumericMax.Value;

        try
        {
            _propertyInfo.SetValue(_target, Convert.ChangeType(d, UnderlyingType, CultureInfo.InvariantCulture));
            // 显式通知 Value：此处位于 StepValue 内部，OnPropertyChanged() 缺省的 CallerMemberName 是 "StepValue"，
            // 不会广播到 TextBox 双向绑定的 Value，导致上下按钮点击后 UI 不刷新（数据其实已写入目标对象）。
            OnPropertyChanged(nameof(Value));
        }
        catch
        {
            // 越界等转换失败时静默忽略
        }
    }

    /// <summary>将用户输入转换为目标类型；失败返回 false（不写入目标）。</summary>
    private bool TryConvert(object? value, out object? result)
    {
        result = null;
        var targetType = UnderlyingType;

        if (value == null)
        {
            if (IsNullable)
            {
                result = null;
                return true;
            }
            return false;
        }

        var valueType = value.GetType();
        if (targetType.IsAssignableFrom(valueType))
        {
            result = value;
            return true;
        }

        if (targetType.IsEnum)
        {
            if (value is string s)
            {
                if (Enum.TryParse(targetType, s, ignoreCase: true, out var parsed))
                {
                    result = parsed;
                    return true;
                }
                // 尝试按显示名匹配（如 [Description] 中文名）
                if (EnumOptions != null)
                {
                    foreach (var opt in EnumOptions)
                    {
                        if (opt.Value != null
                            && string.Equals(opt.DisplayName, s, StringComparison.OrdinalIgnoreCase))
                        {
                            result = opt.Value;
                            return true;
                        }
                    }
                }
                return false;
            }
            if (value is IConvertible)
            {
                try
                {
                    result = Enum.ToObject(targetType, value);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        // 类型转换：优先不变区域化，避免中文区域小数点/分隔符差异
        try
        {
            result = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            try
            {
                result = Convert.ChangeType(value, targetType, CultureInfo.CurrentCulture);
                return true;
            }
            catch
            {
                try
                {
                    var converter = TypeDescriptor.GetConverter(targetType);
                    if (converter.CanConvertFrom(valueType))
                    {
                        result = converter.ConvertFrom(null, CultureInfo.CurrentCulture, value);
                        return true;
                    }
                    return false;
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[PropertyItemViewModel] 无法将值 '{value}' 转换为类型 {targetType.Name}");
                    return false;
                }
            }
        }
    }

    /// <summary>按 Min/Max 裁剪数值。</summary>
    private object? ClampNumeric(object? value)
    {
        if (value == null || !IsNumeric || !HasNumericRange) return value;
        var d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
        if (NumericMin.HasValue && d < NumericMin.Value) d = NumericMin.Value;
        if (NumericMax.HasValue && d > NumericMax.Value) d = NumericMax.Value;
        try
        {
            return Convert.ChangeType(d, UnderlyingType, CultureInfo.InvariantCulture);
        }
        catch
        {
            return value;
        }
    }

    private static IReadOnlyList<EnumOption> BuildEnumOptions(Type enumType, bool nullable)
    {
        var list = new List<EnumOption>();
        if (nullable) list.Add(new EnumOption(null, "(无)"));
        foreach (var value in Enum.GetValues(enumType))
        {
            var name = Enum.GetName(enumType, value) ?? value.ToString() ?? "";
            var field = enumType.GetField(name);
            var desc = field?.GetCustomAttribute<DescriptionAttribute>()?.Description;
            list.Add(new EnumOption(value, string.IsNullOrEmpty(desc) ? name : desc));
        }
        return list;
    }

    /// <summary>
    /// 从目标对象的属性（返回 IEnumerable&lt;string&gt;）解析下拉选项集合。
    /// 通过 [NodeProperty(ItemsSource = nameof(xxx))] 声明，等价于 xceed 属性面板的 ItemsSource 模式；
    /// 每条属性独立声明数据源，多下拉场景无需分发逻辑。
    /// </summary>
    private static IReadOnlyList<EnumOption>? ResolveItemsSource(object target, string memberName)
    {
        var value = target.GetType().GetProperty(memberName)?.GetValue(target);
        if (value is IEnumerable<string> strings)
            return strings.Select(s => new EnumOption(s, s)).ToList();
        return null;
    }

    /// <summary>
    /// 目标对象属性变更时刷新本行：
    /// 仅响应与当前属性匹配（或"所有属性"）的变更，避免无谓刷新。
    /// </summary>
    private void OnTargetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != null && e.PropertyName.Length > 0 && e.PropertyName != _propertyInfo.Name)
            return;

        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            System.Windows.Application.Current.Dispatcher.Invoke(NotifyTargetValueChanged);
        else
            NotifyTargetValueChanged();
    }

    private void NotifyTargetValueChanged()
    {
        NotifyValueChanged();
        if (IsCollection)
            NotifyCollectionChanged();
    }

    private void OnPortConfigChanged()
    {
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(IsInputPort));
                OnPropertyChanged(nameof(IsOutputPort));
            });
        else
        {
            OnPropertyChanged(nameof(IsInputPort));
            OnPropertyChanged(nameof(IsOutputPort));
        }
    }

    private static bool IsCollectionType(Type type) => typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string);
}
