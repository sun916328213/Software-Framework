using Secs4Net;
using System;
using System.Collections.Generic;

namespace SecsGem.Runtime.Protocol.Parsing;

internal static class SecsItemParser
{
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
