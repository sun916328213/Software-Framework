using Communication.Communication.SerialPort.Definition;
using Extension.Core.Concurrency;
using System;
using System.Linq;

namespace Communication.Communication.SerialPort
{
    /// <summary>
    /// 串口通讯基类
    /// </summary>
    public abstract class SerialBase
    {
        /// <summary>
        /// 串口参数
        /// </summary>
        public abstract SerialParam SerialParam { get; set; }
        /// <summary>
        /// 数据集合
        /// </summary>
        internal abstract ChannelHelper<byte[]> Datas { get; set; }

        internal EventHandler<byte[]> _dataReceived;
        /// <summary>
        /// 数据接收回调
        /// </summary>
        public virtual event EventHandler<byte[]> DataReceivedEvent
        {
            add
            {
                if (_dataReceived != null && _dataReceived.GetInvocationList().Contains(value))
                {
                    return;
                }
                _dataReceived += value;
            }
            remove
            {
                _dataReceived -= value;
            }
        }
        /// <summary>
        /// 是否连接
        /// </summary>
        public abstract bool IsConnect { get; }
        /// <summary>
        /// 连接
        /// </summary>
        /// <returns></returns>
        public abstract bool Connect();
        /// <summary>
        /// 断开
        /// </summary>
        /// <returns></returns>
        public abstract bool DisConnect();
        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="value">数据</param>
        public abstract void Write(string value);
        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="buffer">byte[]数据</param>
        /// <param name="offset">起始地址</param>
        /// <param name="count">地址长度</param>
        public abstract void Write(byte[] buffer, int offset, int count);
        /// <summary>
        /// 写入数据并回复
        /// </summary>
        /// <param name="vlaue">数据</param>
        /// <param name="timeOut">超时</param>
        /// <returns></returns>
        public abstract string WriteWithReply(string vlaue,int timeOut);
        /// <summary>
        /// 写入数据并回复
        /// </summary>
        /// <param name="buffer">byte[]数据</param>
        /// <param name="offset">起始地址</param>
        /// <param name="count">地址长度</param>
        /// <param name="timeOut">超时</param>
        /// <returns></returns>
        public abstract byte[] WriteWithReply(byte[] buffer, int offset, int count, int timeOut);
        /// <summary>
        /// 读取数据
        /// </summary>
        /// <returns></returns>
        public abstract string Read( );
 
    }
}
