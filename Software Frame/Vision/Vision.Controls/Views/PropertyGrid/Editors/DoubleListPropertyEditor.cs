using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Vision.Controls;

/// <summary>原 List&lt;double&gt; 的整组编辑，不把集合 ToString() 当作可编辑值。</summary>
internal sealed class DoubleListPropertyEditor : ValidationRule, IValueConverter
{
    public static TextBox Create(string propertyName, object target)
    {
        var editor = new TextBox { ToolTip = "数字以逗号分隔，使用小数点，例如 1, 1.5, 2" };
        var converter = new DoubleListPropertyEditor();
        var binding = new Binding(propertyName)
        {
            Source = target, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            NotifyOnSourceUpdated = true, Converter = converter
        };
        binding.ValidationRules.Add(converter);
        editor.SetBinding(TextBox.TextProperty, binding);
        return editor;
    }
    private static bool TryParse(string text, out List<double> values)
    {
        values = new();
        foreach (var token in (text ?? "").Split(new[] { ',', ';', '，', '；', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) return false;
            values.Add(number);
        }
        return true;
    }
    public override ValidationResult Validate(object value, CultureInfo cultureInfo) =>
        TryParse(value as string, out _) ? ValidationResult.ValidResult : new ValidationResult(false, "请输入逗号分隔的有限数字，使用小数点。");
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is IEnumerable<double> values ? string.Join(", ", values.Select(v => v.ToString("R", CultureInfo.InvariantCulture))) : "";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        TryParse(value as string, out var values) ? values : DependencyProperty.UnsetValue;
}
