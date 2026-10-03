// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsItemParser。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Runtime、SecsGem.Runtime.Protocol、Secs4Net、System、System.Collections.Generic。导入不代表所有类型都会在运行时加载。
using SecsGem.Runtime;
using SecsGem.Runtime.Protocol;
using Secs4Net;
using System;
using System.Collections.Generic;

namespace SecsGem.Runtime.Protocol.Parsing;

/// <summary>
/// SecsItemParser 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// </summary>
internal static class SecsItemParser
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Flatten。
    /// 正文中的关键协作调用包括 L、ReadFirstValue、GetString、Flatten、AddRange；具体条件和先后次序见方法体。
    /// </summary>
    internal static List<string> Flatten(Item Data)
        {
            Item[] Items = null;
            if (Data == null) return null;
            
            // 使用Capacity预分配以减少内存重新分配
            List<string> strings = new List<string>();
            if (Data.Format != SecsFormat.List)
            {
                Items = Item.L(Data).Items;
            }
            else
                Items = Data.Items;

            foreach (Item item in Items)
            {
                //格式  
                var format = item.Format;

                string data = null;
                switch (format)
                {
                    case SecsFormat.U1:
                        data = ReadFirstValue<byte>(item);
                        break;
                    case SecsFormat.U2:
                        data = ReadFirstValue<UInt16>(item);
                        break;
                    case SecsFormat.U4:
                        data = ReadFirstValue<UInt32>(item);
                        break;
                    case SecsFormat.U8:
                        data = ReadFirstValue<UInt64>(item);
                        break;
                    case SecsFormat.Binary:
                        data = ReadFirstValue<byte>(item);
                        break;
                    case SecsFormat.ASCII:
                        data = item.GetString();
                        break;
                    case SecsFormat.List:
                        var list = Flatten(item);
                        if (list != null)
                            strings.AddRange(list);
                        else
                            return list;
                        break;
                    case SecsFormat.Boolean:
                        data = ReadFirstValue<bool>(item);
                        break;
                }
                if (data != null) strings.Add(data);
            }
            return strings;
        }

    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 ReadFirstValue。
    /// 正文中的关键协作调用包括 GetMemory、ToString；具体条件和先后次序见方法体。
    /// </summary>
    internal static string ReadFirstValue<T>(Item pItem) where T : unmanaged, IEquatable<T>
        {
            var u1 = pItem.GetMemory<T>();
            if (!u1.IsEmpty)
            {
                // 使用Span直接访问数据，避免不必要的内存分配
                return u1.Span[0].ToString();
            }
            else return null;
        }
}
