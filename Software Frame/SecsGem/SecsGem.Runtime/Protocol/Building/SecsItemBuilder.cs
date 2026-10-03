// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsItemBuilder。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Runtime、SecsGem.Runtime.Protocol、Secs4Net、System、System.Collections.Generic。导入不代表所有类型都会在运行时加载。
using SecsGem.Runtime;
using SecsGem.Runtime.Protocol;
using Secs4Net;
using System;
using System.Collections.Generic;

namespace SecsGem.Runtime.Protocol.Building;

/// <summary>
/// SecsItemBuilder 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// </summary>
internal static class SecsItemBuilder
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Build。
    /// 正文中的关键协作调用包括 A、ToString、B、ToByte、U1；具体条件和先后次序见方法体。
    /// </summary>
    internal static Item Build(object pData, SecsFormat DataType)
        {

            Item item = null;
            switch (DataType)
            {
                case SecsFormat.ASCII:
                    if (pData == null) return Item.A(string.Empty);
                    item = Item.A(pData.ToString());
                    break;
                case SecsFormat.Binary:
                    if (pData == null) return Item.B(Memory<byte>.Empty);
                    item = Item.B(Convert.ToByte(pData));
                    break;
                case SecsFormat.U1:
                    if (pData == null) return Item.U1(Memory<byte>.Empty);
                    item = Item.U1(Convert.ToByte(pData));
                    break;
                case SecsFormat.U2:
                    if (pData == null) return Item.U2(Memory<UInt16>.Empty);
                    item = Item.U2(Convert.ToUInt16(pData));
                    break;
                case SecsFormat.U4:
                    if (pData == null) return Item.U4(Memory<UInt32>.Empty);
                    item = Item.U4(Convert.ToUInt32(pData));
                    break;
                case SecsFormat.U8:
                    if (pData == null) return Item.U8(Memory<UInt64>.Empty);
                    item = Item.U8(Convert.ToUInt64(pData));
                    break;
                case SecsFormat.I1:
                    if (pData == null) return Item.I1(Memory<SByte>.Empty);
                    item = Item.I1(Convert.ToSByte(pData));
                    break;
                case SecsFormat.I2:
                    if (pData == null) return Item.I2(Memory<Int16>.Empty);
                    item = Item.I2(Convert.ToInt16(pData));
                    break;
                case SecsFormat.I4:
                    if (pData == null) return Item.I4(Memory<Int32>.Empty);
                    item = Item.I4(Convert.ToInt32(pData));
                    break;
                case SecsFormat.I8:
                    if (pData == null) return Item.I8(Memory<Int64>.Empty);
                    item = Item.I8(Convert.ToInt64(pData));
                    break;
                case SecsFormat.Boolean:
                    if (pData == null) return Item.Boolean(Memory<bool>.Empty);
                    item = Item.Boolean(Convert.ToBoolean(pData));
                    break;
                case SecsFormat.List:
                    if (pData == null) return Item.L(new Item[0]);
                    else if (pData is List<string> list)
                    {
                        // 预分配容量以减少内存重新分配
                        List<Item> items = new List<Item>(list.Count);
                        foreach (string obj in list)
                        {
                            items.Add(Item.A(obj));
                        }
                        item = Item.L(items);
                    }
                    else if (pData is Dictionary<string, string> dic)
                    {
                        // 预分配容量以减少内存重新分配
                        List<Item> items = new List<Item>(dic.Count);
                        foreach (var obj in dic)
                        {

                            items.Add(Item.L(Item.A(obj.Key),Item.A(obj.Value)));
                        }
                        item = Item.L(items);
                    }
                    break;
            }
            return item;
        }
}
