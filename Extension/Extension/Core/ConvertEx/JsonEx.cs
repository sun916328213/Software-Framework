using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
 
namespace Extension.Core.ConvertEx
{
    public static class JsonEx
    {
        private static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions()
        {
            //中文串编码
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            //缩进 换行 方便阅读
            WriteIndented = true,
            //数字类型自动转换（string ↔ int 互转不报错）
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
        };

        /// <summary>
        /// 将对象序列化为Json字符串
        /// </summary>
        /// <param name="pData">待序列化的对象</param>
        /// <returns>序列化后的Json字符串，如果序列化失败返回null</returns>
        public static string ToJson<T>(this T pData)
        {
            if (pData == null) return null;
            return JsonSerializer.Serialize(pData, DefaultOptions);
        }
       

        /// <summary>
        /// 以Json格式保存到本地
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="data">数据</param>
        /// <param name="filePath">文件路径</param>
        public static void Save(this object data, string filePath )
        {
            System.IO.File.WriteAllText(filePath, data.ToJson());
        }

        /// <summary>
        /// 以Json格式保存到本地
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="pData">数据</param>
        /// <param name="pFilePath">文件路径</param>
        /// <param name="pName">文件名称</param>
        public static void Save(this object pData, string pFilePath, string pName)
        {
            string path = Path.Combine(pFilePath, pName);
            Save(pData, path);  
        }

        /// <summary>
        /// 将Json字符串反序列化为指定类型的对象
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="json">待反序列化的Json字符串</param>
        /// <returns>反序列化后的对象，如果反序列化失败返回默认值</returns>
        public static T FromJson<T>(this string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return default;
            return JsonSerializer.Deserialize<T>(json, DefaultOptions);
        }

        /// <summary>
        /// 以Json格式 从本地读取
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="filePath">文件路径</param>
        /// <returns>反序列化后的对象，如果文件不存在则返回默认值</returns>
        public static T Read<T>(string filePath )
        {
            if (System.IO.File.Exists(filePath))
            {
                string data = System.IO.File.ReadAllText(filePath);
                return data.FromJson<T>();
            }
            // 使用default而不是new T()，因为default适用于所有类型（包括没有无参构造函数的类型）
            return default(T);
        }

        /// <summary>
        /// 以Json格式 从本地读取
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="pFilePath">文件路径</param>
        /// <param name="pName">文件名称</param>
        /// <returns>反序列化后的对象，如果文件不存在则返回默认值</returns>
        public static T Read<T>(string pFilePath, string pName)
        {
            string path = Path.Combine(pFilePath, pName);
            return Read<T>(path);
        }

        /// <summary>
        /// 将静态类保存转Json
        /// </summary>
        /// <param name="staticType">静态类的类型</param>
        public static string ToJson(this Type staticType)
        {
            // 创建一个字典包含所有需要保存的属性
            var data = new Dictionary<string, object>();
            var properties = staticType.GetProperties(BindingFlags.Static | BindingFlags.Public);

            foreach (var prop in properties)
            {
                data[prop.Name] = prop.GetValue(null);
            }
            return JsonSerializer.Serialize(data, DefaultOptions);
        }

        /// <summary>
        /// 将静态类保存到JSON文件
        /// </summary>
        /// <param name="staticType">静态类的类型</param>
        /// <param name="filePath">文件路径</param>
        public static void SaveStaticClass (Type staticType, string filePath)
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 创建一个字典包含所有需要保存的属性
            var data = new Dictionary<string, object>();
            var properties = staticType.GetProperties(BindingFlags.Static | BindingFlags.Public);

            foreach (var prop in properties)
            {
                data[prop.Name] = prop.GetValue(null);
            }

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        /// <summary>
        /// 从JSON文件加载静态类数据
        /// </summary>
        /// <param name="staticType">静态类的类型</param>
        /// <param name="filePath">文件路径</param>
        public static void LoadStaticClass(Type staticType, string filePath)
        {
            if(!System.IO.File.Exists(filePath))
                return;
            string json = System.IO.File.ReadAllText(filePath);
            
            // 使用 JsonDocument 来解析，保留更多类型信息
            var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            
            if (root.ValueKind != JsonValueKind.Object)
                return;

            // 使用反射自动设置所有公共静态属性
            var properties = staticType.GetProperties(BindingFlags.Static | BindingFlags.Public);

            foreach (var prop in properties)
            {
                if (root.TryGetProperty(prop.Name, out JsonElement element))
                {
                    try
                    {
                        var value = DeserializeJsonElement(element, prop.PropertyType);
                        prop.SetValue(null, value);
                    }
                    catch
                    {
                        // 转换失败时保持当前值
                    }
                }
            }
        }
        
        /// <summary>
        /// 从 JsonElement 反序列化数据，支持复杂类型和集合
        /// </summary>
        /// <param name="element">JsonElement</param>
        /// <param name="targetType">目标类型</param>
        /// <returns>反序列化后的对象</returns>
        private static object DeserializeJsonElement(JsonElement element, Type targetType)
        {
            // 处理可空类型
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                targetType = Nullable.GetUnderlyingType(targetType);
            }
        
            // 根据 JsonElement 的类型进行处理
            switch (element.ValueKind)
            {
                case JsonValueKind.Null:
                    return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        
                case JsonValueKind.String:
                    if (targetType == typeof(string))
                        return element.GetString();
                    if (targetType == typeof(DateTime) && DateTime.TryParse(element.GetString(), out DateTime dt))
                        return dt;
                    if (targetType == typeof(Guid) && Guid.TryParse(element.GetString(), out Guid g))
                        return g;
                    return System.Convert.ChangeType(element.GetString(), targetType);
        
                case JsonValueKind.Number:
                    if (targetType == typeof(int))
                        return element.GetInt32();
                    if (targetType == typeof(long))
                        return element.GetInt64();
                    if (targetType == typeof(float))
                        return element.GetSingle();
                    if (targetType == typeof(double))
                        return element.GetDouble();
                    if (targetType == typeof(decimal))
                        return element.GetDecimal();
                    return System.Convert.ChangeType(element.GetDouble(), targetType);
        
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetBoolean();
        
                case JsonValueKind.Object:
                    // 处理复杂对象，使用 JsonSerializer 反序列化
                    string json = element.GetRawText();
                    return JsonSerializer.Deserialize(json, targetType);
        
                case JsonValueKind.Array:
                    // 处理数组和集合
                    var listType = targetType.GetInterface("IList") != null || 
                                   (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>));
                    if (listType)
                    {
                        var elementType = targetType.IsGenericType 
                            ? targetType.GenericTypeArguments[0] 
                            : targetType.GetElementType();
                                
                        var list = Activator.CreateInstance(targetType);
                        var addMethod = targetType.GetMethod("Add");
                                
                        foreach (var item in element.EnumerateArray())
                        {
                            var convertedItem = DeserializeJsonElement(item, elementType);
                            addMethod?.Invoke(list, new[] { convertedItem });
                        }
                        return list;
                    }
                    else
                    {
                        // 其他类型的数组
                        string json1 = element.GetRawText();
                        return JsonSerializer.Deserialize(json1, targetType);
                    }
        
                default:
                    return System.Convert.ChangeType(element.ToString(), targetType);
            }
        }

        /// <summary>
        /// 将Json字符串转换为JsonNode对象
        /// </summary>
        /// <param name="pJsonData">待转换的Json字符串</param>
        /// <returns>转换后的JsonNode对象，如果转换失败返回null</returns>
        public static JsonNode ToJsonNode(this string pJsonData)
        {
            if (string.IsNullOrWhiteSpace(pJsonData)) return null;

            return JsonNode.Parse(pJsonData);
        }

        /// <summary>
        /// 将JsonNode对象转换为键值对列表
        /// </summary>
        /// <param name="jsonNode">待转换的JsonNode对象</param>
        /// <returns>转换后的键值对列表</returns>
        public static List<KeyValuePair<string, string>> ToList(this JsonNode jsonNode)
        {
            List<KeyValuePair<string, string>> JsonData = new List<KeyValuePair<string, string>>();

            if (jsonNode is JsonObject obj)
            {
                foreach (var prop in obj.ToArray())
                {
                    if (prop.Value is JsonValue)
                        JsonData.Add(new KeyValuePair<string, string>(prop.Key, prop.Value.ToString()));
                    else
                        JsonData.AddRange(ToList(prop.Value));
                }
            }
            else if (jsonNode is JsonArray arr)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is JsonValue value)
                    {
                        JsonData.Add(new KeyValuePair<string, string>(value.ToString(), value.ToString()));
                    }
                    else
                        JsonData.AddRange(ToList(arr[i]));
                }
            }
            return JsonData;
        }

        /// <summary>
        /// 在JsonNode对象中替换指定的字符串值
        /// </summary>
        /// <param name="node">待处理的JsonNode对象</param>
        /// <param name="target">需要替换的字符串值</param>
        /// <param name="replacement">替换后的JsonNode对象</param>
        public static JsonNode Replace(this JsonNode node, string target, JsonNode replacement)
        {
            if (node is JsonObject obj)
            {
                foreach (var prop in obj.ToArray())
                {
                    if (prop.Value is JsonValue val && val.ToString() == target)
                    {
                        obj[prop.Key] = replacement;
                    }
                    else
                    {
                        Replace(prop.Value, target, replacement);
                    }
                }
            }
            else if (node is JsonArray arr)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is JsonValue val && val.ToString() == target)
                    {
                        arr[i] = replacement;
                    }
                    else
                    {
                        Replace(arr[i], target, replacement);
                    }
                }
            }
            return node;
        }
    }
}
