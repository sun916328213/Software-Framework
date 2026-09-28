namespace CommunicationNew.Core.Interfaces
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
