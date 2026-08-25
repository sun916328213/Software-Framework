using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.FileEx
{
    public static class CSVEx
    {  // 缓冲区大小：8KB（可根据系统内存调整，建议4KB-64KB）
        private const int BufferSize = 8192;

        // 批量处理大小：一次处理1000行再写入（可根据内存情况调整）
        private const int BatchSize = 1000;

        /// <summary>
        /// 高性能导出大型List<T>到CSV文件
        /// </summary>
        public static async Task ExportToCsvAsync<T>(IEnumerable<T> data, string filePath, bool includeHeader = true, char delimiter = ',', Encoding encoding = null)
        {
            // 参数验证
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("文件路径不能为空", nameof(filePath));

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (properties.Length == 0) throw new InvalidOperationException("没有可导出的公共属性");

            // 处理编码参数默认值
            if (encoding == null)
                encoding = Encoding.UTF8;

            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 使用带缓冲的StreamWriter，减少IO操作
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
            using (var writer = new StreamWriter(stream, encoding, BufferSize, leaveOpen: false))
            {
                // 写入标题行
                if (includeHeader)
                {
                    await WriteHeaderLineAsync(writer, properties, delimiter).ConfigureAwait(false);
                }

                // 批量处理数据，减少内存占用
                var batchBuilder = new StringBuilder(1024 * 1024); // 1MB初始容量
                int count = 0;

                foreach (var item in data)
                {
                    WriteDataLineToBuilder(batchBuilder, item, properties, delimiter);
                    count++;

                    // 达到批量大小则写入并清空缓冲区
                    if (count >= BatchSize)
                    {
                        await writer.WriteAsync(batchBuilder.ToString()).ConfigureAwait(false);
                        batchBuilder.Clear();
                        count = 0;
                    }
                }

                // 写入剩余数据
                if (count > 0)
                {
                    await writer.WriteAsync(batchBuilder.ToString()).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 高性能导出List<string>到CSV文件
        /// </summary>
        /// <param name="data">要导出的字符串列表</param>
        /// <param name="filePath">导出文件路径</param>
        /// <param name="includeHeader">是否包含标题行</param>
        /// <param name="headerName">标题名称，默认为"Value"</param>
        /// <param name="delimiter">CSV分隔符</param>
        /// <param name="encoding">文件编码</param>
        public static async Task ExportToCsvAsync_String(List<string> data, string filePath, bool includeHeader = true)
        {
            // 参数验证
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("文件路径不能为空", nameof(filePath));

            // 创建包装对象列表
            var wrappedData = data.Select(s => new StringValue { Value = s ?? string.Empty }).ToList();

            // 使用现有的ExportToCsvAsync方法
            await ExportToCsvAsync(wrappedData, filePath, false);
        }

        /// <summary>
        /// 读取CSV文件并转换为指定类型的列表
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="filePath">CSV文件路径</param>
        /// <param name="delimiter">分隔符，默认为逗号</param>
        /// <param name="hasHeaderRow">是否包含标题行</param>
        /// <returns>转换后的对象列表</returns>
        public static List<T> ReadCsv<T>(string filePath, char delimiter = ',', bool hasHeaderRow = true) where T : new()
        {
            var results = new List<T>();
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            try
            {
                // 读取所有行
                var lines = System.IO.File.ReadAllLines(filePath);
                if (lines.Length == 0)
                    return results;

                // 处理标题行
                string[] headers = null;
                int dataStartIndex = 0;

                if (hasHeaderRow)
                {
                    headers = ParseCsvLine(lines[0], delimiter);
                    dataStartIndex = 1;
                }
                else
                {
                    // 如果没有标题行，使用属性索引匹配
                    headers = properties.Select(p => p.Name).ToArray();
                }

                // 处理数据行
                for (int i = dataStartIndex; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                        continue;

                    try
                    {
                        var values = ParseCsvLine(lines[i], delimiter);
                        var item = new T();

                        // 将值映射到对象属性
                        for (int j = 0; j < headers.Length && j < values.Length; j++)
                        {
                            var property = FindProperty(properties, headers[j]);
                            if (property != null && !string.IsNullOrWhiteSpace(values[j]))
                            {
                                SetPropertyValue(item, property, values[j]);
                            }
                        }
                        results.Add(item);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"处理第 {i + 1} 行时出错: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"读取CSV文件失败: {ex.Message}", ex);
            }

            return results;
        }

        /// <summary>
        /// 字符串值包装类，用于将string类型封装为具有属性的对象
        /// </summary>
        private class StringValue
        {
            public string Value { get; set; }
        }

        /// <summary>
        /// 异步写入标题行
        /// </summary>
        private static async Task WriteHeaderLineAsync(StreamWriter writer, PropertyInfo[] properties, char delimiter)
        {
            var headerBuilder = new StringBuilder();
            for (int i = 0; i < properties.Length; i++)
            {
                if (i > 0)
                    headerBuilder.Append(delimiter);

                headerBuilder.Append(EscapeCsvValue(properties[i].Name, delimiter));
            }
            headerBuilder.AppendLine();
            await writer.WriteAsync(headerBuilder.ToString()).ConfigureAwait(false);
        }

        /// <summary>
        /// 将数据行写入StringBuilder（非线程安全，适合单线程批量处理）
        /// </summary>
        private static void WriteDataLineToBuilder<T>(StringBuilder builder, T item, PropertyInfo[] properties, char delimiter)
        {
            for (int i = 0; i < properties.Length; i++)
            {
                if (i > 0)
                    builder.Append(delimiter);

                object value = properties[i].GetValue(item);
                string stringValue = value?.ToString() ?? string.Empty;
                builder.Append(EscapeCsvValue(stringValue, delimiter));
            }
            builder.AppendLine();
        }

        /// <summary>
        /// 高效转义CSV值（减少字符串分配）
        /// </summary>
        private static string EscapeCsvValue(string value, char delimiter)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // 快速检查是否需要转义（减少不必要的字符串操作）
            bool needsQuotes = false;
            foreach (char c in value)
            {
                if (c == delimiter || c == '"' || c == '\r' || c == '\n')
                {
                    needsQuotes = true;
                    break;
                }
            }

            if (!needsQuotes)
                return value;

            // 优化引号替换：预先计算容量避免多次扩容
            int quoteCount = 0;
            foreach (char c in value)
            {
                if (c == '"') quoteCount++;
            }

            // 直接构建结果，减少中间字符串
            var result = new char[value.Length + quoteCount + 2]; // +2是前后的引号
            int index = 0;
            result[index++] = '"';

            foreach (char c in value)
            {
                if (c == '"')
                {
                    result[index++] = '"';
                    result[index++] = '"';
                }
                else
                {
                    result[index++] = c;
                }
            }

            result[index++] = '"';
            return new string(result, 0, index);
        }

        /// <summary>
        /// 解析CSV行，处理带引号的字段
        /// </summary>
        private static string[] ParseCsvLine(string line, char delimiter)
        {
            var result = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;
            bool escaped = false;

            foreach (char c in line)
            {
                if (escaped)
                {
                    currentField.Append(c);
                    escaped = false;
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == delimiter && !inQuotes)
                {
                    result.Add(currentField.ToString().Trim());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            // 添加最后一个字段
            result.Add(currentField.ToString().Trim());

            return result.ToArray();
        }

        /// <summary>
        /// 查找与标题匹配的属性（忽略大小写）
        /// </summary>
        private static PropertyInfo FindProperty(PropertyInfo[] properties, string header)
        {
            return properties.FirstOrDefault(p =>
                string.Equals(p.Name, header, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 设置对象属性值，处理类型转换
        /// </summary>
        private static void SetPropertyValue(object obj, PropertyInfo property, string value)
        {
            Type targetType = property.PropertyType;

            // 处理可空类型
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    property.SetValue(obj, null);
                    return;
                }
                targetType = Nullable.GetUnderlyingType(targetType);
            }


            // 处理枚举类型
            if (targetType.IsEnum)
            {
                property.SetValue(obj, Enum.Parse(targetType, value, true));
                return;
            }
            ///处理数组 [1,2,3,4,5]格式
            else if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var strings = value.Replace("[", "").Replace("]", "").Split(',').ToList();
                property.SetValue(obj, strings);
                return;
            }

            // 处理基础类型转换
            object convertedValue = System.Convert.ChangeType(value, targetType);
            property.SetValue(obj, convertedValue);
        }
    }
}
