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