using Communication.Core.Definition;
using Communication.Communication.Socket;
using Communication.Tools.Stream.FrameSplitter;
using Communication.Tools.Stream.FrameSplitter.Splitter;
using Extension.Core.TaskEx;
using HPSocket;
using HPSocket.Adapter;
using HPSocket.Base;
using HPSocket.Sdk;
using HPSocket.Tcp;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Communication.Communication.Socket.Definition;


namespace Communication.Communication.Socket.HPSocket
{
    /// <summary>
    /// 简单的 TCP 网口实现
    /// </summary>
    public class HPSocketClientHelper : Communication.Socket.SocketBase
    {
        public HPSocketClientHelper()
        {
            mClient = new TcpClient();
            // 绑定事件
            mClient.OnConnect += OnClientConnect;
            mClient.OnReceive += OnClientReceive;
            mClient.OnClose += OnClientClose;

            mFrameSplitter.FrameCompleted += OnFrameCompleted;
        }

        public HPSocketClientHelper(string ip, ushort port) : this()
        {
            SocketParam.RemoteIP = ip;
            SocketParam.RemotePort = port;
        }

        private readonly TcpClient mClient = null;

        // 保证一次只发一个指令，防止串包
        private readonly SemaphoreSlim mSendLock = new SemaphoreSlim(1, 1);
        //  异步,将收到的数据“路由”回 WriteRequestAsync 的等待处
        private volatile TaskCompletionSource<byte[]> mCurrentRequestTcs;
 
        SocketParam mSocketParam=new SocketParam("127.0.0.1",502);

        IFrameSplitter mFrameSplitter = ModuleSplitterPresets.Delimited(new byte[] { 0x0d, 0x0a });  

        public override event Action<ICommPort, ReadOnlyMemory<byte>> OnDataReceived;
        public override event Action<ICommPort> OnDisconnect;
        public override event Action<ICommPort> OnConnect;

 
        public override SocketParam SocketParam  { get { return mSocketParam; } set { mSocketParam = value; } }

        public override PortType PortType => PortType.Ethernet;

        public override string Name => $"{mSocketParam.RemoteIP}:{SocketParam.RemotePort}";

        public override bool IsOpen { get { return mClient == null ? false : mClient.IsConnected; } }

        public override IFrameSplitter FrameSplitter { 
            set 
            {
                mFrameSplitter.FrameCompleted -= OnFrameCompleted;
                mFrameSplitter = value;
                mFrameSplitter.Reset();
                mFrameSplitter.FrameCompleted += OnFrameCompleted;
            }
        }

        private HandleResult OnClientConnect(IClient sender)
        {
            OnConnect?.Invoke(this);
            return HandleResult.Ok;
        }

        private HandleResult OnClientClose(IClient sender, SocketOperation so, int errorCode)
        {
            OnDisconnect?.Invoke(this);
            return HandleResult.Ok;
        }

        private HandleResult OnClientReceive(IClient sender, byte[] data)
        {
            // 由分包器来判断何时构成“完整一帧”
            mFrameSplitter.Feed(data);
            return HandleResult.Ok;
        }

        /// <summary>
        /// 分包器判定“完成一帧”后触发
        /// </summary>
        private void OnFrameCompleted(ReadOnlyMemory<byte> frame)
        {
            if (frame.Length == 0) return;
            // 路由
            if (mCurrentRequestTcs != null && !mCurrentRequestTcs.Task.IsCompleted)
            {
                // WriteRequestAsync 正在等回复 -> 填坑，唤醒 await
               bool b =  mCurrentRequestTcs.TrySetResult(frame.ToArray());
            }
            else
            {
                // 抛给外部事件
                OnDataReceived?.Invoke(this, frame);
            }
        }

        public override void Disconnect()
        {
            if (IsOpen)
            {
                mClient.Stop();
            }
        }

        public override bool Connect()
        {
            if (IsOpen) return true;

            
            bool startOk = mClient.Connect(mSocketParam.RemoteIP, mSocketParam.RemotePort);
            if (!startOk)
                return false;

            // 简单等待，防止立即调用 Write 失败
            int waitMs = 500;
            int start = Environment.TickCount;

            while (!IsOpen && Environment.TickCount - start < waitMs)
            {
                Thread.Sleep(10);
            }

            return IsOpen;
        }

        public override bool ReConnect()
        {
            Disconnect();
            return Connect();
        }

        public override async Task<byte[]> WriteRequestAsync(byte[] buffer, int timeoutMs = 1000)
        {
            if (!IsOpen && !Connect())
                return null;
            // 加锁：防止多线程同时发送指令导致数据错乱
            await mSendLock.WaitAsync();

            try
            {
                //创建一个 TCS 等待结果
                // RunContinuationsAsynchronously 确保后续代码不在 socket 回调线程执行，防止死锁
                mCurrentRequestTcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

                // 发送数据
                if (!mClient.Send(buffer, buffer.Length)) return null;
                return await mCurrentRequestTcs.Task.WithTimeout(timeoutMs);
            }
            catch
            {
                mCurrentRequestTcs.TrySetCanceled(); // 超时后作废
                return null;
            }
            finally
            {
                mCurrentRequestTcs = null;
                mSendLock.Release();
            }
        }

        public override async Task<bool> WriteOnlyAsync(byte[] buffer)
        {
            if (!IsOpen && !Connect())
                return false;
            await mSendLock.WaitAsync();
            try
            {
                bool ok = mClient.Send(buffer, buffer.Length);
                if (!ok)
                {
                    return false;
                }
                return true;
            }
            finally 
            {
                mSendLock.Release();
            }
        }

        public override void Dispose()
        {
            Disconnect();
            mSendLock.Dispose();
        }
    }
}
