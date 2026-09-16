using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.EnumEx
{
    /// <summary>
    /// 枚举帮助类，提供获取枚举描述等方法
    /// </summary>
    public static class EnumEx
    {
        /// <summary>
        /// 获取枚举值的描述信息
        /// </summary>
        /// <typeparam name="T">枚举类型</typeparam>
        /// <param name="value">枚举值</param>
        /// <returns>枚举值的描述信息，如果没有描述则返回枚举值的名称</returns>
        public static string GetDescription<T>(this T value) where T : Enum
        {
            return GetDescription((Enum)(object)value);
        }

        /// <summary>
        /// 获取枚举值的描述信息
        /// </summary>
        /// <param name="value">枚举值</param>
        /// <returns>枚举值的描述信息，如果没有描述则返回枚举值的名称</returns>
        public static string GetDescription(this Enum value)
        {
            if (value == null)
                return string.Empty;

            Type enumType = value.GetType();
            string enumName = Enum.GetName(enumType, value);

            if (enumName == null)
                return string.Empty;

            FieldInfo fieldInfo = enumType.GetField(enumName);
            if (fieldInfo == null)
                return enumName;

            DescriptionAttribute[] attributes =
                (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);

            return attributes.Length > 0 ? attributes[0].Description : enumName;
        }

        /// <summary>
        /// 获取Enum集合
        /// </summary>
        /// <returns></returns>
        public static string[] GetName<T>() where T : Enum
        {
            return Enum.GetNames(typeof(T));
        }

        /// <summary>
        /// 根据Name获取Enum
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static T GetEnum<T>(this string name) where T : Enum
        {
            return (T)Enum.Parse(typeof(T), name);
        }

        /// <summary>
        /// 获取枚举类型的所有值及其描述信息
        /// </summary>
        /// <returns>枚举值和描述信息的数组</returns>
        public static (T Value, string Description)[] GetAllDescriptions<T>() where T : Enum
        {
            T[] values = Enum.GetValues(typeof(T)) as T[];
            var result = new (T Value, string Description)[values.Length];

            for (int i = 0; i < values.Length; i++)
            {
                result[i] = (values[i], GetDescription(values[i]));
            }

            return result;
        }
    }
}

