using System.Collections.Generic;
using System.Threading.Tasks;

namespace Secs
{
    /// <summary>
    /// 入站事件回调接口（结构参照 SecsGem.NetCore 的 ISecsGemServerEventHandler）。
    /// 业务方实现该接口并通过 SecsControl.SetEvents 注册，
    /// 即可收到 Host 主动发起的消息，替代旧的 ActiveReceive 轮询方式。
    /// </summary>
    public interface ISecsControlEvents
    {
        /// <summary>
        /// 收到远程命令（S2F41）。实现中设置 e.Return（HCACK 码）作为自动回包 S2F42 的结果。
        /// 默认 HCACK=0 表示命令已接受。
        /// </summary>
        Task OnRemoteCommandAsync(RemoteCommandArgs e);
    }

    /// <summary>
    /// 远程命令参数（S2F41）
    /// </summary>
    public sealed class RemoteCommandArgs
    {
        /// <summary>
        /// RCMD 命令名称，如 "START"
        /// </summary>
        public string RCMD { get; set; }

        /// <summary>
        /// 命令参数集合 Key=CPNAME Value=值
        /// </summary>
        public Dictionary<string, string> Params { get; } = new Dictionary<string, string>();

        /// <summary>
        /// HCACK 回码：0=接受 1=不接受 2=参数无效 3=状态不允许 4-63=保留，业务方可设置
        /// </summary>
        public byte Return { get; set; }
    }
}
