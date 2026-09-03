namespace CommunicationNew.Core.Interfaces
{
    /// <summary>
    /// 帧构建器接口 —— 模块化组装协议数据帧
    /// </summary>
    public interface IFrameBuilder
    {
        /// <summary>构建完整帧</summary>
        byte[] Build();

        /// <summary>清空，准备下一次构建</summary>
        IFrameBuilder Clear();

        /// <summary>追加头部</summary>
        IFrameBuilder AppendHeader(byte[] header);

        /// <summary>追加长度字段</summary>
        IFrameBuilder AppendLength(int length, int fieldSize = 2, bool bigEndian = true);

        /// <summary>追加载荷数据</summary>
        IFrameBuilder AppendPayload(byte[] payload);

        /// <summary>追加校验码（CRC16/累加和等）</summary>
        IFrameBuilder AppendChecksum(string algorithm = "CRC16");

        /// <summary>追加固定字节</summary>
        IFrameBuilder AppendBytes(byte[] bytes);

        /// <summary>追加结束符</summary>
        IFrameBuilder AppendDelimiter(byte[] delimiter);
    }
}
