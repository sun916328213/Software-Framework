using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows.Data;

namespace Vision.Controls.Wpf.Converter
{
    public class PropertiesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return null;

            var props = value.GetType()
                .GetProperties()
                .Where(p => p.CanRead &&
                            p.GetCustomAttributes(typeof(BrowsableAttribute), true)
                             .All(a => ((BrowsableAttribute)a).Browsable))
                .Select(p => new KeyValuePair<string, string>
                (
                    p.Name,
                    GetStringValue(p, value)
                ))
                .ToList();

            return props;
        }

        private static string GetStringValue(PropertyInfo p, object value)
        {
            try
            {
                return p.GetValue(value)?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
