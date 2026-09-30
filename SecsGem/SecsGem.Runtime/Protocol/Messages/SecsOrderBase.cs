using Prism.Ioc;
using SecsGem.Runtime.Protocol.Building;
using SecsGem.Runtime.Protocol.Parsing;
using Secs.Manager;
using Secs4Net;
using SecsGem.Runtime;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Secs.Order
{
    public class SecsOrderBase
    {
        IContainerProvider ContainerProvider;

        internal SecsGemRuntime SecsControl => ContainerProvider?.Resolve<SecsGemRuntime>();
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
            return SecsItemBuilder.Build(pData, DataType);
        }

        /// <summary>  
        /// 解析数据  
        /// </summary>  
        /// <param name="Data"></param>  
        public virtual List<string> SecsItemToList(Item Data)
        {
            return SecsItemParser.Flatten(Data);
        }

        /// <summary>  
        /// 获取数据 uint  
        /// </summary>  
        /// <typeparam name="T">数据类型</typeparam>  
        /// <param name="pItem">数据 格式:Item</param>  
        /// <returns></returns>  
        public virtual string GetValue<T>(Item pItem) where T : unmanaged, IEquatable<T>
        {
            return SecsItemParser.ReadFirstValue<T>(pItem);
        }


    }

}
