using Extension.Core.EnumEx;
using Mapster;
using System.ComponentModel;

namespace Extension.Struct
{

    /// <summary>
    /// Log日志等级
    /// </summary>
    public enum ELogLevel
    {
        [Description("信息")]
        Information,
        [Description("警告")]
        Warning,
        [Description("警告复位")]
        Reset,
        [Description("错误")]
        Error,
        [Description("系统错误")]
        SysError,
    }

    /// <summary>
    /// Log日志信息
    /// </summary>
    public struct LogInfo
    {
        public string Time { get; set; }
        public string Info { get; set; }
        public ELogLevel Level { get; set; }

        public LogInfo(string time, string info, ELogLevel level = ELogLevel.Information)
        {
            Time = time;
            Info = info;
            Level = level;
        }
    }

    public struct LogInfoDTO
    {
        public string Time { get; set; }
        public string Info { get; set; }
        public string Level { get; set; }
    }

    
    /// <summary>
    /// SLogInfo相关映射配置
    /// </summary>
    public static class LogInfoMapping
    {
        static LogInfoMapping()
        {
            Configure(TypeAdapterConfig.GlobalSettings);
        }

        /// <summary>
        /// 配置SLogInfo和SLogInfoDTO之间的映射关系
        /// </summary>
        /// <param name="config">映射配置对象</param>
        public static void Configure(TypeAdapterConfig config)
        {
            // 配置从SLogInfo到SLogInfoDTO的映射
            config.ForType<LogInfo, LogInfoDTO>()
                .Map(dest => dest.Level, src => src.Level.ToString());

            // 从SLogInfoDTO到SLogInfo的映射
            config.ForType<LogInfoDTO, LogInfo>()
                .Map(dest => dest.Level, src => src.Level.GetEnum<ELogLevel>());
        }
    }
}
