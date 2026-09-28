using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// ⛔ 历史坑（P2 · 2026-09-28 收口）：本文件原先声明 namespace Common.StreamEX.Json ——
//    全仓**只有这一个文件**声明 Common.*，却给 Vision.Core 的 ITool/ToolMge 提供 JSON 存盘能力，
//    命名上完全看不出它属于 Vision.Core。P2 按《Vision_Architecture_Optimization_Plan v1.2》§5.1
//    收回 Vision.Core.Serialization（目录 StreamEx → Serialization，folder 与 namespace 对齐）。
//    ⚠️ JSON 文本格式**零变化**：本次只改命名空间，不动 JsonSerializerOptions / 字段名。
namespace Vision.Core.Serialization
{
    public static class JsonHelper
    {

        private static string FileOf(string path,string readerName) => Path.Combine(path, $"{readerName}.json");
        private static string BackupOf(string path,string readerName) => Path.Combine(path, $"{readerName}.bak.json");

        private static JsonSerializerOptions mJsonSettings = null;

        private static JsonSerializerOptions mBaseSettings = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            // 允许多态判别符（如 ROIType / XLDType）出现在 JSON 对象任意位置，
            // 兼容旧格式文件（判别符在末尾）无需重存
            AllowOutOfOrderMetadataProperties = true
        };

        public static void SetJsonSetting(Type[] types, string[] namespaces)
        {
            mJsonSettings = new JsonSerializerOptions
            {
                WriteIndented = true,
                IncludeFields = true,
                AllowOutOfOrderMetadataProperties = true
            };
        }


        /// <summary>
        /// 序列化对象为 JSON 字符串
        /// </summary>
        /// <typeparam name="T">转换对象类型</typeparam>
        /// <param name="obj">转换对象</param>
        /// <returns></returns>
        public static string Serialize<T>(T obj, JsonSerializerOptions jsonSettings = null)
        {
            jsonSettings ??= mJsonSettings ?? mBaseSettings;
            // 必须用运行时类型序列化，否则 STJ 只按声明类型（基类 Tool）契约输出，派生类属性（如 ImagePath、SearchRegions、训练参数）全部丢失
            return JsonSerializer.Serialize(obj, obj.GetType(), jsonSettings);
        }

        /// <summary>
        /// 将对象序列化并保存到文件
        /// </summary>
        /// <typeparam name="T">转换对象类型</typeparam>
        /// <param name="obj">转换对象</param>
        /// <param name="filePath">文件地址</param 
        public static void SerializeToFile<T>(T obj, string filePath, string fileName, JsonSerializerOptions jsonSettings = null)
        {
            var path = FileOf(filePath, fileName);
            var bak = BackupOf(filePath, fileName);
            var tmp = path + ".tmp";
            string jsonString = string.Empty;

            if (jsonSettings != null)
            {
                jsonString = Serialize(obj, jsonSettings);
            }
            else
            {
                jsonString = Serialize(obj);
            }


            if (jsonString != null)
            {
                Directory.CreateDirectory(filePath);
                File.WriteAllText(tmp, jsonString);
                if (File.Exists(path))
                {
                    // 用 Replace 保留备份
                    File.Replace(tmp, path, bak, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tmp, path);
                }

                Console.WriteLine($"数据已保存到文件: {filePath}");
            }
        }



        /// <summary>
        /// 反序列化 JSON 字符串为对象
        /// </summary>
        /// <typeparam name="T">转换对象类型</typeparam>
        /// <param name="jsonString">json字符串</param>
        /// <returns></returns>
        public static object Deserialize(string jsonString, Type tp, JsonSerializerOptions inputSetting = null)
        {
            inputSetting ??= mJsonSettings ?? mBaseSettings;
            return JsonSerializer.Deserialize(jsonString, tp, inputSetting);
        }

        public static object DeserializeFromFile(string filePath, Type tp, JsonSerializerOptions inputSetting = null)
        {
            string jsonString = File.ReadAllText(filePath);
            return Deserialize(jsonString, tp, inputSetting);
        }

 

        /// <summary>
        /// 反序列化 JSON 字符串为对象
        /// </summary>
        /// <typeparam name="T">转换对象类型</typeparam>
        /// <param name="jsonString">json字符串</param>
        /// <returns></returns>
        public static T Deserialize<T>(string jsonString, JsonSerializerOptions inputSetting = null)
        {
            inputSetting ??= mJsonSettings ?? mBaseSettings;
            return JsonSerializer.Deserialize<T>(jsonString, inputSetting);
        }


        /// <summary>
        /// 从文件中读取 JSON 并反序列化为对象
        /// </summary>
        /// <typeparam name="T">转换对象类型</typeparam>
        /// <param name="filePath">文件地址</param>
        /// <returns></returns>
        public static T DeserializeFromFile<T>(string filePath, string fileName, JsonSerializerOptions inputSetting = null)
        {
            try
            {
                var path = FileOf(filePath, fileName);
                string jsonString = File.ReadAllText(path);
                return Deserialize<T>(jsonString, inputSetting);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取文件失败: {ex.Message}");
                return default;
            }
        }


        /// <summary>
        /// 从文件中读取 JSON 并反序列化为对象
        /// </summary>
        /// <typeparam name="T">转换对象类型</typeparam>
        /// <param name="filePath">文件地址</param>
        /// <returns></returns>
        public static T DeserializeFromFile<T>(string filePath, JsonSerializerOptions inputSetting = null)
        {
            string jsonString = File.ReadAllText(filePath);
            return Deserialize<T>(jsonString, inputSetting);
        }

 

        /// <summary>
        /// 比较两个 JSON 文件的内容是否发生变化
        /// </summary>
        /// <param name="serializeStr">序列化后的字符串</param>
        /// <param name="lastSerializedFilePath">想要比较的文件地址</param>
        /// <returns></returns>
        public static bool HasChanged(string serializeStr, string lastSerializedFilePath)
        {
            try
            {
                // 检查之前的序列化文件是否存在
                if (File.Exists(lastSerializedFilePath))
                {
                    // 读取上次保存的序列化数据
                    string lastSerialized = File.ReadAllText(lastSerializedFilePath);

                    // 比较当前序列化数据和上次的序列化数据
                    return serializeStr != lastSerialized;
                }
                else
                {
                    // 如果上次序列化文件不存在，表示第一次比较，直接保存当前数据
                    File.WriteAllText(lastSerializedFilePath, serializeStr);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"文件比较失败: {ex.Message}");
                return false;
            }
        }


        public static void AtomicWrite(string path, string content)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var tmp = path + ".tmp";


            File.WriteAllText(tmp, content, Encoding.UTF8);
            // 先写临时文件，再替换：Windows 上 Move 覆盖接近原子
            if (File.Exists(path)) File.Replace(tmp, path, path + ".bak", ignoreMetadataErrors: true);
            else File.Move(tmp, path);
        }
 
    }
}
