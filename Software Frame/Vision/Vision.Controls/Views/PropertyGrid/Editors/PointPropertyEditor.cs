using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;
using Vision.Core.Geometry;

namespace Vision.Controls;

/// <summary>通用 Point2D 编辑器；每次仅替换一个分量，另一个读取当前 DTO，避免旧值覆盖。</summary>
internal static class PointPropertyEditor
{
    public static FrameworkElement Create(PropertyInfo property, object target)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (var axis = 0; axis < 2; axis++)
        {
            var stack = new StackPanel();
            var label = new TextBlock { Text = axis == 0 ? "X（列）" : "Y（行）" };
            label.SetResourceReference(FrameworkElement.StyleProperty, "Vision.PropertyPanel.CoordinateLabel");
            stack.Children.Add(label);
            var editor = new DecimalUpDown
            { ValueStep = 0.001m, Minimum = decimal.MinValue, Maximum = decimal.MaxValue };
            View.VisionPropertyPanel.ApplyEditorStyle(editor);
            editor.SetBinding(DecimalUpDown.ValueProperty, new Binding(property.Name)
            {
                Source = target, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                NotifyOnSourceUpdated = true, ValidatesOnExceptions = true,
                Converter = new CoordinateConverter(property, target, axis == 0)
            });
            stack.Children.Add(editor);
            Grid.SetColumn(stack, axis);
            grid.Children.Add(stack);
        }
        return grid;
    }

    private sealed class CoordinateConverter(PropertyInfo property, object target, bool isX) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Point2D point ? System.Convert.ToDecimal(isX ? point.X : point.Y) : DependencyProperty.UnsetValue;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value == DependencyProperty.UnsetValue) return Binding.DoNothing;
            var current = (Point2D)property.GetValue(target);
            var coordinate = System.Convert.ToDouble(value, culture);
            return isX ? new Point2D(coordinate, current.Y) : new Point2D(current.X, coordinate);
        }
    }
}
