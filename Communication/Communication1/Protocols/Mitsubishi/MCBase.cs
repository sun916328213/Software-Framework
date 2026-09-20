using Communication.Communication;
using Communication.Core.Definition;
using Extension.Core.Comparison;
using Prism.Ioc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Communication.Protocols.Mitsubishi
{
    /// <summary>
    /// 数据块描述
    /// </summary>
    internal class AddressBlock
    {
        public int StartAddress { get; set; }
        public int Length { get; set; }
        public int EndAddress => StartAddress + Length - 1;
    }

    public abstract class MCBase : Core.Abstracts.DeviceCommunication
    {
        IContainerProvider containerProvider;
        protected MCBase(IContainerProvider container) : base(container)
        {
            containerProvider= container;
        }

        // SLMP 协议限制：单个块或单次读取通常最大不超过 960 个字 (视具体PLC型号和指令而定，这里保守设为 960)
        const int MAX_BLOCK_SIZE = 960;
        const int MAX_BLOCK_SIZE_BOOL = 960 * 16;
 
        /// <summary>
        /// 用于生成运行唯一码的锚
        /// </summary>
        int mGeneration;

        /// <summary>
        /// 连续读取时允许地址空位
        /// </summary>
        public ushort HoleThreshold { get; set; } = 480;

        // 正则表达式
        protected readonly Regex mAddrRegex = new Regex(@"^(?<Type>[A-Za-z]{1,2})(?<Addr>[0-9A-Fa-f]+)(?<Suffix>H)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public override ICommunicationDataPoint this[string name]
        {
            get
            {
                if (mNameIndex.ContainsKey(name))
                {
                    return mNameIndex[name];
                }
                else
                {
                    return null;
                }
            }
        }

        public override bool CanAutoRead { get; } = true;

        /// <summary>
        /// 用于记录所属的所有数据点
        /// </summary>
        protected Dictionary<string, ICommunicationDataPoint> mNameIndex = new Dictionary<string, ICommunicationDataPoint>();

        /// <summary>
        /// 数据集合
        /// </summary>
        protected Dictionary<E_McDeviceCode, Array> mValueGroup = new Dictionary<E_McDeviceCode, Array>();

        /// <summary>
        /// 输入的寄存器原始数据
        /// </summary>
        protected Dictionary<E_McDeviceCode, List<int>> mReadData_Base = new Dictionary<E_McDeviceCode, List<int>>();

        /// <summary>
        /// 用于回调进行索引的字典
        /// </summary>
        protected Dictionary<E_McDeviceCode, Dictionary<int, List<McDataPoint>>> mEventMap = new Dictionary<E_McDeviceCode, Dictionary<int, List<McDataPoint>>>();

        /// <summary>
        /// 用于排序后的读取列表
        /// </summary>
        internal Dictionary<E_McDeviceCode, List<AddressBlock>> mReadList = new Dictionary<E_McDeviceCode, List<AddressBlock>>();


        /// <summary>
        /// 初始化数据数组长度
        /// </summary>
        /// <param name="code"></param>
        /// <param name="num"></param>
        public void ConfigValueArray(E_McDeviceCode code, ushort num)
        {
            if (!mValueGroup.ContainsKey(code))
            {
                switch (code)
                {
                    case E_McDeviceCode.M:
                    case E_McDeviceCode.X:
                    case E_McDeviceCode.Y:
                        mValueGroup.Add(code, new bool[num]);
                        break;
                    case E_McDeviceCode.D:
                    case E_McDeviceCode.R:
                        mValueGroup.Add(code, new byte[num * 2]);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                throw new Exception($"数据数组 {code} 已经被初始化，无法重复初始化。");
            }
            if (!mReadData_Base.ContainsKey(code))
            {
                mReadData_Base.Add(code, new List<int>());
            }
            if (!mEventMap.ContainsKey(code))
            {
                mEventMap.Add(code, new Dictionary<int, List<McDataPoint>>());
            }
        }

        /// <summary>
        /// 添加寄存器点位
        /// </summary>
        /// <param name="point"></param>
        /// <exception cref="Exception"></exception>
        /// <exception cref="FormatException"></exception>
        public void AddDataPoint(ICommunicationDataPoint point)
        {
            McDataPoint dp = point as McDataPoint;
            if (dp == null)
            {
                throw new Exception($"{point.Name}:{point.Input} 地址不是Mc3EDataPoint类似，无法进行转换。");
            }

            // 正则格式校验与拆分
            var match = mAddrRegex.Match(dp.Input);
            if (!match.Success)
            {
                throw new FormatException($"地址格式错误: {point.Name}:{point.Input}。应为 '字母+数字' 格式。");
            }

            string typeStr = match.Groups["Type"].Value;
            string addrStr = match.Groups["Addr"].Value;
            bool hasHexSuffix = match.Groups["Suffix"].Success; // 检查是否有 H 后缀
            int addrNum = dp.GetLength();

            E_McDeviceCode dc;

            if (!mNameIndex.ContainsKey(point.Name))
            {
                mNameIndex.Add(point.Name, point);
            }

            if (!Enum.TryParse(typeStr, out dc))
            {
                throw new FormatException($"地址格式错误: {point.Name}:{point.Input}。{typeStr}不被支持。");
            }
            else
            {
                if (!mValueGroup.ContainsKey(dc))
                {
                    throw new FormatException($"地址格式错误: {point.Name}:{point.Input}。{typeStr}对应的内存1缓存没有被初始化。");
                }
                else
                {
                    ushort address = 1;
                    if (hasHexSuffix)//16进制需要转化到10进制参与计算
                    {
                        address = Convert.ToUInt16(addrStr, 16);
                    }
                    else
                    {
                        address = Convert.ToUInt16(addrStr);
                    }


                    dp.DecodeData.Area = dc;
                    dp.DecodeData.Address = address;

                    int arrayLength = 0;
                    arrayLength = mValueGroup[dc].Length;

                    int oAddress = address + addrNum;
                    if (oAddress > arrayLength)
                    {
                        throw new Exception($"当前地址{point.Name}{point.Input},（地址:{address} + 长度{addrNum}） > ArrrayL:{arrayLength}");
                    }

                    point.Panel = this;
                    //将数据点都加入原始数据的列表中
                    for (int i = 0; i < addrNum; i++)
                    {
                        //添加用于查找的基础数据
                        int add = address + i;
                        if (!mReadData_Base[dc].Contains(add))
                        {
                            mReadData_Base[dc].Add(add);
                        }

                        //添加用于回调的索引数据
                        if (!mEventMap[dc].ContainsKey(add))
                        {
                            mEventMap[dc].Add(add, new List<McDataPoint>());
                        }
                        mEventMap[dc][add].Add(dp);
                    }
                }
            }
        }

        /// <summary>
        /// 对寄存器列表进行排序
        /// </summary>
        public void SortOrderReadList()
        {
            // 清空 ReadList，重新生成
            foreach (var item in mReadList)
            {
                item.Value.Clear();
            }

            // 遍历每个区域
            foreach (var areaKvp in mReadData_Base)
            {
                E_McDeviceCode code = areaKvp.Key;
                List<int> baseList = areaKvp.Value;

                if (baseList == null || baseList.Count == 0)
                {
                    continue;
                }

                // 获取所有地址：去重 + 排序 
                var sortedAddresses = areaKvp.Value.Distinct().OrderBy(a => a).ToList();

                if (sortedAddresses.Count == 0) continue;

                var blocks = new List<AddressBlock>();

                // 初始化第一个块
                AddressBlock currentBlock = new AddressBlock
                {
                    StartAddress = sortedAddresses[0],
                    Length = 1
                };

                // 遍历后续地址进行聚类
                for (int i = 1; i < sortedAddresses.Count; i++)
                {
                    int currentAddr = sortedAddresses[i];
                    int prevAddr = sortedAddresses[i - 1];

                    // 判断是否连续：当前地址 == 前一个地址 + 1
                    bool isConsecutive = false;

                    // 判断是否超长：当前块长度是否已达上限
                    bool isFull = false;

                    switch (code)
                    {
                        case E_McDeviceCode.M:
                        case E_McDeviceCode.X:
                        case E_McDeviceCode.Y:
                        case E_McDeviceCode.B:
                            isConsecutive = (currentAddr <= prevAddr + HoleThreshold * 4);
                            isFull = currentBlock.Length >= MAX_BLOCK_SIZE_BOOL;
                            break;
                        case E_McDeviceCode.D:
                        case E_McDeviceCode.W:
                        case E_McDeviceCode.R:
                            isConsecutive = (currentAddr <= prevAddr + HoleThreshold);
                            isFull = currentBlock.Length >= MAX_BLOCK_SIZE;
                            break;
                        default:
                            break;
                    }

                    if (isConsecutive && !isFull)
                    {
                        // 连续且未满 -> 延长当前块
                        currentBlock.Length += currentAddr - prevAddr;
                    }
                    else
                    {
                        // 不连续 或 已满 -> 结束当前块，开启新块
                        blocks.Add(currentBlock);

                        currentBlock = new AddressBlock
                        {
                            StartAddress = currentAddr,
                            Length = 1
                        };
                    }
                }

                // 添加最后一个块
                blocks.Add(currentBlock);

                // 保存结果
                if (!mReadList.ContainsKey(code))
                {
                    mReadList.Add(code, new List<AddressBlock>());
                }
                mReadList[code] = blocks;

            }
        }

        /// <summary>
        ///  内存解码 获取数据
        /// </summary>
        /// <param name="mp"></param>
        /// <returns></returns>
        public DeviceValue DecodeValue(McDataPoint mp)
        {
            //if (!mChangedMge.IsChannelRunning)
            //{
            //    throw new Exception("自动读取没有开启，无法支持内存解码操作");
            //}
            switch (mp.DecodeData.Area)
            {
                case E_McDeviceCode.M:
                case E_McDeviceCode.X:
                case E_McDeviceCode.Y:
                case E_McDeviceCode.B:
                    return ((bool[])mValueGroup[mp.DecodeData.Area])[mp.DecodeData.Address];
                case E_McDeviceCode.D:
                case E_McDeviceCode.W:
                case E_McDeviceCode.R:
                    ReadOnlySpan<byte> bytes = ((byte[])mValueGroup[mp.DecodeData.Area]).AsSpan(mp.DecodeData.Address * 2, mp.GetLength() * 2);
                    return ValueDecoder.Decode(bytes, mp.DataType, 0);
                default:
                    break;
            }
            return new DeviceValue();
        }

        /// <summary>
        /// Bool数据变换回调
        /// </summary>
        /// <param name="code">类型</param>
        /// <param name="bl">数据缓冲区</param>
        /// <param name="offest1"></param>
        /// <param name="b2"></param>
        /// <param name="IsCompare"></param>
        protected void ValueCompare_Bool(E_McDeviceCode code, bool[] bl, int offest1, bool[] b2, bool IsCompare)
        {
            if (IsCompare)
            {
                ReadOnlySpan<bool> bv1 = new ReadOnlySpan<bool>(bl, offest1, b2.Length);
                ReadOnlySpan<bool> bv2 = new ReadOnlySpan<bool>(b2);
                List<CompareValue> differentIndex = BitOperationEx.Compare_Ptr(bv1.ToArray(), bv2.ToArray());

                foreach (var item in differentIndex)
                {
                    int addr = offest1 + item.Index;
                    TriggerCallback(code, addr);
                }
            }
            ///最新数据进行赋值
            Array.Copy(b2, 0, mValueGroup[code], offest1, b2.Length);
        }


        /// <summary>
        /// Byte类型数据变换对比 并更新
        /// </summary>
        /// <param name="code"></param>
        /// <param name="bl"></param>
        /// <param name="offest1"></param>
        /// <param name="b2"></param>
        /// <param name="offest2"></param>
        /// <param name="IsCompare"></param>
        protected void ValueCompare_Byte(E_McDeviceCode code, byte[] bl, int offest1, byte[] b2, int offest2, bool IsCompare)
        {
            int length = b2.Length - offest2;
            if (IsCompare)
            {
                ReadOnlySpan<byte> bv1 = new ReadOnlySpan<byte>(bl, offest1, length);
                ReadOnlySpan<byte> bv2 = new ReadOnlySpan<byte>(b2, offest2, length);
                List<CompareValue> differentIndex = BitOperationEx.Compare_Ptr(bv1.ToArray(), bv2.ToArray());
                foreach (var item in differentIndex)
                {
                    //绑定的最小单位是ushort，所以触发回调时要除2
                    int addr = offest1 / 2 + item.Index / 2;

                    TriggerCallback(code, addr);
                }
            }
            ///最新数据进行赋值
            Array.Copy(b2, offest2, mValueGroup[code], offest1, length);
        }

        /// <summary>
        /// 触发数据变换回调
        /// </summary>
        /// <param name="code"></param>
        /// <param name="address"></param>
        private void TriggerCallback(E_McDeviceCode code,int address)
        {
            int gen = Interlocked.Increment(ref mGeneration);

            if (!mEventMap[code].TryGetValue(address, out var points))
               return;

            foreach (var mEventMap in points)
            {
                if (mEventMap.LastGeneration != gen)
                {
                    DeviceValue val = this.DecodeValue(mEventMap);
                    mCommunicationBuffer.Enqueue(mEventMap.GetValChangeDel_Light(), val, E_CallBackWeight.Light);
                    mCommunicationBuffer.Enqueue(mEventMap.GetValChangeDel_Heavy(), val, E_CallBackWeight.Heavy);
                    mEventMap.LastGeneration = gen;
                }
            }
        }
    }
}
