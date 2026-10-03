// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IFrameBuilder。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core。导入不代表所有类型都会在运行时加载。
using Communication.Core;
namespace Communication.Core.Interfaces
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
