using System;
using System.Collections.Generic;
using Communication.ModbusRTU;
using Device.RFID.Definition;
using Extension.Core.ConvertEx;

namespace Device.RFID.Abstract
{
    internal abstract class RFIDBase_ModbusRTU : IRFID
    {
        public RFIDParam Param { get; set; }

        public ModbusRTUHelper ModbusRTUHelper { get; set; } = new ModbusRTUHelper();


        public bool IsConnected => ModbusRTUHelper.IsConnect;

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
            ModbusRTUHelper.ModbusRTUParam = Param;
            return ConnectRefactor();
        }

        protected abstract bool ConnectRefactor();
        
        public bool DisConnect()
        {
          return  DisConnectRefactor();
        }
        protected abstract bool DisConnectRefactor();

        public string Read(int page)
        {
            ushort length = 4;
            page = (ushort)(page - 1);
            var data = ModbusRTUHelper.ReadHoldingRegisters(Param.SlaveID, (ushort)(StartAddress + length * page), length);
            string result = data.ToASCII();
            return result;
        }

        public string Read(int startPage, int length)
        {
            ushort length1 = (ushort)(4 * length);
            startPage = (ushort)(startPage - 1);
            var data = ModbusRTUHelper.ReadHoldingRegisters(Param.SlaveID, (ushort)(StartAddress + length * startPage), length1);
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
            ModbusRTUHelper.WriteMultipleRegisters(Param.SlaveID, (ushort)(StartAddress + length * page), data1);
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
            ModbusRTUHelper.WriteMultipleRegisters(Param.SlaveID, StartAddress, ushorts.ToArray());
        }
    }
}
