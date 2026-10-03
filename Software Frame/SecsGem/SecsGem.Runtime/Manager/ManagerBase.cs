// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：ManagerBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Runtime、DataManager.Core.Log、DataManager.Runtime.Log、Prism.Ioc、System。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Runtime;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using Prism.Ioc;
using System;

namespace SecsGem.Runtime.Manager
{
    /// <summary>
    /// 管理器基类 - 提供统一的管理器结构和异常处理机制
    /// 根据项目规范实现IDisposable模式和线程安全机制
    /// </summary>
    public abstract class ManagerBase : IDisposable
    {
        protected IContainerProvider ContainerProvider { get; set; }
        
        internal SecsGemRuntime SecsGemRuntime => ContainerProvider?.Resolve<SecsGemRuntime>();
        internal VIDManager VIDManager => ContainerProvider?.Resolve<VIDManager>();
        internal DeviceManager DeviceManager => ContainerProvider?.Resolve<DeviceManager>();
        internal StatusManager StatusManager => ContainerProvider?.Resolve<StatusManager>();
 
        /// <summary>
        /// 初始化 ManagerBase 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ManagerBase(IContainerProvider container)
        {
            ContainerProvider = container;
        }

        #region IDisposable Support

        private bool _disposed;

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 Dispose、SuppressFinalize；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的具体实现，子类重写时须调用 base.Dispose(disposing)
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }

        /// <summary>
        /// 确保对象未被释放
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        #endregion
    }
}