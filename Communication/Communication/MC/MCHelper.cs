using ActProgType64Lib;
using ActUtlType64Lib;
using System.Collections.Generic;
using System.Threading;

namespace Communication.MC
{
    enum ActType
    {
        ActUtlType,
        ActProgType
    }
    public class MCHelper
    {
        private ActUtlType64Class CM_Utl;
        private ActProgType64Class CM_Prog;
        private ActType ActType;
        private CancellationTokenSource Token = new CancellationTokenSource();
 

        private object obj_write = new object();
        private object obj_read = new object();

        /// <summary>
        /// 寄存器信息集合
        /// </summary>
        public List<CRegisterManager> RegisterCollection { get; set; } = new List<CRegisterManager>();

        /// <summary>
        /// 是否连接
        /// </summary>
        public bool IsConnected { get; set; } = false;
 
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
            if (IsConnected == true) CM_Utl.Close();
            int rect = CM_Utl.Open();
            if (rect == 0)
            {
                IsConnected = true;
            }
            else
            {
                IsConnected = false;
            }
            return IsConnected;
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
                IsConnected  = true;
            }
            else
            {
                IsConnected = false;
            }
            return IsConnected;
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
                IsConnected = true;
            }
            else
            {
                IsConnected = true;
            }
            return IsConnected;
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

            IsConnected  = false;
            if (rect == 0) return true;
            else return false;
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
                    if (IsConnected)
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
                    if (IsConnected)
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
        /// 读取寄存器,格式为short,占16个bit  基础读取方式
        /// </summary>
        /// <param name="registeraddress">寄存器地址</param>
        /// <param name="length">寄存器长度,16bit=1</param>
        /// <returns></returns>
        internal short[] ReadRegister(  string registeraddress, int length = 1)
        {
            lock (obj_read)
            {
                if (JudgeIsConnect() == false) return null;
                int rect;
                short[] DeviceValue = new short[length];
                if (ActType == ActType.ActUtlType)
                    rect = CM_Utl.ReadDeviceBlock2(registeraddress, length, out DeviceValue[0]);
                else
                    rect = CM_Prog.ReadDeviceBlock2(registeraddress, length, out DeviceValue[0]);
                return DeviceValue;
            }
        }

        /// <summary>
        /// 写入寄存器,格式short,占16个bit  最基础函数
        /// </summary>
        /// <param name="registeraddress">寄存器地址</param>
        /// <param name="Data">数据,short=16bit</param>
        /// <returns></returns>
        internal bool WriteRegister(string registeraddress,params short[] Data)
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

     
 
    }
}
