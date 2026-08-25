using Communication.Core.Definition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communication.Buffers
{
    /// <summary>
    /// 通讯数据存储缓冲 用于触发
    /// </summary>
   public interface ICommunicationBuffer : IDisposable
    {
        bool IsChannelRunning { get; }
        void Enqueue(ValueChangedDelegate cb, DeviceValue value, E_CallBackWeight weight);
        Task StopAsync();
    }
}
