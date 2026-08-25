namespace Extension.Struct
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

    public enum ESecsMode
    {
        /// <summary>
        /// 离线模式
        /// </summary>
        Offline = 0,
        /// <summary>
        /// 在线本地模式
        /// </summary>
        Online_Local = 1,
        /// <summary>
        /// 在线远程模式
        /// </summary>
        Online_Remote = 2,
    }
}
