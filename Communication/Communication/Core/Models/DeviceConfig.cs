using CommunicationNew.Core.Interfaces;

namespace CommunicationNew.Core.Models
{
    /// <summary>
    /// 设备配置 —— 完整的设备配置（通信参数 + 数据点定义列表）
    /// 可从 JSON 序列化/反序列化，实现配置驱动的设备管理
    /// </summary>
    public class DeviceConfig
    {
        /// <summary>设备名称</summary>
        public string DeviceName { get; set; } = "";

        /// <summary>协议类型</summary>
        public ProtocolType ProtocolType { get; set; }

        /// <summary>角色</summary>
        public DeviceRole Role { get; set; } = DeviceRole.Client;

        /// <summary>超时时间（毫秒）</summary>
        public int TimeoutMs { get; set; } = 3000;

        /// <summary>重试次数</summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>重试间隔（毫秒）</summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>是否自动重连</summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>自动读取间隔（毫秒，0=不自动读取）</summary>
        public int AutoReadIntervalMs { get; set; } = 0;

        /// <summary>数据点定义列表</summary>
        public List<DataPointDefinition> DataPoints { get; set; } = new();

        /// <summary>协议特定参数（如 IP、端口、串口名等，以 Dictionary 形式存储）</summary>
        public Dictionary<string, object> ProtocolParams { get; set; } = new();

        /// <summary>
        /// 从 JSON 文件加载配置
        /// </summary>
        public static DeviceConfig? FromJsonFile(string path)
        {
            var json = System.IO.File.ReadAllText(path);
            return System.Text.Json.JsonSerializer.Deserialize<DeviceConfig>(json);
        }

        /// <summary>
        /// 保存为 JSON 文件
        /// </summary>
        public void SaveToJsonFile(string path)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(path, json);
        }
    }
}
