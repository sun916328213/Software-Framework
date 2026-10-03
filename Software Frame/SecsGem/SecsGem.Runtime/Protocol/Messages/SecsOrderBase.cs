// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsOrderBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、Prism.Ioc、SecsGem.Runtime.Protocol.Building、SecsGem.Runtime.Protocol.Parsing、SecsGem.Runtime.Manager。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using Prism.Ioc;
using SecsGem.Runtime.Protocol.Building;
using SecsGem.Runtime.Protocol.Parsing;
using SecsGem.Runtime.Manager;
using Secs4Net;
using SecsGem.Runtime;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SecsGem.Runtime.Order
{
    /// <summary>
    /// SecsOrderBase 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// </summary>
    public class SecsOrderBase
    {
        IContainerProvider ContainerProvider;

        internal SecsGemRuntime SecsGemRuntime => ContainerProvider?.Resolve<SecsGemRuntime>();
        internal VIDManager VIDManager => ContainerProvider?.Resolve<VIDManager>();
        internal DeviceManager DeviceManager => ContainerProvider?.Resolve<DeviceManager>();
        internal StatusManager StatusManager => ContainerProvider?.Resolve<StatusManager>();

        /// <summary>
        /// 初始化 SecsOrderBase 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
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
