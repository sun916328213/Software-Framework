
using Communication.Protocols.Modbus.RTU.Definition;
using Extension.Core.Concurrency;
using FluentModbus;
using System;
using System.IO.Ports;

namespace Communication.Protocols.Modbus.RTU
{
    public class ModbusRTUHelper : ModbusRTUBase
    {
        public override ModbusRTUParam ModbusRTUParam { get  ; set  ; } 
        internal override ChannelHelper<byte[]> Datas { get  ; set ; }=new ChannelHelper<byte[]>();
 
        public override bool IsConnect => ModbusClient.IsConnected;


      private  ModbusRtuClient ModbusClient =new ModbusRtuClient();
        
        public override bool Connect()
        {
           
            if (ModbusRTUParam == null) throw new Exception("Serial Param not set");

            ModbusClient.BaudRate = ModbusRTUParam.BaudRate;
            ModbusClient.Parity = ModbusRTUParam.Parity;
            ModbusClient.StopBits = ModbusRTUParam.StopBits;
            ModbusClient.Connect(ModbusRTUParam.PortName);
             
            return true;
        }

        public override bool DisConnect()
        {
            ModbusClient.Close();
            return true;
        }

        public override void WriteSingleRegister(int slaveID, ushort startAddress, ushort value)
        {
            ModbusClient.WriteSingleRegister((byte)slaveID, startAddress, value);
        }

        public override void WriteMultipleRegisters(int slaveID, ushort startAddress, ushort[] value)
        {
            ModbusClient.WriteMultipleRegisters((byte)slaveID, startAddress, value);
        }

        public override byte[] ReadHoldingRegisters(int slaveID, ushort startAddress, ushort length)
        {
            var value = ModbusClient.ReadHoldingRegisters((byte)slaveID, startAddress, length);
            return value.ToArray();
        }
    }
}
