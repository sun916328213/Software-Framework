using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunicationNew.Protocols.Modbus;
using CommunicationNew.Protocols.Modbus.ModbusRtu;
using Device.RFID.Definition;
using Extension.Core.Conversion;

namespace Device.RFID.Abstract
{
    internal abstract class RFIDBase_ModbusRTU : IRFID
    {
        public RFIDParam Param { get; set; }

        /// <summary>
        /// 底层 Modbus RTU 客户端。CommunicationNew 的客户端在构造时绑定参数，
        /// 因此这里在 <see cref="Connect"/> 时按 Param 创建，而不是字段初始化。
        /// </summary>
        private ModbusRtuClientDevice _modbusClient;

        /// <summary>底层 Modbus RTU 客户端；未连接时访问会抛出异常。</summary>
        protected ModbusRtuClientDevice ModbusClient => _modbusClient
            ?? throw new InvalidOperationException("Modbus RTU 尚未连接。");

        public bool IsConnected => _modbusClient?.IsConnected ?? false;

        public string Name
        {
            get => Param.Name;
            set => Param.Name = value;
        }
 
        /// <summary>
        /// RFID寄存器开始地址
        /// </summary>
        protected abstract ushort StartAddress { get; set; }

        public bool Connect()
        {
            if (Param == null) throw new Exception("ModbusRTU Param not set");
            _modbusClient?.Dispose();
            _modbusClient = new ModbusRtuClientDevice(Param);
            return ConnectRefactor();
        }

        protected abstract bool ConnectRefactor();
        
        public bool DisConnect()
        {
          if (_modbusClient == null) return true;
          return  DisConnectRefactor();
        }
        protected abstract bool DisConnectRefactor();

        public string Read(int page)
        {
            ushort length = 4;
            page = (ushort)(page - 1);
            var data = ReadHoldingRegisters((ushort)(StartAddress + length * page), length);
            string result = data.ToASCII();
            return result;
        }

        public string Read(int startPage, int length)
        {
            ushort length1 = (ushort)(4 * length);
            startPage = (ushort)(startPage - 1);
            var data = ReadHoldingRegisters((ushort)(StartAddress + length * startPage), length1);
            string result = data.ToASCII();
            return result;
        }

        public void Write(int page, string data, int makeuplength = 0)
        {
            if (data.Length > 8) throw new Exception("The data length exceeds the limit");
            if (data.Length < makeuplength) data = data.PadRight(makeuplength, '\0');

            page = (ushort)(page - 1);
            ushort[] data1 = data.ToUshorts();
            int length = 4;
            WriteMultipleRegisters((ushort)(StartAddress + length * page), data1);
        }

        public void Write(int startPage, List<string> data, int makeuplength = 0)
        {
            List<ushort> ushorts = new List<ushort>();
            foreach (string item in data)
            {
                if (item.Length < makeuplength)
                {
                    string data1 = item.PadRight(makeuplength, '\0');
                }
                ushort[] itemUshorts = item.ToUshorts();
                foreach (ushort u in itemUshorts)
                {
                    ushorts.Add(u);
                }
            }
            WriteMultipleRegisters(StartAddress, ushorts.ToArray());
        }

        /// <summary>
        /// 保持寄存器读取。旧实现直接调 FluentModbus 的 ReadHoldingRegisters；
        /// 这里改用 CommunicationNew 的地址字符串 + 异步客户端。
        /// </summary>
        private byte[] ReadHoldingRegisters(ushort startAddress, ushort length)
        {
            var address = new ModbusAddressValue(ModbusRegion.HoldingRegister, startAddress).ToString();
            return Task.Run(() => ModbusClient.ReadAsync(address, length)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 保持寄存器批量写入。寄存器按大端拆字节，与 FluentModbus 的线序一致。
        /// </summary>
        private void WriteMultipleRegisters(ushort startAddress, ushort[] values)
        {
            var address = new ModbusAddressValue(ModbusRegion.HoldingRegister, startAddress).ToString();
            var payload = new byte[values.Length * 2];
            for (int i = 0; i < values.Length; i++)
            {
                payload[i * 2] = (byte)(values[i] >> 8);
                payload[i * 2 + 1] = (byte)(values[i] & 0xFF);
            }
            Task.Run(() => ModbusClient.WriteAsync(address, payload)).GetAwaiter().GetResult();
        }
    }
}
