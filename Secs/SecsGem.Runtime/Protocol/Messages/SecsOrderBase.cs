using Prism.Ioc;
using Secs.Manager;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Secs.Order
{
    public class SecsOrderBase
    {
        IContainerProvider ContainerProvider;

        internal SecsControl SecsControl => ContainerProvider?.Resolve<SecsControl>();
        internal VIDManager VIDManager => ContainerProvider?.Resolve<VIDManager>();
        internal DeviceManager DeviceManager => ContainerProvider?.Resolve<DeviceManager>();
        internal StatusManager StatusManager => ContainerProvider?.Resolve<StatusManager>();

        public SecsOrderBase() { }
        
        // 通过属性注入设置容器提供者
        public IContainerProvider ContainerProviderSetter
        {
            set { ContainerProvider = value; }
        }

        /// <summary>
        /// 接收数据 Host->Device 
        /// </summary>
        /// <param name="pSecsMessage"></param>
        /// <returns></returns>
        public virtual  Item  GetReplyItem(SecsMessage pSecsMessage)
        {
            // 使用Item.B(0)的静态实例减少内存分配
            return Item.B(0);
        }

        /// <summary>
        /// 异步回包入口，默认包装同步 GetReplyItem。
        /// 需要异步处理（如回调业务方）的指令重写此方法。
        /// 返回 null 时由底层自动触发 S9F7 兜底。
        /// </summary>
        public virtual Task<Item> GetReplyItemAsync(SecsMessage pSecsMessage)
        {
            return Task.FromResult(GetReplyItem(pSecsMessage));
        }
 
        //发送------------------------------------------------------------------------------------------------
 
        /// <summary>
        /// 发送数据 Device->Host
        /// </summary>
        /// <returns></returns>
        public virtual Item GetSendItem(params object[] pData)
        {
            // 使用Item.B(0)的静态实例减少内存分配
            return Item.B(0);
        }

        /// <summary>
        /// 添加数据
        /// </summary>
        /// <param name="pData"></param>
        /// <param name="DataType"></param>
        /// <returns></returns>
        public virtual Item AddData(object pData, SecsFormat DataType)
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

        /// <summary>  
        /// 解析数据  
        /// </summary>  
        /// <param name="Data"></param>  
        public virtual List<string> SecsItemToList(Item Data)
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
                        data = GetValue<byte>(item);
                        break;
                    case SecsFormat.U2:
                        data = GetValue<UInt16>(item);
                        break;
                    case SecsFormat.U4:
                        data = GetValue<UInt32>(item);
                        break;
                    case SecsFormat.U8:
                        data = GetValue<UInt64>(item);
                        break;
                    case SecsFormat.Binary:
                        data = GetValue<byte>(item);
                        break;
                    case SecsFormat.ASCII:
                        data = item.GetString();
                        break;
                    case SecsFormat.List:
                        var list = SecsItemToList(item);
                        if (list != null)
                            strings.AddRange(list);
                        else
                            return list;
                        break;
                    case SecsFormat.Boolean:
                        data = GetValue<bool>(item);
                        break;
                }
                if (data != null) strings.Add(data);
            }
            return strings;
        }

        /// <summary>  
        /// 获取数据 uint  
        /// </summary>  
        /// <typeparam name="T">数据类型</typeparam>  
        /// <param name="pItem">数据 格式:Item</param>  
        /// <returns></returns>  
        public virtual string GetValue<T>(Item pItem) where T : unmanaged, IEquatable<T>
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

}