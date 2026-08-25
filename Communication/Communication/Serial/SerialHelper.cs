using Communication.Struct;
using Extension.Core.ConvertEx;
using Extension.Helper.Channel;
using Extension.Helper.TaskHelper;
using System;
using System.IO.Ports;
using System.Threading.Tasks;

namespace Communication.Serial
{
    public class SerialHelper : SerialBase
    {
        public override SerialParam SerialParam { get; set; }
        internal override ChannelHelper<byte[]> Datas { get; set; } = new ChannelHelper<byte[]>();
 
        public override bool IsConnect => serialPort.IsOpen;

        private ValueTaskSourceHelper<byte[]> valueTaskSource = new ValueTaskSourceHelper<byte[]>();
        /// <summary>
        /// 串口资源
        /// </summary>
        private SerialPort serialPort;
        /// <summary>
        /// lock锁
        /// </summary>
        private object _lock = new object();

        public override bool Connect()
        {
            if (serialPort != null && serialPort.IsOpen) return true;
            if (SerialParam == null) throw new Exception("Serial Param not set");

            serialPort = new SerialPort(SerialParam.PortName, SerialParam.BaudRate, SerialParam.Parity, SerialParam.DataBits, SerialParam.StopBits);
            serialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceived);
            if (SerialParam.TimeoutMs != null)
            {
                serialPort.ReadTimeout = (int)SerialParam.TimeoutMs;
                serialPort.WriteTimeout = (int)SerialParam.TimeoutMs;
            }
            if (SerialParam.BufferSize != null)
            {
                serialPort.ReadBufferSize = (int)SerialParam.BufferSize;
                serialPort.WriteBufferSize = (int)SerialParam.BufferSize;
            }
            else
            {
                serialPort.ReadBufferSize = 16 * 1024; ;
                serialPort.WriteBufferSize = 16 * 1024; ;
            }
 
            serialPort.Open();
            return true;
        }

        public override bool DisConnect()
        {
            serialPort.Close();
            return true;
        }

 
        public override void Write(string value)
        {
            Datas.Clear();
            serialPort.Write(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Datas.Clear();
            serialPort.Write(buffer, offset, count);
        }

        public override string WriteWithReply(string vlaue, int timeOut)
        {
            Datas.Clear();
            Write(vlaue);
            var data = Task.Run(async () =>
            {
                return await valueTaskSource.WaitSignal(timeOut).ConfigureAwait(true);

            }).GetAwaiter().GetResult();
            return data.ToASCII();
        }

        public override byte[] WriteWithReply(byte[] buffer, int offset, int count, int timeOut)
        {
            Datas.Clear();
            Write(buffer, offset, count);
            // 安全同步等待异步方法
            var data = Task.Run(async () =>
            {
                return await valueTaskSource.WaitSignal(timeOut).ConfigureAwait(false);
            }).GetAwaiter().GetResult();
            return data;
        }

        public override string Read()
        {
            Datas.TryRead(out var data);
            return data.ToASCII();
        }

        /// <summary>
        /// 串口接收数据事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            lock (_lock)
            {
                int readByteNum = 0;
                do
                {
                    readByteNum = serialPort.BytesToRead;
                    System.Threading.Thread.Sleep(10);
                } while (readByteNum < serialPort.BytesToRead && serialPort.BytesToRead < serialPort.ReadBufferSize);

                //创建接收字节数组
                Byte[] received = new Byte[serialPort.BytesToRead];
                //读取数据
                serialPort.Read(received, 0, received.Length);

                Datas.Write(received);
                //设置触发信号
                valueTaskSource.SetSignal(received);
                //触发回调函数
                _dataReceived?.Invoke(sender, received);
            }
        }
    }
}
