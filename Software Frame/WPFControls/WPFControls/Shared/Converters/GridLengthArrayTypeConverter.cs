// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：GridLengthArrayTypeConverter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、System、System.ComponentModel、System.Globalization、System.Windows。导入不代表所有类型都会在运行时加载。
using WPFControls;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;

namespace WPFControls.Controls.Converters
{
    /// <summary>
    /// GridLength数组的类型转换器，支持从字符串转换
    /// </summary>
    public class GridLengthArrayTypeConverter : TypeConverter
    {
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 CanConvertFrom。
        /// 正文中的关键协作调用包括 CanConvertFrom；具体条件和先后次序见方法体。
        /// </summary>
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string))
                return true;
            return base.CanConvertFrom(context, sourceType);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ConvertFrom。
        /// 正文中的关键协作调用包括 Split、Trim、Equals、EndsWith、TryParse；具体条件和先后次序见方法体。
        /// </summary>
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