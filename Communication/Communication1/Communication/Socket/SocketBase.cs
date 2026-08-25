using Communication.Communication.Socket.Definition;
using Communication.Core.Definition;
using Communication.Tools.Stream.FrameSplitter;
using Communication.Tools.Stream.FrameSplitter.Splitter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communication.Communication.Socket
{
    public abstract class SocketBase : ICommPort
    {
        /// <summary>
        /// TCP参数
        /// </summary>
       public abstract SocketParam SocketParam { get; set; }

        public abstract PortType PortType { get;   }

        public abstract string Name { get; }

        public abstract IFrameSplitter FrameSplitter { set; }

        public abstract bool IsOpen { get; }

        public abstract event Action<ICommPort, ReadOnlyMemory<byte>> OnDataReceived;
        public abstract event Action<ICommPort> OnConnect;
        public abstract event Action<ICommPort> OnDisconnect;
 

        /// <summary>
        /// 连接
        /// </summary>
        /// <returns></returns>
        public abstract bool Connect();
       

        /// <summary>
        /// 断开连接 
        /// </summary>
        public abstract  void Disconnect();
 

        /// <summary>
        /// 重连
        /// </summary>
        /// <returns></returns>
        public abstract bool ReConnect();

       
        public abstract Task<bool> WriteOnlyAsync(byte[] buffer);

 
        public abstract Task<byte[]> WriteRequestAsync(byte[] buffer, int timeoutMs = 1000);

        /// <summary>
        /// 释放
        /// </summary>
        public abstract void Dispose();
    }
}
