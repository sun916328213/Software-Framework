using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communication.Socket
{
    /// <summary>
    /// Socket 通讯助手类
    /// </summary>
    public class SocketHelper : SocketBase
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="socketParam">Socket 参数</param>
        public SocketHelper(SocketParam socketParam) : base(socketParam)
        {
        }

        /// <summary>
        /// 发送数据并等待响应
        /// </summary>
        /// <param name="data">要发送的数据</param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <returns>响应数据</returns>
        public async Task<byte[]> SendAndReceive(byte[] data, int timeout = 5000)
        {
            if (!IsConnected)
            {
                return null;
            }

            // 创建任务CompletionSource
            var tcs = new TaskCompletionSource<byte[]>();
            byte[] responseData = null;

            // 临时事件处理程序
            EventHandler<DataReceivedEventArgs> dataReceivedHandler = (sender, e) =>
            {
                responseData = e.Data;
                tcs.TrySetResult(responseData);
            };

            try
            {
                // 注册事件处理程序
                DataReceived += dataReceivedHandler;

                // 发送数据
                if (!Send(data))
                {
                    return null;
                }

                // 等待响应或超时
                var timeoutTask = Task.Delay(timeout);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // 超时
                    return null;
                }

                return await tcs.Task;
            }
            finally
            {
                // 移除事件处理程序
                DataReceived -= dataReceivedHandler;
            }
        }

        /// <summary>
        /// 发送字符串数据并等待响应
        /// </summary>
        /// <param name="data">要发送的字符串</param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <returns>响应字符串</returns>
        public async Task<string> SendAndReceive(string data, int timeout = 5000)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            byte[] responseBytes = await SendAndReceive(bytes, timeout);
            if (responseBytes == null)
            {
                return null;
            }
            return Encoding.UTF8.GetString(responseBytes);
        }

        /// <summary>
        /// 发送数据并等待响应（同步）
        /// </summary>
        /// <param name="data">要发送的数据</param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <returns>响应数据</returns>
        public byte[] SendAndReceiveSync(byte[] data, int timeout = 5000)
        {
            return SendAndReceive(data, timeout).Result;
        }

        /// <summary>
        /// 发送字符串数据并等待响应（同步）
        /// </summary>
        /// <param name="data">要发送的字符串</param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <returns>响应字符串</returns>
        public string SendAndReceiveSync(string data, int timeout = 5000)
        {
            return SendAndReceive(data, timeout).Result;
        }

        /// <summary>
        /// 批量发送数据
        /// </summary>
        /// <param name="dataList">要发送的数据列表</param>
        /// <returns>是否全部发送成功</returns>
        public bool SendBatch(List<byte[]> dataList)
        {
            if (!IsConnected)
            {
                return false;
            }

            foreach (var data in dataList)
            {
                if (!Send(data))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 批量发送字符串数据
        /// </summary>
        /// <param name="dataList">要发送的字符串列表</param>
        /// <returns>是否全部发送成功</returns>
        public bool SendBatch(List<string> dataList)
        {
            List<byte[]> byteList = dataList.Select(s => Encoding.UTF8.GetBytes(s)).ToList();
            return SendBatch(byteList);
        }
    }
}