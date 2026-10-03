// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：EDeviceStatus。成员说明可结合下方具体实现阅读。
namespace Extension.Automation.Device
{
    /// <summary>
    /// 设备状态  
    /// </summary>
    public enum EDeviceStatus
    {
        /// <summary>  
        /// 空闲  
        /// </summary>  
        Idle = 0,
        /// <summary>
        /// 就绪
        /// </summary>
        Ready = 1,
        /// <summary>  
        /// 运行中  
        /// </summary>  
        Running = 2,
        /// <summary>  
        /// 暂停  
        /// </summary>  
        Paused = 3,
        /// <summary>  
        /// 错误  
        /// </summary>  
        Error = 4,
    }
}