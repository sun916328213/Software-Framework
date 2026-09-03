using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Communication.Socket
{
    /// <summary>
    /// Socket 通讯基础类
    /// </summary>
    public abstract class SocketBase
    {
        /// <summary>
        /// Socket 客户端
        /// </summary>
        protected TcpClient _client;

        /// <summary>
        /// 网络流
        /// </summary>
        protected NetworkStream _stream;

        /// <summary>
        /// Socket 参数
        /// </summary>
        public SocketParam SocketParam { get; set; }

        /// <summary>
        /// 是否连接
        /// </summary>
        public bool IsConnected { get; protected set; }

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;

        /// <summary>
        /// 数据接收事件
        /// </summary>
        public event EventHandler<DataReceivedEventArgs> DataReceived;

        /// <summary>
        /// 错误事件
        /// </summary>
        public event EventHandler<ErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="socketParam">Socket 参数</param>
        public SocketBase(SocketParam socketParam)
        {
            SocketParam = socketParam;
            _client = new TcpClient();
        }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        /// <returns>是否连接成功</returns>
        public virtual bool Connect()
        {
            try
            {
                if(_client==null)
                    _client = new TcpClient();
                _client.ReceiveTimeout = SocketParam.ReceiveTimeout;
                _client.SendTimeout = SocketParam.SendTimeout;
                _client.Connect(SocketParam.ServerIP, SocketParam.Port);
                _stream = _client.GetStream();
                IsConnected = true;
                OnConnectionStateChanged(new ConnectionStateChangedEventArgs(true));
                StartReceiveLoop();
                return true;
            }
            catch (Exception ex)
            {
                Disconnect();
                OnConnectionStateChanged(new ConnectionStateChangedEventArgs(false));
                OnErrorOccurred(new ErrorEventArgs(ex));
                return false;
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public virtual void Disconnect()
        {
            try
            {
                _stream?.Close();
                _client?.Close();
                _client = null;
                IsConnected = false;
                OnConnectionStateChanged(new ConnectionStateChangedEventArgs(false));
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs(ex));
            }
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="data">要发送的数据</param>
        /// <returns>是否发送成功</returns>
        public virtual bool Send(byte[] data)
        {
            try
            {
                if (!IsConnected)
                {
                    return false;
                }

                _stream.Write(data, 0, data.Length);
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs(ex));
                IsConnected = false;
                OnConnectionStateChanged(new ConnectionStateChangedEventArgs(false));
                return false;
            }
        }

        /// <summary>
        /// 发送字符串数据
        /// </summary>
        /// <param name="data">要发送的字符串</param>
        /// <returns>是否发送成功</returns>
        public virtual bool Send(string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            return Send(bytes);
        }

        /// <summary>
        /// 开始接收数据循环
        /// </summary>
        protected virtual void StartReceiveLoop()
        {
            Task.Run(async () =>
            {
                try
                {
                    byte[] buffer = new byte[SocketParam.BufferSize];
                    while (IsConnected && _client.Connected)
                    {
                        int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            byte[] receivedData = new byte[bytesRead];
                            Array.Copy(buffer, 0, receivedData, 0, bytesRead);
                            OnDataReceived(new DataReceivedEventArgs(receivedData));
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnErrorOccurred(new ErrorEventArgs(ex));
                    IsConnected = false;
                    OnConnectionStateChanged(new ConnectionStateChangedEventArgs(false));
                    if (SocketParam.AutoReconnect)
                    {
                        Reconnect();
                    }
                }
            });
        }

        /// <summary>
        /// 重连
        /// </summary>
        protected virtual void Reconnect()
        {
            int reconnectCount = 0;
            while (reconnectCount < SocketParam.ReconnectCount && !IsConnected)
            {
                reconnectCount++;
                Task.Delay(SocketParam.ReconnectInterval).Wait();
                Connect();
            }
        }

        /// <summary>
        /// 触发连接状态变化事件
        /// </summary>
        /// <param name="e">事件参数</param>
        protected virtual void OnConnectionStateChanged(ConnectionStateChangedEventArgs e)
        {
            ConnectionStateChanged?.Invoke(this, e);
        }

        /// <summary>
        /// 触发数据接收事件
        /// </summary>
        /// <param name="e">事件参数</param>
        protected virtual void OnDataReceived(DataReceivedEventArgs e)
        {
            DataReceived?.Invoke(this, e);
        }

        /// <summary>
        /// 触发错误事件
        /// </summary>
        /// <param name="e">事件参数</param>
        protected virtual void OnErrorOccurred(ErrorEventArgs e)
        {
            ErrorOccurred?.Invoke(this, e);
        }
    }

    /// <summary>
    /// 连接状态变化事件参数
    /// </summary>
    public class ConnectionStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 是否连接
        /// </summary>
        public bool IsConnected { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="isConnected">是否连接</param>
        public ConnectionStateChangedEventArgs(bool isConnected)
        {
            IsConnected = isConnected;
        }
    }

    /// <summary>
    /// 数据接收事件参数
    /// </summary>
    public class DataReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// 接收到的数据
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="data">接收到的数据</param>
        public DataReceivedEventArgs(byte[] data)
        {
            Data = data;
        }
    }

    /// <summary>
    /// 错误事件参数
    /// </summary>
    public class ErrorEventArgs : EventArgs
    {
        /// <summary>
        /// 错误信息
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="exception">错误信息</param>
        public ErrorEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }
}