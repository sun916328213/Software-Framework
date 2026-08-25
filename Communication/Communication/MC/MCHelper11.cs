using ActProgType64Lib;
using ActUtlType64Lib;
using Communication;
using Extension.Core.CompareEx;
using Extension.Core.ConvertEx;
using Extension.Core.DataEx;
using Extension.Core.EnumEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Communication
{
    enum ActType
    {
        ActUtlType,
        ActProgType
    }
 
    public class MCHelper11 
    {
        private ActUtlType64Class CM_Utl;
        private ActProgType64Class CM_Prog;
        private ActType ActType;
        private CancellationTokenSource Token=new CancellationTokenSource();
        private List<Action> Actions=new List<Action>();

        private object obj_write = new object();
        private object obj_read = new object();
 
        /// <summary>
        /// 寄存器信息集合
        /// </summary>
        private List<CRegisterManager> RegisterCollection { get; set; } = new List<CRegisterManager>();

        /// <summary>
        /// 是否打开
        /// </summary>
        public bool IsOpen { get; set; } = false;

        /// <summary>
        /// 配置寄存器信息
        /// </summary>
        /// <param name="pRegisterInfos"></param>
        /// <returns></returns>
        public void LoadConfig(List<CRegisterInfo> pRegisterInfos)
        {
            RegisterCollection.Clear();
            pRegisterInfos.ForEach(s =>
            {
                RegisterCollection.Add(new CRegisterManager(s));
            });
        }

        /// <summary>
        /// 获取寄存器信息
        /// </summary>
        /// <param name="pName"></param>
        /// <returns></returns>
        public CRegisterManager GetRegister(string pName)
        {
            return RegisterCollection.FirstOrDefault(s => s.RegisterInfo.Name == pName);
        }

        /// <summary>
        /// 根据寄存器名称获取寄存器索引
        /// </summary>
        /// <param name="pName">寄存器名称</param>
        /// <param name="offestAddress">寄存器偏移地址</param>
        /// <returns></returns>
        public CRegisterManager GetRegister (string pName,int offestAddress)
        {
            var data = GetRegister(pName);
            int address = data.Address + offestAddress;
            return RegisterCollection.FirstOrDefault(s => s.Address == address);
        }


        /// <summary>
        /// 连接三菱PLC,ActUtlType
        /// </summary>
        /// <param name="stationnumber">站号</param>
        /// <param name="password">密码</param>
        /// <returns></returns>
        public bool Connect(int stationnumber, string password = "")
        {
            ActType = ActType.ActUtlType;
            if (CM_Utl == null) CM_Utl = new ActUtlType64Class();
            CM_Utl.ActLogicalStationNumber = stationnumber;
            CM_Utl.ActPassword = password;
            if (IsOpen == true) CM_Utl.Close();
            int rect = CM_Utl.Open();
            if (rect == 0)
            {
                IsOpen = true;
            }
            else
            {
                IsOpen = false;
            }
            return IsOpen;
        }

        /// <summary>
        /// 连接三菱PLC,ActProgType
        /// </summary>
        /// <param name="ActUnitType"></param>
        /// <param name="ActProtocolType"></param>
        /// <param name="ActNetworkNumber"></param>
        /// <param name="ActStationNumber"></param>
        /// <param name="ActIONumber"></param>
        /// <param name="ActCpuType"></param>
        /// <param name="ActPortNumber"></param>
        /// <param name="ActHostAddress"></param>
        /// <param name="ActSourceNetworkNumber"></param>
        /// <param name="ActSourceStationNumber"></param>
        /// <param name="ActDestinationPortNumber"></param>
        /// <param name="ActTargetSimulator"></param>
        /// <returns></returns>
        public bool Connect(int ActUnitType, int ActProtocolType, int ActNetworkNumber, int ActStationNumber, int ActIONumber, int ActCpuType, int ActPortNumber, string ActHostAddress, int ActSourceNetworkNumber, int ActSourceStationNumber, int ActDestinationPortNumber, int ActTargetSimulator)
        {
            ActType = ActType.ActProgType;
            CM_Prog.ActUnitType = ActUnitType;       // UNIT_RUSB
            CM_Prog.ActProtocolType = ActProtocolType;     // PROTOCOL_USB
            CM_Prog.ActNetworkNumber = ActNetworkNumber;
            CM_Prog.ActStationNumber = ActStationNumber;
            CM_Prog.ActUnitNumber = 0;
            CM_Prog.ActConnectUnitNumber = 0;
            CM_Prog.ActIONumber = ActIONumber;
            CM_Prog.ActCpuType = ActCpuType;
            CM_Prog.ActPortNumber = ActPortNumber;
            CM_Prog.ActBaudRate = 0;
            CM_Prog.ActDataBits = 0;
            CM_Prog.ActParity = 0;
            CM_Prog.ActStopBits = 0;
            CM_Prog.ActControl = 0;
            CM_Prog.ActHostAddress = ActHostAddress;
            CM_Prog.ActCpuTimeOut = 0;
            CM_Prog.ActTimeOut = 10000;
            CM_Prog.ActSumCheck = 0;
            CM_Prog.ActSourceNetworkNumber = ActSourceNetworkNumber;
            CM_Prog.ActSourceStationNumber = ActSourceStationNumber;
            CM_Prog.ActDestinationPortNumber = ActDestinationPortNumber;
            CM_Prog.ActDestinationIONumber = 0;
            CM_Prog.ActMultiDropChannelNumber = 0;
            CM_Prog.ActThroughNetworkType = 0;
            CM_Prog.ActIntelligentPreferenceBit = 0;
            CM_Prog.ActDidPropertyBit = 1;
            CM_Prog.ActDsidPropertyBit = 1;
            CM_Prog.ActPacketType = 1;
            CM_Prog.ActTargetSimulator = ActTargetSimulator;
            int rect = CM_Prog.Open();
            if (rect == 0)
            {
                IsOpen = true;
            }
            else
            {
                IsOpen = false;
            }
            return IsOpen;
        }

        /// <summary>
        /// 连接三菱PLC,ActProgType
        /// </summary>
        /// <param name="Params">参数</param>
        /// <returns></returns>
        public bool Connect(ActProgType64Class Params)
        {
            ActType = ActType.ActProgType;
            CM_Prog = Params;
            int rect = CM_Prog.Open();
            if (rect == 0)
            {
                IsOpen = true;
            }
            else
            {
                IsOpen = true;
            }
            return IsOpen;
        }

        /// <summary>
        /// 关闭通讯
        /// </summary>
        /// <returns></returns>
        public bool Close()
        {
            int rect = -1;
            if (ActType == ActType.ActUtlType)
                rect = CM_Utl.Close();

            else
                rect = CM_Prog.Close();

            IsOpen = false;
            if (rect == 0) return true;
            else return false;
        }

        /// <summary>
        /// 读取单个寄存器
        /// </summary>
        /// <param name="Data">输出数据</param>
        /// <param name="registeraddress">寄存器地址</param>
        /// <returns></returns>
        public short ReadRegister_Single(string registeraddress)
        {
            if (JudgeIsConnect() == false) throw new Exception("Unconnected!!");
            int rect;
            short DeviceValue;
            if (ActType == ActType.ActUtlType)
                rect = CM_Utl.GetDevice2(registeraddress, out DeviceValue);
            else
                rect = CM_Prog.GetDevice2(registeraddress, out DeviceValue);

            if (rect == 0) return DeviceValue;
            throw new Exception("Read Faill!!");
        }

        /// <summary>
        /// 读取寄存器,格式为bit
        /// </summary>
        /// <param name="Data">输出数据</param>
        /// <param name="registeraddress">寄存器地址</param>
        /// <param name="length">寄存器长度,16bit=1</param>
        /// <returns></returns>
        public bool ReadRegister_ushort(out List<ushort[]> Data, string registeraddress, int length = 1)
        {
            lock (obj_read)
            {
                Data = null;
                if (JudgeIsConnect() == false) return false;
                int rect;
                short[] DeviceValue = new short[length];
                if (ActType == ActType.ActUtlType)
                    rect = CM_Utl.ReadDeviceBlock2(registeraddress, length, out DeviceValue[0]);
                else
                    rect = CM_Prog.ReadDeviceBlock2(registeraddress, length, out DeviceValue[0]);

                if (rect == 0)
                {
                    Data = new List<ushort[]>();
                    for (int index = 0; index < length; index++)
                    {
                        string val =  DecimalEx.ToBinary(DeviceValue[index], 16);
                        ushort[] iData = new ushort[16];
                        for (int index2 = 0; index2 < val.Length; index2++)
                        {
                            string bytes = val[val.Length - 1 - index2].ToString();
                            iData[index2] = Convert.ToUInt16(bytes);
                        }
                        Data.Add(iData);
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 读取寄存器,格式为二进制
        /// </summary>
        /// <param name="Data">输出数据</param>
        /// <param name="registeraddress">寄存器地址</param>
        /// <param name="length">寄存器长度,16bit=1</param>
        /// <returns></returns>
        public bool ReadRegister(out List<string> Data, string registeraddress, int length = 1)
        {
            lock (obj_read)
            {
                Data = null;
                if (JudgeIsConnect() == false) return false;
                int rect;
                short[] DeviceValue = new short[length];
                if (ActType == ActType.ActUtlType)
                    rect = CM_Utl.ReadDeviceBlock2(registeraddress, length, out DeviceValue[0]);
                else
                    rect = CM_Prog.ReadDeviceBlock2(registeraddress, length, out DeviceValue[0]);

                if (rect == 0)
                {
                    Data = new List<string>();
                    for (int index = 0; index < length; index++)
                    {
                        string val = DecimalEx.ToBinary(DeviceValue[index], 16);
                        Data.Add(val);
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 读取寄存器,格式为short,占16个bit  基础读取方式
        /// </summary>
        /// <param name="Data">输出数据</param>
        /// <param name="registeraddress">寄存器地址</param>
        /// <param name="length">寄存器长度,16bit=1</param>
        /// <returns></returns>
        public bool ReadRegister(out short[] Data, string registeraddress, int length = 1)
        {
            lock (obj_read)
            {
                Data = null;
                if (JudgeIsConnect() == false) return false;
                int rect;
                short[] DeviceValue = new short[length];
                if (ActType == ActType.ActUtlType)
                    rect = CM_Utl.ReadDeviceBlock2(registeraddress, length, out DeviceValue[0]);
                else
                    rect = CM_Prog.ReadDeviceBlock2(registeraddress, length, out DeviceValue[0]);
                Data = DeviceValue;
                if (rect == 0) return true;
                else return false;
            }
        }

        /// <summary>
        /// 读取寄存器,格式为int,占32个bit
        /// </summary>
        /// <param name="Data">输出数据</param>
        /// <param name="registeraddress">寄存器地址</param>
        /// <param name="length">寄存器长度,偶数,16bit=1</param>
        /// <returns></returns>
        public bool ReadRegister(out int[] Data, string registeraddress, int length = 2)
        {
            lock (obj_read)
            {
                Data = null;
                if (JudgeIsConnect() == false) return false;
                if (length % 2 == 0)
                {
                    int rect;
                    short[] DeviceValue = new short[length];
                    if (ActType == ActType.ActUtlType)
                        rect = CM_Utl.ReadDeviceBlock2(registeraddress, length, out DeviceValue[0]);
                    else
                        rect = CM_Prog.ReadDeviceBlock2(registeraddress, length, out DeviceValue[0]);

                    if (rect == 0)
                    {
                        length = length / 2;
                        Data = new int[length];
                        for (int index = 0; index < length; index++)
                        {
                            byte[] bytes1 = BitConverter.GetBytes(DeviceValue[2 * index]);
                            byte[] bytes2 = BitConverter.GetBytes(DeviceValue[2 * index + 1]);
                            byte[] bytes = new byte[] { bytes1[0], bytes1[1], bytes2[0], bytes2[1] };
                            Data[index] = BitConverter.ToInt32(bytes, 0);
                        }
                        return true;
                    }
                    else return false;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 读取寄存器,格式为string,占16个bit
        /// </summary>
        /// <param name="Data">输出数据</param>
        /// <param name="registeraddress">寄存器地址</param>
        /// <param name="length">寄存器长度,16bit=1</param>
        /// <returns></returns>
        public bool ReadRegister(out string Data, string registeraddress, int length = 1)
        {
            lock (obj_read)
            {
                Data = null;
                if (JudgeIsConnect() == false) return false;
                int rect;
                short[] DeviceValue = new short[length];
                if (ActType == ActType.ActUtlType)
                    rect = CM_Utl.ReadDeviceBlock2(registeraddress, length, out DeviceValue[0]);
                else
                    rect = CM_Prog.ReadDeviceBlock2(registeraddress, length, out DeviceValue[0]);

                if (rect == 0)
                {
                    byte[] bytes = new byte[2 * length];
                    for (int index = 0; index < length; index++)
                    {

                        byte[] bytes1 = BitConverter.GetBytes(DeviceValue[index]);
                        bytes[2 * index] = bytes1[0];
                        bytes[2 * index + 1] = bytes1[1];
                    }
                    Data = System.Text.Encoding.UTF8.GetString(bytes).ToString();
                    return true;
                }
                else return false;
            }
        }

        /// <summary>
        /// 读取寄存器,格式为strin[],占16个bit
        /// </summary>
        /// <param name="Data">输出数据</param>
        /// <param name="registeraddress">寄存器地址</param>
        /// <param name="length">寄存器长度,16bit=1</param>
        /// <returns></returns>
        public bool ReadRegister(out string[] Data, string registeraddress, int length = 1)
        {
            lock (obj_read)
            {
                Data = null;
                if (JudgeIsConnect() == false) return false;
                int rect;
                short[] DeviceValue = new short[length];
                if (ActType == ActType.ActUtlType)
                    rect = CM_Utl.ReadDeviceBlock2(registeraddress, length, out DeviceValue[0]);
                else
                    rect = CM_Prog.ReadDeviceBlock2(registeraddress, length, out DeviceValue[0]);

                if (rect == 0)
                {
                    Data = new string[length];
                    for (int index = 0; index < length; index++)
                    {
                        byte[] bytes = BitConverter.GetBytes(DeviceValue[index]);
                        Data[index] = System.Text.Encoding.UTF8.GetString(bytes).ToString();
                    }
                    return true;
                }
                else return false;
            }
        }

        /// <summary>
        /// 读取寄存器,格式为float,占32个bit
        /// </summary>
        /// <param name="Data">输出数据</param>
        /// <param name="registeraddress">寄存器地址</param>
        /// <param name="length">寄存器长度,偶数</param>
        /// <returns></returns>
        public bool ReadRegister(out float[] Data, string registeraddress, int length = 2)
        {
            Data = null;
            if (JudgeIsConnect() == false) return false;
            if (length % 2 == 0)
            {
                int rect;
                short[] DeviceValue = new short[length];

                if (ActType == ActType.ActUtlType)
                    rect = CM_Utl.ReadDeviceBlock2(registeraddress, length, out DeviceValue[0]);
                else
                    rect = CM_Prog.ReadDeviceBlock2(registeraddress, length, out DeviceValue[0]);

                if (rect == 0)
                {
                    length = length / 2;
                    Data = new float[length];
                    for (int index = 0; index < length; index++)
                    {
                        byte[] bytes1 = BitConverter.GetBytes(DeviceValue[2 * index]);
                        byte[] bytes2 = BitConverter.GetBytes(DeviceValue[2 * index + 1]);
                        byte[] bytes = new byte[] { bytes1[0], bytes1[1], bytes2[0], bytes2[1] };
                        Data[index] = BitConverter.ToSingle(bytes, 0);
                    }
                    return true;
                }
                else return false;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 读取寄存器,格式为double,占64个bit
        /// </summary>
        /// <param name="Data">输出数据</param>
        /// <param name="registeraddress">寄存器地址</param>
        /// <param name="length">寄存器长度,4倍数</param>
        /// <returns></returns>
        public bool ReadRegister(out double[] Data, string registeraddress, int length = 4)
        {
            Data = null;
            if (JudgeIsConnect() == false) return false;
            if (length % 4 == 0)
            {
                int rect;
                short[] DeviceValue = new short[length];
                if (ActType == ActType.ActUtlType)
                    rect = CM_Utl.ReadDeviceBlock2(registeraddress, length, out DeviceValue[0]);
                else
                    rect = CM_Prog.ReadDeviceBlock2(registeraddress, length, out DeviceValue[0]);

                if (rect == 0)
                {
                    length = length / 4;
                    Data = new double[length];
                    for (int index = 0; index < length; index++)
                    {
                        byte[] bytes1 = BitConverter.GetBytes(DeviceValue[4 * index]);
                        byte[] bytes2 = BitConverter.GetBytes(DeviceValue[4 * index + 1]);
                        byte[] bytes3 = BitConverter.GetBytes(DeviceValue[4 * index + 2]);
                        byte[] bytes4 = BitConverter.GetBytes(DeviceValue[4 * index + 3]);
                        byte[] bytes = new byte[] { bytes1[0], bytes1[1], bytes2[0], bytes2[1], bytes3[0], bytes3[1], bytes4[0], bytes4[1] };
                        Data[index] = BitConverter.ToDouble(bytes, 0);
                    }
                    return true;
                }
                else return false;
            }
            else
            {
                return false;
            }
        }



        /// <summary>
        /// 写入单个寄存器 
        /// </summary>
        /// <param name="registeraddress">寄存器地址</param>
        /// <param name="Data">数据,bit</param>
        /// <returns></returns>
        public bool WriteRegister(string registeraddress, short Data)
        {
            lock (obj_write)
            {
                if (JudgeIsConnect() == false) return false;
                int rect;
                if (ActType == ActType.ActUtlType)
                    rect = CM_Utl.SetDevice2(registeraddress, Data);
                else
                    rect = CM_Prog.SetDevice2(registeraddress, Data);
                if (rect == 0) return true;
                else return false;
            }
        }

        /// <summary>
        /// 写入寄存器,格式为bit,占1个bit
        /// </summary>
        /// <param name="registeraddress">寄存器地址</param>
        /// <param name="Data">数据,ushort[]=16bit</param>
        /// <returns></returns>
        public bool WriteRegister(string registeraddress, List<ushort[]> Data)
        {
            if (JudgeIsConnect() == false) return false;
            short[] shorts = new short[Data.Count];
            for (int index = 0; index < Data.Count; index++)
            {
                ushort[] Data2 = Data[index];
                string str = "";
                for (int index2 = 0; index2 < Data2.Length; index2++)
                {
                    str = Data2[index2].ToString() + str;
                }
                str = str.PadLeft(16, '0');

                shorts[index] = Convert.ToInt16(str, 2);
            }
            bool rect = WriteRegister(registeraddress, shorts);
            return rect;
        }

        /// <summary>
        /// 写入寄存器,格式short,占16个bit  最基础函数
        /// </summary>
        /// <param name="registeraddress">寄存器地址</param>
        /// <param name="Data">数据,short=16bit</param>
        /// <returns></returns>
        public bool WriteRegister(string registeraddress, short[] Data)
        {
            lock (obj_write)
            {
                if (JudgeIsConnect() == false) return false;
                int rect;
                int length = Data.Length;
                if (ActType == ActType.ActUtlType)
                    rect = CM_Utl.WriteDeviceBlock2(registeraddress, length, ref Data[0]);
                else
                    rect = CM_Prog.WriteDeviceBlock2(registeraddress, length, ref Data[0]);
                if (rect == 0) return true;
                else return false;
            }
        }

        /// <summary>
        /// 写入寄存器,格式int,占32个bit
        /// </summary>
        /// <param name="registeraddress">寄存器地址</param>
        /// <param name="Data">数据,int=32bit</param>
        /// <returns></returns>
        public bool WriteRegister(string registeraddress,params int[]  Data)
        {
            lock (obj_write)
            {
                if (JudgeIsConnect() == false) return false;
                short[] shorts = new short[Data.Length * 2];
                for (int index = 0; index < Data.Length; index++)
                {
                    byte[] bytes = BitConverter.GetBytes(Data[index]);
                    byte[] bytes1 = new byte[] { bytes[0], bytes[1] };
                    byte[] bytes2 = new byte[] { bytes[2], bytes[3] };
                    shorts[2 * index] = BitConverter.ToInt16(bytes1, 0);
                    shorts[2 * index + 1] = BitConverter.ToInt16(bytes2, 0);
                }
                bool rect = WriteRegister(registeraddress, shorts);
                return rect;
            }
        }

        /// <summary>
        /// 写入寄存器,格式string,占16个bit
        /// </summary>
        /// <param name="registeraddress">寄存器地址</param>
        /// <param name="Data">数据,string=16bit</param>
        /// <returns></returns>
        public bool WriteRegister(string registeraddress, string Data)
        {
            lock (obj_write)
            {
                if (JudgeIsConnect() == false) return false;
                int length = Data.Length / 2 + Data.Length % 2;
                short[] shorts = new short[length];
                for (int index = 0; index < length; index++)
                {
                    int ind = 2 * index;
                    byte byte1 = Convert.ToByte(Data[ind]);
                    byte byte2 = new byte();
                    if (ind + 1 < Data.Length)
                        byte2 = Convert.ToByte(Data[ind + 1]);
                    byte[] bytes = new byte[] { byte1, byte2 };
                    shorts[index] = BitConverter.ToInt16(bytes, 0);
                }
                bool rect = WriteRegister(registeraddress, shorts);
                return rect;
            }
        }

        /// <summary>
        ///  写入寄存器,格式string[],[]=2个char,占16个bit
        /// </summary>
        /// <param name="registeraddress">寄存器地址</param>
        /// <param name="Data">数据,string=16bit</param>
        /// <returns></returns>
        public bool WriteRegister(string registeraddress, params string[] Data)
        {
            lock (obj_write)
            {
                if (JudgeIsConnect() == false) return false;
                short[] shorts = new short[Data.Length];
                for (int index = 0; index < Data.Length; index++)
                {
                    string idata = Data[index];
                    byte byte1 = Convert.ToByte(idata[0]);
                    byte byte2 = Convert.ToByte(idata[1]);
                    byte[] bytes = new byte[] { byte1, byte2 };
                    shorts[index] = BitConverter.ToInt16(bytes, 0);
                }
                bool rect = WriteRegister(registeraddress, shorts);
                return rect;
            }
        }

        /// <summary>
        /// 写入寄存器,格式float,占32个bit
        /// </summary>
        /// <param name="registeraddress">寄存器地址</param>
        /// <param name="Data">数据,float=32bit</param>
        /// <returns></returns>
        public bool WriteRegister(string registeraddress, params float[] Data)
        {
            lock (obj_write)
            {
                if (JudgeIsConnect() == false) return false;
                short[] shorts = new short[Data.Length * 2];
                for (int index = 0; index < Data.Length; index++)
                {
                    float idata = Data[index];
                    byte[] bytes = BitConverter.GetBytes(idata);
                    byte[] bytes1 = new byte[] { bytes[0], bytes[1] };
                    byte[] bytes2 = new byte[] { bytes[2], bytes[3] };
                    shorts[2 * index] = BitConverter.ToInt16(bytes1, 0);
                    shorts[2 * index + 1] = BitConverter.ToInt16(bytes2, 0);
                }
                bool rect = WriteRegister(registeraddress, shorts);
                return rect;
            }
        }

        /// <summary>
        /// 写入寄存器,格式double,占64个bit
        /// </summary>
        /// <param name="registeraddress">寄存器地址</param>
        /// <param name="Data">数据,double=64bit</param>
        /// <returns></returns>
        public bool WriteRegister(string registeraddress, params double[] Data)
        {
            lock (obj_write)
            {
                if (JudgeIsConnect() == false) return false;
                short[] shorts = new short[Data.Length * 4];
                for (int index = 0; index < Data.Length; index++)
                {
                    double idata = Data[index];
                    byte[] bytes = BitConverter.GetBytes(idata);
                    byte[] bytes1 = new byte[] { bytes[0], bytes[1] };
                    byte[] bytes2 = new byte[] { bytes[2], bytes[3] };
                    byte[] bytes3 = new byte[] { bytes[4], bytes[5] };
                    byte[] bytes4 = new byte[] { bytes[6], bytes[7] };
                    shorts[4 * index] = BitConverter.ToInt16(bytes1, 0);
                    shorts[4 * index + 1] = BitConverter.ToInt16(bytes2, 0);
                    shorts[4 * index + 2] = BitConverter.ToInt16(bytes3, 0);
                    shorts[4 * index + 3] = BitConverter.ToInt16(bytes4, 0);
                }
                bool rect = WriteRegister(registeraddress, shorts);
                return rect;
            }
        }

        /// <summary>
        /// 判断是否连接
        /// </summary>
        /// <returns></returns>
        private bool JudgeIsConnect()
        {
            if (ActType == ActType.ActUtlType)
            {
                if (CM_Utl != null)
                {
                    if (IsOpen)
                        return true;
                    else
                        return false;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                if (CM_Prog != null)
                {
                    if (IsOpen)
                        return true;
                    else
                        return false;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 读取数据并按照指定格式进行解析
        /// </summary>
        /// <param name="registerType">寄存器类型</param>
        /// <param name="data">通讯寄存器信息</param>
        /// <param name="startadd">通讯寄存器开始地址</param>
        /// <param name="reglength">通讯寄存器长度</param>
        /// <param name="OutputData">输出读取数据信息</param>
        /// <param name="OutputAlarm">输出读取报警信息</param>
        public bool AnalyData(ERegisterType registerType, Dictionary<string, CRegisterManager> data, string startadd, int reglength, out Dictionary<string, string> OutputData)
        {
            lock (obj_read)
            {
                Dictionary<string, string> iOutputData = new Dictionary<string, string>();
                Dictionary<string, string> iOutputAlarm = new Dictionary<string, string>();

                List<string> InputData = new List<string>();
                Dictionary<string, CRegisterManager> fieldInfos = null;
                if (!JudgeIsConnect()) { OutputData = null; return false; }
                ///读取二进制格式
                bool ret = ReadRegister(out InputData, registerType.ToString() + startadd, reglength);
                fieldInfos = data;
                if (fieldInfos != null && InputData != null && ret == true)
                {
                    foreach (KeyValuePair<string, CRegisterManager> item in fieldInfos)
                    {
                        string Value = "";

                        CRegisterManager info = item.Value;

                        int Index = 0, StartAdd = 0;
                        if (registerType == ERegisterType.W) { Index = Convert.ToInt32(info.RegisterInfo.Address, 16); StartAdd = Convert.ToInt32(startadd, 16); }
                        else if (registerType == ERegisterType.D) { Index = Convert.ToInt32(info.RegisterInfo.Address); StartAdd = Convert.ToInt32(startadd); }
                        else if (registerType == ERegisterType.B) { Index = Convert.ToInt32(info.RegisterInfo.Address, 16) / 16; StartAdd = Convert.ToInt32(startadd, 16) / 16; }
                        else if (registerType == ERegisterType.M) { Index = Convert.ToInt32(info.RegisterInfo.Address) / 16; StartAdd = Convert.ToInt32(startadd) / 16; }
                        Index -= StartAdd;
                        string type = info.RegisterInfo.Type.ToString();
                        if (type.Contains(EDataType.String.ToString()))
                        {
                            int length = Convert.ToInt32(info.RegisterInfo.Length);
                            Value = "";
                            for (int i = 0; i < length; i++)
                            {
                                string val =  BinaryEx.ToASC(InputData[Index + i]).Replace("\0", string.Empty);
                                Value += val;
                            }
                        }
                        else if (type.Contains(EDataType.Bit.ToString()))
                        {
                            int length = Convert.ToInt32(info.RegisterInfo.Length.ToString().Split('.')[1]);
                            Value = InputData[Index].Substring(16 - length, 1);

                        }
                        else if (type.Contains(EDataType.Short.ToString()))
                        {
                            int length = Convert.ToInt32(info.RegisterInfo.Length);
                            Value = "";
                            for (int i = 0; i < length; i++)
                            {
                                short val = Convert.ToInt16(InputData[Index], 2);

                                Value += val.ToString();
                            }
                        }
                        else if (type.Contains(EDataType.Int.ToString()))
                        {
                            int length = Convert.ToInt32(info.RegisterInfo.Length);
                            Value = "";
                            for (int i = 0; i < length; i = i + 2)
                            {
                                short val1 = Convert.ToInt16(InputData[Index + i], 2);
                                short val2 = Convert.ToInt16(InputData[Index + i + 1], 2);
                                byte[] bytes1 = BitConverter.GetBytes(val1);
                                byte[] bytes2 = BitConverter.GetBytes(val2);
                                byte[] bytes = new byte[] { bytes1[0], bytes1[1], bytes2[0], bytes2[1] };
                                int val = BitConverter.ToInt32(bytes, 0);
                                Value += val.ToString();
                            }
                        }
                        else if (type.Contains(EDataType.Float.ToString()))
                        {
                            int length = Convert.ToInt32(info.RegisterInfo.Length);
                            Value = "";
                            for (int i = 0; i < length; i = i + 2)
                            {
                                short val1 = Convert.ToInt16(InputData[Index + i], 2);
                                short val2 = Convert.ToInt16(InputData[Index + i + 1], 2);
                                byte[] bytes1 = BitConverter.GetBytes(val1);
                                byte[] bytes2 = BitConverter.GetBytes(val2);
                                byte[] bytes = new byte[] { bytes1[0], bytes1[1], bytes2[0], bytes2[1] };
                                float val = BitConverter.ToSingle(bytes, 0);
                                Value += val.ToString();
                            }
                        }
                        else if (type.Contains(EDataType.Double.ToString()))
                        {
                            int length = Convert.ToInt32(info.RegisterInfo.Length);
                            Value = "";
                            for (int i = 0; i < length; i = i + 4)
                            {
                                short val1 = Convert.ToInt16(InputData[Index + i], 2);
                                short val2 = Convert.ToInt16(InputData[Index + i + 1], 2);
                                short val3 = Convert.ToInt16(InputData[Index + i + 2], 2);
                                short val4 = Convert.ToInt16(InputData[Index + i + 3], 2);
                                byte[] bytes1 = BitConverter.GetBytes(val1);
                                byte[] bytes2 = BitConverter.GetBytes(val2);
                                byte[] bytes3 = BitConverter.GetBytes(val3);
                                byte[] bytes4 = BitConverter.GetBytes(val4);
                                byte[] bytes = new byte[] { bytes1[0], bytes1[1], bytes2[0], bytes2[1], bytes3[0], bytes3[1], bytes4[0], bytes4[1] };
                                double val = BitConverter.ToDouble(bytes, 0);
                                Value += val.ToString();
                            }
                        }

                        iOutputData.Add(info.RegisterInfo.Name, Value);

                    }
                    OutputData = new Dictionary<string, string>(iOutputData);

                    return true;
                }
                else { OutputData = null; return false; }
            }
        }


        short[] CurData = new short[Int16.MaxValue];
        short[] LastData = null;

        public void LoopRead(string registeraddress, int length)
        {
            int MaxReadLength = 5000;//一次读取的最大长度
            for (int Index = 0; Index < length; Index = Index + MaxReadLength)
            {
                short[] curdata = new short[MaxReadLength];
                int plcadd = Convert.ToInt32(registeraddress.ReplaceLetter()) + Index;
                //读取数据
                ReadRegister(out curdata, registeraddress, length);
                if (curdata == null) throw new Exception("MC read fail");
                    Array.Copy(curdata, 0, CurData, plcadd, curdata.Length);
            }

            ///第一次执行时 先复制读取数据 否则当打开等于1时 会被触发
            if(LastData==null) LastData= CurData.DeepClone();

            var ret = BitOperationEx.Compare_Ptr(CurData, LastData,  EComparePolarity.RisingEdge);

            if (ret!=null&&ret.Count>0)
            {
                foreach (var item in ret)
                {
                    var ievent = RegisterCollection.FirstOrDefault(s => s.Address == item.Index);
                
                    if (ievent!=null&& !ievent.IsTrigger)
                    {
                        ievent.Value = item.Value_Current;
                        ievent.TriggerCallBack(ievent);
                        ievent.IsTrigger = true;
                    }

                }
            }
            RegisterCollection.ForEach(s => s.IsTrigger = false);
            LastData = CurData.DeepClone();
        }

       /// <summary>
       /// 读取数据 根据寄存器
       /// </summary>
       /// <param name="pRegisterInfo"></param>
       /// <returns></returns>
        public object Read(CRegisterManager pRegisterInfo)
        {
            CRegisterManager info = pRegisterInfo;
            if (info == null) return null;
          
            var add=Convert.ToInt32( info.RegisterInfo.Address.ReplaceLetter());

            string type = info.RegisterInfo.Type.ToString();
            if (type.Contains(EDataType.String.ToString()))
            {
                short[] ushorts = new short[info.RegisterInfo.Length];
                for (int index = 0; index < info.RegisterInfo.Length; index++)
                    ushorts[index] = CurData[info.Address + index];

                return ushorts.ToASC();
            }
            else if (type.Contains(EDataType.Bit.ToString()))
            {
                int length = Convert.ToInt32(info.RegisterInfo.Length.ToString().Split('.')[1]);
                return CurData[add].ToBinary().Substring(16 - length, 1);
            }
            else if (type.Contains(EDataType.Short.ToString()))
            {
                return CurData[add]; // 返回short类型
            }
            else if (type.Contains(EDataType.Int.ToString()))
            {
                byte[] bytes1 = BitConverter.GetBytes(CurData[add]);
                byte[] bytes2 = BitConverter.GetBytes(CurData[add + 1]);
                byte[] bytes = new byte[] { bytes1[0], bytes1[1], bytes2[0], bytes2[1] };
                return bytes.ToShort(); // 返回int类型
            }
            else if (type.Contains(EDataType.Float.ToString()))
            {
                byte[] bytes1 = BitConverter.GetBytes(CurData[add]);
                byte[] bytes2 = BitConverter.GetBytes(CurData[add + 1]);
                byte[] bytes = new byte[] { bytes1[0], bytes1[1], bytes2[0], bytes2[1] };
                return bytes.ToFloat(); // 返回float类型
            }
            else if (type.Contains(EDataType.Double.ToString()))
            {
                byte[] bytes1 = BitConverter.GetBytes(CurData[add]);
                byte[] bytes2 = BitConverter.GetBytes(CurData[add + 1]);
                byte[] bytes3 = BitConverter.GetBytes(CurData[add + 2]);
                byte[] bytes4 = BitConverter.GetBytes(CurData[add + 3]);
                byte[] bytes = new byte[] { bytes1[0], bytes1[1], bytes2[0], bytes2[1], bytes3[0], bytes3[1], bytes4[0], bytes4[1] };
                return bytes.ToDouble(); // 返回double类型
            }
                return null;
        }
 
        /// <summary>
        /// 读取寄存器数据并返回指定类型
        /// </summary>
        /// <typeparam name="T">期望返回的数据类型</typeparam>
        /// <param name="pRegisterInfo">寄存器信息</param>
        /// <returns>指定类型的数据</returns>
        public T Read<T>(CRegisterManager pRegisterInfo)
        {
            object value = Read(pRegisterInfo);
    
            if (value == null)
                return default(T);
        
            // 如果类型匹配，直接返回
            if (value is T)
                return (T)value;
        
            // 特殊处理：如果期望返回string但实际是数值类型
            if (typeof(T) == typeof(string) && !(value is string))
            {
                return (T)(object)value.ToString();
            }
        
            // 如果类型不匹配，尝试转换
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default(T);
            }
        }


        /// <summary>
        /// 读取数据 根据名称
        /// </summary>
        /// <param name="pName">寄存器名称</param>
        /// <returns>根据寄存器类型返回的相应数据</returns>
        public object Read(string pName)
        {
            var data = GetRegister(pName);
            return Read(data);
        }

        /// <summary>
        /// 读取数据 根据名称并返回指定类型
        /// </summary>
        /// <typeparam name="T">期望返回的数据类型</typeparam>
        /// <param name="pName">寄存器名称</param>
        /// <returns>指定类型的数据</returns>
        public T Read<T>(string pName)
        {
            var data = GetRegister(pName);
            return Read<T>(data);
        }

        /// <summary>
        /// 读取数据 根据名称进行地址偏移
        /// </summary>
        /// <param name="pName">寄存器名称</param>
        /// <param name="OffestAdd">地址偏移</param>
        /// <returns>根据寄存器类型返回的相应数据</returns>
        public object Read(string pName, int OffestAdd)
        {
            var data = GetRegister(pName, OffestAdd)  ;
            return Read(data);
        }

        /// <summary>
        /// 读取数据 根据名称进行地址偏移并返回指定类型
        /// </summary>
        /// <typeparam name="T">期望返回的数据类型</typeparam>
        /// <param name="pName">寄存器名称</param>
        /// <param name="OffestAdd">地址偏移</param>
        /// <returns>指定类型的数据</returns>
        public T Read<T>(string pName, int OffestAdd)
        {
            var data = GetRegister(pName, OffestAdd);
            return Read<T>( data);
        }



        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="pName">地址名称</param>
        /// <param name="pValue">数据</param>
        public void Write(string pName, object pValue)
        {
            var data = GetRegister(pName).DeepClone();
            data.Value = pValue;
            Write(data);
        }

        /// <summary>
        /// 写入数据 根据地址偏移
        /// </summary>
        /// <param name="pName">寄存器名称</param>
        /// <param name="OffestAdd">寄存器名称基础上进行地址偏移</param>
        /// <param name="pValue">数据</param>
        public void Write(string pName, int OffestAdd, object pValue)
        {
            var data = GetRegister(pName, OffestAdd);
            data.Value = pValue;
            Write(data);
        }

        /// <summary>
        /// 写入数据 根据寄存器
        /// </summary>
        /// <param name="pRegisterInfo"></param>
        public void Write(CRegisterManager pRegisterInfo)
        {
            var d1 = pRegisterInfo.Value;
            if (d1 == null) return;
            if (d1 is string && string.IsNullOrEmpty(d1.ToString())) return;

            CRegisterManager info = pRegisterInfo;
            ERegisterType registerType = EnumEx.GetEnum<ERegisterType>(RegexEx.ReplaceLetter(info.RegisterInfo.Address));
            
            string type = info.RegisterInfo.Type.ToString();
            if (type.Contains(EDataType.String.ToString()))
            {
                WriteRegister(info.RegisterInfo.Address, info.Value.ToString());
            }
            else if (type.Contains(EDataType.Bit.ToString()))
            {
                WriteRegister(info.RegisterInfo.Address,  Convert.ToInt16( info.Value));
            }
            else if (type.Contains(EDataType.Short.ToString()))
            {
                WriteRegister(info.RegisterInfo.Address, Convert.ToInt16(info.Value));
            }
            else if (type.Contains(EDataType.Int.ToString()))
            {
                WriteRegister(info.RegisterInfo.Address, Convert.ToInt32(info.Value));
            }
            else if (type.Contains(EDataType.Float.ToString()))
            {
                WriteRegister(info.RegisterInfo.Address, Convert.ToSingle(info.Value));

            }
            else if (type.Contains(EDataType.Double.ToString()))
            {
                WriteRegister(info.RegisterInfo.Address, Convert.ToDouble(info.Value));
            }
        }

        /// <summary>
        /// 设置心跳信号
        /// </summary>
        /// <param name="registeraddress"></param>
        /// <param name="pValue"></param>
        public void SetHeartbeat(string registeraddress, short pValue)
        {
            Actions.Add(new Action(() => {
                IsOpen =  WriteRegister(registeraddress, pValue);
            }));
        }

        /// <summary>
        /// 设置读取任务
        /// </summary>
        /// <param name="registeraddress"></param>
        /// <param name="length"></param>
        public void SetRead(string registeraddress, int length)
        {
            Actions.Add(new Action(() => {
                LoopRead(registeraddress, length);
            }));
        }

        /// <summary>
        /// 开启循环任务
        /// </summary>
        /// <param name="pInterval">循环间隔时间 ms</param>
        public void StartLoop(int pInterval = 500)
        {
            // 设置Task任务的优先级
             Task.Factory.StartNew(async () =>
             {
                 while (!Token.IsCancellationRequested)
                 {
                         foreach (var item in Actions)
                             item();
                     
                     await Task.Delay(pInterval);
                 }
             }, Token.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <summary>
        /// 停止循环
        /// </summary>
        public void StopLoopRead()
        {
            Token?.Cancel();
        }


    }
}
