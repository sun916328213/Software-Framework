using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.Reflection
{
    public   class ReflectionEx
    {
        /// <summary>
        /// 使用反射设置对象的单个属性值
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">要设置属性的对象</param>
        /// <param name="propertyName">属性名称</param>
        /// <param name="value">要设置的值</param>
        /// <exception cref="ArgumentNullException">当obj为null时抛出</exception>
        /// <exception cref="ArgumentException">当属性不存在或不可写时抛出</exception>
        public   void SetProperty<T>(T obj, string propertyName, object value)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentException("属性名称不能为空", nameof(propertyName));

            // 获取对象的类型
            Type type = typeof(T);

            // 获取指定名称的属性
            PropertyInfo property = type.GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.Instance);

            // 检查属性是否存在且可写
            if (property == null)
                throw new ArgumentException($"类型 {type.Name} 中不存在属性 {propertyName}", nameof(propertyName));

            if (!property.CanWrite)
                throw new ArgumentException($"属性 {propertyName} 不可写", nameof(propertyName));

            // 设置属性值
            SetPropertyValue(obj, property, value);
        }

        /// <summary>
        /// 使用反射设置对象的多个属性值
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">要设置属性的对象</param>
        /// <param name="propertyValues">属性名和值的字典</param>
        public   void SetProperties<T>(  T obj, Dictionary<string, object> propertyValues)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            if (propertyValues == null || propertyValues.Count == 0)
                return;

            Type type = typeof(T);

            foreach (var kvp in propertyValues)
            {
                string propertyName = kvp.Key;
                object value = kvp.Value;

                // 获取指定名称的属性
                PropertyInfo property = type.GetProperty(propertyName,
                    BindingFlags.Public | BindingFlags.Instance);

                // 检查属性是否存在且可写
                if (property != null && property.CanWrite)
                {
                    // 设置属性值
                    SetPropertyValue(obj, property, value);
                }
            }
        }

        /// <summary>
        /// 使用反射创建对象并设置属性值
        /// </summary>
        /// <typeparam name="T">要创建的对象类型</typeparam>
        /// <returns>创建并设置了属性值的对象</returns>
        public   T CreateInstance<T>() where T : new()
        {
            return new T();
        }

        /// <summary>
        /// 使用反射创建对象并设置属性值
        /// </summary>
        /// <typeparam name="T">要创建的对象类型</typeparam>
        /// <param name="propertyValues">属性名和值的字典</param>
        /// <returns>创建并设置了属性值的对象</returns>
        public   T CreateInstance<T>(Dictionary<string, object> propertyValues) where T : new()
        {
            T obj = new T();
            SetProperties(obj, propertyValues);
            return obj;
        }

        /// <summary>
        /// 获取对象的属性值
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">要获取属性值的对象</param>
        /// <param name="propertyName">属性名称</param>
        /// <returns>属性值</returns>
        public   object GetProperty<T>(T obj, string propertyName)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentException("属性名称不能为空", nameof(propertyName));

            Type type = typeof(T);
            PropertyInfo property = type.GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.Instance);

            if (property == null)
                throw new ArgumentException($"类型 {type.Name} 中不存在属性 {propertyName}", nameof(propertyName));

            return property.GetValue(obj);
        }

        /// <summary>
        /// 获取对象的所有公共属性及其值
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">要获取属性值的对象</param>
        /// <returns>属性名和值的字典</returns>
        public   Dictionary<string, object> GetProperties<T>(T obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            var properties = new Dictionary<string, object>();
            Type type = typeof(T);

            PropertyInfo[] propertyInfos = type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo property in propertyInfos)
            {
                properties[property.Name] = property.GetValue(obj);
            }

            return properties;
        }

        #region 私有辅助方法

        /// <summary>
        /// 设置属性值，包含类型转换逻辑
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">要设置属性的对象</param>
        /// <param name="property">属性信息</param>
        /// <param name="value">要设置的值</param>
        private   void SetPropertyValue<T>(T obj, PropertyInfo property, object value)
        {
            try
            {
                object convertedValue = ConvertValue(value, property.PropertyType);
                property.SetValue(obj, convertedValue);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"无法设置属性 {property.Name} 的值: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 转换值到目标类型
        /// </summary>
        /// <param name="value">原始值</param>
        /// <param name="targetType">目标类型</param>
        /// <returns>转换后的值</returns>
        private   object ConvertValue(object value, Type targetType)
        {
            // 如果值为null且目标类型是值类型，返回默认值
            if (value == null)
            {
                if (targetType.IsValueType)
                {
                    return Activator.CreateInstance(targetType);
                }
                return null;
            }

            // 如果类型匹配，直接返回
            if (targetType.IsAssignableFrom(value.GetType()))
            {
                return value;
            }

            // 处理可空类型
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                targetType = Nullable.GetUnderlyingType(targetType);
            }

            // 特殊类型转换
            if (targetType == typeof(string))
            {
                return value.ToString();
            }

            if (targetType == typeof(int))
            {
                if (value is string stringValue)
                    return int.Parse(stringValue);
                return Convert.ToInt32(value);
            }

            if (targetType == typeof(long))
            {
                if (value is string stringValue)
                    return long.Parse(stringValue);
                return Convert.ToInt64(value);
            }

            if (targetType == typeof(double))
            {
                if (value is string stringValue)
                    return double.Parse(stringValue);
                return Convert.ToDouble(value);
            }

            if (targetType == typeof(decimal))
            {
                if (value is string stringValue)
                    return decimal.Parse(stringValue);
                return Convert.ToDecimal(value);
            }

            if (targetType == typeof(DateTime))
            {
                if (value is string stringValue)
                    return DateTime.Parse(stringValue);
                return Convert.ToDateTime(value);
            }

            if (targetType == typeof(bool))
            {
                if (value is string stringValue)
                    return bool.Parse(stringValue);
                return Convert.ToBoolean(value);
            }

            // 尝试使用Convert.ChangeType
            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch (Exception ex) when (ex is InvalidCastException
                                          or FormatException
                                          or OverflowException
                                          or ArgumentNullException)
            {
                // 无法转换时返回原始值，由调用方决定如何处理，这个回退语义保留。
                // 但只吞"类型不匹配/格式/溢出"这一类，其余异常（如空引用）说明调用有缺陷，必须暴露。
                Debug.WriteLine($"[ReflectionEx] 无法将值转换为 {targetType?.Name}，返回原始值：{ex.Message}");
                return value;
            }
        }

        #endregion
    }
}
