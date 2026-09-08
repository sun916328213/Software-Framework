using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using WPFCustomControls.PropertyGrid.Models;

namespace WPFCustomControls.PropertyGrid.Internal;

/// <summary>
/// Owns property metadata, conversion, range enforcement and value access behind one
/// internal interface. The ViewModel remains responsible only for binding and notifications.
/// </summary>
internal sealed class PropertyValueAccessor
{
    private static readonly HashSet<Type> NumericTypes =
    [
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(float), typeof(double), typeof(decimal)
    ];

    private readonly object _target;
    private readonly PropertyInfo _property;

    public PropertyValueAccessor(object target, PropertyInfo property, NodePropertyAttribute? attribute)
    {
        _target = target;
        _property = property;

        PropertyType = property.PropertyType;
        IsNullable = Nullable.GetUnderlyingType(PropertyType) != null;
        UnderlyingType = Nullable.GetUnderlyingType(PropertyType) ?? PropertyType;
        IsCollection = typeof(IEnumerable).IsAssignableFrom(PropertyType) && PropertyType != typeof(string);
        IsReadOnly = attribute?.ReadOnly switch
        {
            1 => true,
            -1 => false,
            _ => property.SetMethod == null || !property.SetMethod.IsPublic
        };
        IsBoolean = UnderlyingType == typeof(bool);
        IsNumeric = NumericTypes.Contains(UnderlyingType);

        DisplayName = string.IsNullOrEmpty(attribute?.DisplayName) ? property.Name : attribute.DisplayName;
        Description = attribute?.Description;
        Minimum = double.IsNaN(attribute?.Min ?? double.NaN) ? null : attribute!.Min;
        Maximum = double.IsNaN(attribute?.Max ?? double.NaN) ? null : attribute!.Max;
        Step = attribute is { Step: > 0 }
            ? attribute.Step
            : IsFloatingPoint(UnderlyingType) ? 0.01 : 1;

        if (UnderlyingType.IsEnum)
            Options = BuildEnumOptions(UnderlyingType, IsNullable);
        else if (!string.IsNullOrEmpty(attribute?.ItemsSource))
            Options = ResolveItemsSource(target, attribute.ItemsSource);
    }

    public string PropertyName => _property.Name;
    public string DisplayName { get; }
    public string? Description { get; }
    public Type PropertyType { get; }
    public Type UnderlyingType { get; }
    public bool IsCollection { get; }
    public bool IsNullable { get; }
    public bool IsReadOnly { get; }
    public bool IsBoolean { get; }
    public bool IsNumeric { get; }
    public double? Minimum { get; }
    public double? Maximum { get; }
    public double Step { get; }
    public IReadOnlyList<EnumOption>? Options { get; }
    public bool HasRange => Minimum.HasValue || Maximum.HasValue;
    public object? Value => _property.GetValue(_target);
    public IEnumerable? CollectionSource => IsCollection ? Value as IEnumerable : null;

    public bool TrySetValue(object? input)
    {
        if (IsReadOnly || !TryConvert(input, out var converted))
            return false;

        try
        {
            _property.SetValue(_target, Clamp(converted));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or TargetException or TargetInvocationException)
        {
            return false;
        }
    }

    public bool TryStep(double delta)
    {
        if (!IsNumeric || IsReadOnly)
            return false;

        try
        {
            var current = Value == null ? 0d : Convert.ToDouble(Value, CultureInfo.InvariantCulture);
            var stepped = Math.Round(current + delta, 6);
            return TrySetValue(stepped);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            return false;
        }
    }

    private bool TryConvert(object? input, out object? converted)
    {
        converted = null;
        if (input == null)
            return IsNullable;

        var inputType = input.GetType();
        if (UnderlyingType.IsAssignableFrom(inputType))
        {
            converted = input;
            return true;
        }

        if (UnderlyingType.IsEnum)
            return TryConvertEnum(input, out converted);

        if (TryChangeType(input, CultureInfo.InvariantCulture, out converted)
            || TryChangeType(input, CultureInfo.CurrentCulture, out converted))
            return true;

        try
        {
            var converter = TypeDescriptor.GetConverter(UnderlyingType);
            if (!converter.CanConvertFrom(inputType))
                return false;
            converted = converter.ConvertFrom(null, CultureInfo.CurrentCulture, input);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or FormatException or InvalidCastException)
        {
            return false;
        }
    }

    private bool TryConvertEnum(object input, out object? converted)
    {
        converted = null;
        if (input is string text)
        {
            if (Enum.TryParse(UnderlyingType, text, true, out converted))
                return true;

            converted = Options?.FirstOrDefault(option =>
                option.Value != null
                && string.Equals(option.DisplayName, text, StringComparison.OrdinalIgnoreCase))?.Value;
            return converted != null;
        }

        if (input is not IConvertible)
            return false;

        try
        {
            converted = Enum.ToObject(UnderlyingType, input);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidCastException or OverflowException)
        {
            return false;
        }
    }

    private bool TryChangeType(object input, CultureInfo culture, out object? converted)
    {
        try
        {
            converted = Convert.ChangeType(input, UnderlyingType, culture);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            converted = null;
            return false;
        }
    }

    private object? Clamp(object? value)
    {
        if (value == null || !IsNumeric || !HasRange)
            return value;

        var numericValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
        if (Minimum.HasValue)
            numericValue = Math.Max(Minimum.Value, numericValue);
        if (Maximum.HasValue)
            numericValue = Math.Min(Maximum.Value, numericValue);
        return Convert.ChangeType(numericValue, UnderlyingType, CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<EnumOption> BuildEnumOptions(Type enumType, bool nullable)
    {
        var options = new List<EnumOption>();
        if (nullable)
            options.Add(new EnumOption(null, "(无)"));

        foreach (var value in Enum.GetValues(enumType))
        {
            var name = Enum.GetName(enumType, value) ?? value.ToString() ?? string.Empty;
            var description = enumType.GetField(name)
                ?.GetCustomAttribute<DescriptionAttribute>()?.Description;
            options.Add(new EnumOption(value, string.IsNullOrEmpty(description) ? name : description));
        }
        return options;
    }

    private static IReadOnlyList<EnumOption>? ResolveItemsSource(object target, string memberName) =>
        target.GetType().GetProperty(memberName)?.GetValue(target) is IEnumerable<string> values
            ? values.Select(value => new EnumOption(value, value)).ToList()
            : null;

    private static bool IsFloatingPoint(Type type) =>
        type == typeof(float) || type == typeof(double) || type == typeof(decimal);
}
