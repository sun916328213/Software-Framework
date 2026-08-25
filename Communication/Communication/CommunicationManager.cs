using Communication.MC;
using Extension.Core.CompareEx;
using Extension.Core.ConvertEx;
using Extension.Core.DataEx;
using Extension.Core.EnumEx;
using Prism.Ioc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Communication
{
    public class CommunicationManager
    {
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        ConcurrentDictionary<string, short[]> CurData = new ConcurrentDictionary<string, short[]>();
        ConcurrentDictionary<string, short[]> LastData = new ConcurrentDictionary<string, short[]>();


        private List< Action> LoopTask = new List<Action>();
        MCHelper _MCHelper;
 
        public CommunicationManager(IContainerProvider container)
        {
            _MCHelper = container.Resolve<MCHelper>();
        }

        public short[] GetData(string taskName)
        {
            if (CurData.TryGetValue(taskName, out short[] data))
                return data;
            return null;
        }

        public void LoopRead(string taskName, string registeraddress, int length,  EComparePolarity comparePolarity )
        {
            try
            {
                short[] shorts = new short[Int16.MaxValue];
                int MaxReadLength = 5000;//一次读取的最大长度
                for (int Index = 0; Index < length; Index = Index + MaxReadLength)
                {
                    short[] curdata = new short[MaxReadLength];
                    int plcadd = Convert.ToInt32(registeraddress.ReplaceLetter()) + Index;
                    //读取数据
                    curdata = _MCHelper.ReadRegister(registeraddress, length);
                    if (curdata == null)
                    {
                        throw new Exception("MC read fail");
                    }
                    Array.Copy(curdata, 0, shorts, plcadd, curdata.Length);
                }

                ///第一次执行时 先复制读取数据  
                CurData[taskName] = shorts.DeepClone();
                ///第一次执行时 先复制读取数据 否则当打开等于1时 会被触发
                if (LastData[taskName] == null) LastData[taskName] = shorts.DeepClone();

                if(comparePolarity== EComparePolarity.None) return;  //不需要比较

                var ret = BitOperationEx.Compare_Ptr(CurData[taskName], LastData[taskName], comparePolarity);

                if (ret != null && ret.Count > 0)
                {
                    foreach (var item in ret)
                    {
                        var ievent = _MCHelper.RegisterCollection.FirstOrDefault(s => s.Address == item.Index);

                        if (ievent != null && !ievent.IsTrigger)
                        {
                            ievent.Value = item.Value_Current;
                            ievent.TriggerCallBack(ievent);
                            ievent.IsTrigger = true;
                        }

                    }
                }
                _MCHelper.RegisterCollection.ForEach(s => s.IsTrigger = false);
                LastData[taskName] = CurData[taskName].DeepClone();
            }
            catch (Exception ex) { 
               
            }
        }


        /// <summary>
        /// 设置写入任务
        /// </summary>
        /// <param name="registeraddress"></param>
        /// <param name="pValue"></param>
        public void SetWriteTask(string registeraddress, short pValue)
        {
            LoopTask.Add(() =>
            {
                _MCHelper.IsConnected = Write(registeraddress, pValue);
            });
        }

        /// <summary>
        /// 设置读取任务
        /// </summary>
        /// <param name="registeraddress"></param>
        /// <param name="length"></param>
        public void SetReadTask(string taskName, string registeraddress, int length,EComparePolarity comparePolarity)
        {
            CurData.TryAdd(taskName,null);
            LastData.TryAdd(taskName, null);
            int Num = Convert.ToInt32(registeraddress.ReplaceLetter());
            string letter = registeraddress.ReplaceNum();
            _MCHelper.RegisterCollection.Where(s => s.Address >= Num &&
                 s.Address < (Num + length) &&
                 s.RegisterInfo.Address.ReplaceNum() == letter).Select(s => { s.TaskName = taskName; return s; }).ToList();

            LoopTask.Add(() =>
            {
                LoopRead(taskName, registeraddress, length, comparePolarity);
            });
        }

        /// <summary>
        /// 设置心跳信号
        /// </summary>
        /// <param name="registeraddress"></param>
        /// <param name="pValue"></param>
        public void SetWrite(string registeraddress, short pValue)
        {
            LoopTask.Add(new Action(() => {
                _MCHelper.IsConnected = _MCHelper.WriteRegister(registeraddress, pValue);
            }));
        }

        /// <summary>
        /// 设置读取任务
        /// </summary>
        /// <param name="registeraddress"></param>
        /// <param name="length"></param>
        public void SetRead(string taskName,string registeraddress, int length, EComparePolarity comparePolarity)
        {
            LoopTask.Add(new Action(() => {
                LoopRead(taskName,registeraddress, length, comparePolarity);
            }));
        }

        /// <summary>
        /// 开启循环任务
        /// </summary>
        /// <param name="pInterval">循环间隔时间 ms</param>
        public void StartLoop(int pInterval = 500)
        {
            Stopwatch stopwatch = new Stopwatch();
            // 设置Task任务的优先级
            Task.Factory.StartNew(async () =>
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        int index = 0;
                        foreach (var item in LoopTask)
                        {
                            index++;
                            stopwatch.Restart();
                            item();
                            stopwatch.Stop();
                            //Console.WriteLine($"{index} 读取时间: {stopwatch.ElapsedMilliseconds}");
                        }
                    }
                    catch 
                    { 
                    
                    }
                    await Task.Delay(pInterval);
                }
            }, cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <summary>
        /// 停止循环
        /// </summary>
        public void StopLoopRead()
        {
            cancellationTokenSource?.Cancel();
        }


        /// <summary>
        /// 读取数据 根据寄存器
        /// </summary>
        /// <param name="pRegisterInfo"></param>
        /// <returns></returns>
        public object Read(CRegisterManager pRegisterInfo, EEndianType  endianType= EEndianType.Little_Endian)
        {
            CRegisterManager info = pRegisterInfo;
            if (info == null) return null;

            var add = Convert.ToInt32(info.RegisterInfo.Address.ReplaceLetter());

            var data = CurData[info.TaskName];

            if (data == null) return null;

            string type = info.RegisterInfo.Type.ToString();
            if (type.Contains(EDataType.String.ToString()))
            {
                short[] ushorts = new short[info.RegisterInfo.Length];
                for (int index = 0; index < info.RegisterInfo.Length; index++)
                    ushorts[index] = data[info.Address + index];

                return ushorts.ToASC(endianType);
            }
            else if (type.Contains(EDataType.Bit.ToString()))
            {
                int length = Convert.ToInt32(info.RegisterInfo.Length.ToString().Split('.')[1]);
                return data[add].ToBinary().Substring(16 - length, 1);
            }
            else if (type.Contains(EDataType.Short.ToString()))
            {
                return data[add]; // 返回short类型
            }
            else if (type.Contains(EDataType.Int.ToString()))
            {
                byte[] bytes1 = BitConverter.GetBytes(data[add]);
                byte[] bytes2 = BitConverter.GetBytes(data[add + 1]);
                byte[] bytes = new byte[] { bytes1[0], bytes1[1], bytes2[0], bytes2[1] };
                return bytes.ToShort(endianType); // 返回int类型
            }
            else if (type.Contains(EDataType.Float.ToString()))
            {
                byte[] bytes1 = BitConverter.GetBytes(data[add]);
                byte[] bytes2 = BitConverter.GetBytes(data[add + 1]);
                byte[] bytes = new byte[] { bytes1[0], bytes1[1], bytes2[0], bytes2[1] };
                return bytes.ToFloat(endianType); // 返回float类型
            }
            else if (type.Contains(EDataType.Double.ToString()))
            {
                byte[] bytes1 = BitConverter.GetBytes(data[add]);
                byte[] bytes2 = BitConverter.GetBytes(data[add + 1]);
                byte[] bytes3 = BitConverter.GetBytes(data[add + 2]);
                byte[] bytes4 = BitConverter.GetBytes(data[add + 3]);
                byte[] bytes = new byte[] { bytes1[0], bytes1[1], bytes2[0], bytes2[1], bytes3[0], bytes3[1], bytes4[0], bytes4[1] };
                return bytes.ToDouble(endianType); // 返回double类型
            }
            return null;
        }

        /// <summary>
        /// 配置寄存器信息
        /// </summary>
        /// <param name="pRegisterInfos"></param>
        /// <returns></returns>
        public void LoadConfig(List<CRegisterInfo> pRegisterInfos)
        {
            _MCHelper.RegisterCollection.Clear();
            pRegisterInfos.ForEach(s =>
            {
                _MCHelper.RegisterCollection.Add(new CRegisterManager(s));
            });
        }

        /// <summary>
        /// 获取寄存器信息
        /// </summary>
        /// <param name="pName"></param>
        /// <returns></returns>
        public CRegisterManager GetRegister(string pName)
        {
            return _MCHelper.RegisterCollection.FirstOrDefault(s => s.RegisterInfo.Name == pName);
        }

        /// <summary>
        /// 根据寄存器名称获取寄存器索引 并加上偏移地址
        /// </summary>
        /// <param name="pName">寄存器名称</param>
        /// <param name="offestAddress">寄存器偏移地址</param>
        /// <returns></returns>
        public CRegisterManager GetRegister(string pName, int offestAddress)
        {   

            var data = GetRegister(pName);
            if (data == null) return null;
            int address = data.Address + offestAddress;

            //return _MCHelper.RegisterCollection.FirstOrDefault(s => s.Address == address);//只获取集合中存在的寄存器信息
            CRegisterInfo registerInfo = data.RegisterInfo.DeepClone();
            registerInfo.Address= registerInfo.Address.ReplaceNum()+ address.ToString();
            //CRegisterManager registerManager = new CRegisterManager(registerInfo);
            //return registerManager;

            return _MCHelper.RegisterCollection.FirstOrDefault(s => s.Address == address);//只获取集合中存在的寄存器信息
        }


        /// <summary>
        /// 读取数据 根据名称
        /// </summary>
        /// <param name="pName">寄存器名称</param>
        /// <returns>根据寄存器类型返回的相应数据</returns>
        public object Read(string pName)
        {
            var data = GetRegister(pName);
            if (data == null) return false;
            return Read(data);
        }

        /// <summary>
        /// 读取数据 根据名称进行地址偏移
        /// </summary>
        /// <param name="pName">寄存器名称</param>
        /// <param name="OffestAdd">地址偏移</param>
        /// <returns>根据寄存器类型返回的相应数据</returns>
        public object Read(string pName, int OffestAdd)
        {
            var data = GetRegister(pName, OffestAdd);
            if (data == null) return false;
            return Read(data);
        }

        /// <summary>
        /// 连续读取
        /// </summary>
        /// <param name="add"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public short[] ReadContinuous(string add, int length)
        {
            var data = _MCHelper.ReadRegister(add, length);
            return data;
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="pName">地址名称</param>
        /// <param name="pValue">数据</param>
        public bool Write(string pName, object pValue)
        {
            var data = GetRegister(pName);
            if (data == null) return false;
            data.Value = pValue;
            return Write(data);
        }

        /// <summary>
        /// 写入数据 根据地址偏移
        /// </summary>
        /// <param name="pName">寄存器名称</param>
        /// <param name="OffestAdd">寄存器名称基础上进行地址偏移</param>
        /// <param name="pValue">数据</param>
        public bool Write(string pName, int OffestAdd, object pValue)
        {
            var data = GetRegister(pName, OffestAdd);
            if (data == null) return false;
            data.Value = pValue;
            return Write(data);
        }

        /// <summary>
        /// 写入数据 根据寄存器
        /// </summary>
        /// <param name="pRegisterInfo"></param>
        public bool Write(CRegisterManager pRegisterInfo)
        {
            var d1 = pRegisterInfo.Value;
            if (d1 == null) return false;
            if (d1 is string && string.IsNullOrEmpty(d1.ToString())) return false;

            CRegisterManager info = pRegisterInfo;
            ERegisterType registerType = EnumEx.GetEnum<ERegisterType>(RegexEx.ReplaceLetter(info.RegisterInfo.Address));

            string type = info.RegisterInfo.Type.ToString();
            if (type.Contains(EDataType.String.ToString()))
            {
                return _MCHelper.WriteRegister(info.RegisterInfo.Address, info.Value.ToString().ToShortArray());
            }
            else if (type.Contains(EDataType.Bit.ToString()))
            {
                return _MCHelper.WriteRegister(info.RegisterInfo.Address, Convert.ToInt32(info.Value).ToShortArray());
            }
            else if (type.Contains(EDataType.Short.ToString()))
            {
                return _MCHelper.WriteRegister(info.RegisterInfo.Address, Convert.ToInt16(info.Value) );
            }
            else if (type.Contains(EDataType.Int.ToString()))
            {
                return _MCHelper.WriteRegister(info.RegisterInfo.Address, Convert.ToInt32(info.Value).ToShortArray());
            }
            else if (type.Contains(EDataType.Float.ToString()))
            {
                return _MCHelper.WriteRegister(info.RegisterInfo.Address, Convert.ToSingle(info.Value).ToShortArray());

            }
            else if (type.Contains(EDataType.Double.ToString()))
            {
                return _MCHelper.WriteRegister(info.RegisterInfo.Address, Convert.ToDouble(info.Value).ToShortArray());
            }
            else
                return false;
        }

        public bool Reset(CRegisterManager pRegisterInfo, int length)
        { 

            CRegisterManager info = pRegisterInfo;
            ERegisterType registerType = EnumEx.GetEnum<ERegisterType>(RegexEx.ReplaceLetter(info.RegisterInfo.Address));

            return _MCHelper.WriteRegister(info.RegisterInfo.Address, new short[length]);
        }

        public bool Reset(string pName, int OffestAdd, int  length)
        {
            var data = GetRegister(pName, OffestAdd);
            if (data == null) return false;
           return Reset(data, length);
        }
    }
}
