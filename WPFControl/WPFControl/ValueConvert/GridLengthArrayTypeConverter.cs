using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;

namespace WPFControl.ValueConvert
{
    /// <summary>
    /// GridLength数组的类型转换器，支持从字符串转换
    /// </summary>
    public class GridLengthArrayTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string))
                return true;
            return base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string stringValue)
            {
                var parts = stringValue.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                var gridLengths = new GridLength[parts.Length];
                
                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i].Trim();
                    if (part.Equals("*"))
                    {
                        gridLengths[i] = new GridLength(1, GridUnitType.Star);
                    }
                    else if (part.EndsWith("*"))
                    {
                        if (double.TryParse(part.Substring(0, part.Length - 1), out double starValue))
                        {
                            gridLengths[i] = new GridLength(starValue, GridUnitType.Star);
                        }
                        else
                        {
                            gridLengths[i] = new GridLength(1, GridUnitType.Star);
                        }
                    }
                    else if (double.TryParse(part, out double pixelValue))
                    {
                        gridLengths[i] = new GridLength(pixelValue);
                    }
                    else
                    {
                        gridLengths[i] = new GridLength(1, GridUnitType.Auto);
                    }
                }
                
                return gridLengths;
            }
            
            return base.ConvertFrom(context, culture, value);
        }
    }
}