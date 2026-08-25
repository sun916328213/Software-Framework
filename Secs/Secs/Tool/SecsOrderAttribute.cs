using System;

namespace Secs.Order
{
    /// <summary>
    /// Secs指令注册特性，标注指令处理器对应的 SxFy 名称。
    /// 工厂启动时优先读取该特性，未标注时按类名规则兜底并告警。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SecsOrderAttribute : Attribute
    {
        /// <summary>
        /// 指令名称，如 "S6F11"
        /// </summary>
        public string Name { get; }

        public SecsOrderAttribute(string name)
        {
            Name = name;
        }
    }
}
