// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：ESecsMode。成员说明可结合下方具体实现阅读。
namespace Extension.Automation.Secs
{
    /// <summary>
    /// ESecsMode 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// </summary>
    public enum ESecsMode
    {
        Offline = 0,
        Online_Local = 1,
        Online_Remote = 2,
    }
}