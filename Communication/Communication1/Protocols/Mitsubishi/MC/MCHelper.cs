using ActUtlType64Lib;
using Communication.Communication;
using Communication.Communication.Socket;
using Communication.Core.Definition;
using Communication.Tools.Stream.FrameSplitter.Splitter;
using Extension.Core.ConvertEx;
using Prism.Ioc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Communication.Protocols.Mitsubishi
{
    public class MCHelper : MCBase
    {
        IContainerProvider containerProvider;
        public MCHelper(IContainerProvider container) : base(container)
        {
            containerProvider=container;
        }

        /// <summary>
        /// 用于生成运行唯一码的锚
        /// </summary>
        int mGeneration;

        private ActUtlType64Class CM_Utl;

        /// <summary>
        /// 站号 
        /// </summary>
        int mStationnumber;
        /// <summary>
        /// 密码
        /// </summary>
        string mPassword;


        // 用于生成并匹配请求ID ，（发一个等一个），这是最安全的做法。
        private readonly SemaphoreSlim mLock = new SemaphoreSlim(1, 1);

        private TaskCompletionSource<byte[]> mCurrentTcs;

        public override CommunicationParam.DeviceProtocolType CommunicationType => CommunicationParam.DeviceProtocolType.MC;

        public override bool IsConnected { get ; set ; }

        public void Config(int stationnumber,string password)
        {
            mStationnumber=stationnumber;
            mPassword=password;
        }

        public override Task Connect()
        {
            if (CM_Utl == null) CM_Utl = new ActUtlType64Class();
            CM_Utl.ActLogicalStationNumber = mStationnumber;
            CM_Utl.ActPassword = mPassword;
            if (IsConnected == true) CM_Utl.Close();
            int rect = CM_Utl.Open();
            if (rect == 0)
                IsConnected = true;
            else
                IsConnected = false;
            return Task.CompletedTask;
        }

        public override void Disconnect()
        {
            int ret = CM_Utl.Close();

            IsConnected = false;
        }

        public override async Task ReadAsync(CancellationToken ct = default)
        {
            await Task.Run(() =>
            {
                // 遍历所有已分类和排序的读取块
                foreach (var kvp in mReadList)
                {
                    E_McDeviceCode code = kvp.Key;
                    List<AddressBlock> blocks = kvp.Value;

                    foreach (var block in blocks)
                    {
                        int startAddr = block.StartAddress;
                        int readLength = block.Length;
                        short[] DeviceValue = ReadRegister(code.ToString() + startAddr, readLength);

                        FillData(code, DeviceValue, startAddr, true);
                    }
                }
            });
        }

        public override async Task<DeviceValue> ReadAsync(ICommunicationDataPoint dp, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                McDataPoint mp = dp as McDataPoint;
                E_McDeviceCode code = mp.DecodeData.Area;
                string startAddr = mp.Input;
                int readLength = mp.GetLength();
                short[] DeviceValue = ReadRegister( startAddr, readLength);

                FillData(code, DeviceValue, mp.DecodeData.Address, false );
                return dp.GetValue();
            });
        }

        public override void Write(params (ICommunicationDataPoint dp, object value)[] pvs)
        {
            foreach (var pv in pvs)
            {
                Write(pv.dp, pv.value);
            }
        }

        public override void Write(ICommunicationDataPoint dp, object value)
        {
            McDataPoint mp = dp as McDataPoint;
            E_McDeviceCode code = mp.DecodeData.Area;
            string startAddr = mp.Input;
            int readLength = mp.GetLength();
            byte[] d = ValueDecoder.Code(value, mp.DataType, (ushort)(mp.GetLength() * 2));
     
            var shorts = d.ToShort( EEndianType.Little_Endian);
            WriteRegister(startAddr, readLength, shorts);
        }

        public override async Task WriteAsync(params (ICommunicationDataPoint dp, object value)[] pvs)
        {
            await Task.Run(async () =>
            {
                foreach (var pv in pvs)
                {
                    await WriteAsync(pv.dp, pv.value);
                }
            });
        }

        public override async Task WriteAsync(ICommunicationDataPoint dp, object value)
        {
            await Task.Run(() =>
            {
                Write(dp, value);
            });
        }


        /// <summary>
        /// 填充数据
        /// </summary>
        /// <param name="code">寄存器类型</param>
        /// <param name="DeviceValue">寄存器数据</param>
        /// <param name="startAddr">寄存器开始地址</param>
        private void FillData(E_McDeviceCode code, short[] DeviceValue, int startAddr,bool IsCompare)
        {
            switch (code)
            {
                case E_McDeviceCode.M:
                case E_McDeviceCode.X:
                case E_McDeviceCode.Y:
                case E_McDeviceCode.B:
                    bool[] bools = new bool[DeviceValue.Length * 16];
                    int index = 0;
                    foreach (short val in DeviceValue)
                    {
                        for (int i = 0; i < 16; i++)
                            bools[index++] = (val & (1 << i)) != 0;
                    }
                    ////进行比较,复制，触发变更
                    ValueCompare_Bool(code, (bool[])mValueGroup[code], startAddr, bools.ToArray(), IsCompare);
                    break;
                case E_McDeviceCode.D:
                case E_McDeviceCode.W:
                case E_McDeviceCode.R:
                    byte[] byteArray = new byte[DeviceValue.Length * 2];
                    Buffer.BlockCopy(DeviceValue, 0, byteArray, 0, byteArray.Length);
                    ////进行比较,复制，触发变更
                    ValueCompare_Byte(code, (byte[])mValueGroup[code], startAddr * 2, byteArray, 0, IsCompare);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 读取寄存器数据
        /// </summary>
        /// <param name="startAddr">开始地址</param>
        /// <param name="readLength">读取长度</param>
        /// <returns>返回结果</returns>
        private short[] ReadRegister(  string startAddr,int readLength)
        {
            short[] DeviceValue = new short[readLength];
            CM_Utl.ReadDeviceBlock2(  startAddr.ToString(), readLength, out DeviceValue[0]);
            return DeviceValue;
        }

        /// <summary>
        /// 写入寄存器地址
        /// </summary>
        /// <param name="startAddr">开始地址</param>
        /// <param name="readLength">读取长度</param>
        /// <param name="data">数据</param>
        public void WriteRegister(string startAddr, int readLength, short  data)
        {
            CM_Utl.WriteDeviceBlock2(startAddr, readLength, ref data);
        }
    }
}
