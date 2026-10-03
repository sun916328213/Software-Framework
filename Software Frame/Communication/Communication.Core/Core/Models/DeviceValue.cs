// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：DeviceValue。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core。导入不代表所有类型都会在运行时加载。
using Communication.Core;
namespace Communication.Core.Models
{
    /// <summary>
    /// 设备值 —— 封装类型化值及其元数据（时间戳、质量）
    /// </summary>
    public class DeviceValue
    {
        /// <summary>值</summary>
        public object? Value { get; set; }

        /// <summary>原始字节</summary>
        public byte[]? RawData { get; set; }

        /// <summary>数据类型</summary>
        public Interfaces.DataPointType DataType { get; set; }

        /// <summary>最后更新时间</summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>数据质量</summary>
        public Interfaces.DataQuality Quality { get; set; } = Interfaces.DataQuality.Good;

        /// <summary>数据点名称</summary>
        public string? Name { get; set; }

        /// <summary>
        /// 初始化 DeviceValue 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public DeviceValue() { }

        /// <summary>
        /// 初始化 DeviceValue 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public DeviceValue(object? value, Interfaces.DataPointType type)
        {
            Value = value;
            DataType = type;
        }

        /// <summary>获取强类型值</summary>
        /// <exception cref="InvalidCastException">当前值无法转换为目标类型。</exception>
        public T? GetValue<T>()
        {
            if (Value is T t) return t;
            if (Value != null)
            {
                try
                {
                    return (T)Convert.ChangeType(Value, typeof(T));
                }
                catch (Exception ex) when (
                    ex is InvalidCastException or FormatException or OverflowException)
                {
                    // 已有值转换失败必须显式暴露，不能用 default 冒充设备的合法采样值。
                    throw new InvalidCastException(
                        $"无法将设备值从 {Value.GetType().FullName} 转换为 " +
                        $"{typeof(T).FullName ?? typeof(T).Name}。",
                        ex);
                }
            }
            return default;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// </summary>
        public override string ToString() => $"{Name ?? "?"}: {Value} [{DataType}] @ {Timestamp:HH:mm:ss.fff}";
    }
}
