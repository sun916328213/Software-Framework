// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IFrameSplitter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core。导入不代表所有类型都会在运行时加载。
using Communication.Core;
namespace Communication.Core.Interfaces
{
    /// <summary>
    /// 帧拆分器接口 —— 从接收字节流中切分出完整帧
    /// </summary>
    public interface IFrameSplitter
    {
        /// <summary>尝试从缓冲区中拆分出一个完整帧</summary>
        /// <param name="buffer">接收缓冲区</param>
        /// <param name="frame">拆分出的完整帧</param>
        /// <param name="consumed">消耗的字节数</param>
        /// <returns>是否成功拆分</returns>
        bool TrySplit(ReadOnlySpan<byte> buffer, out byte[] frame, out int consumed);
    }
}
