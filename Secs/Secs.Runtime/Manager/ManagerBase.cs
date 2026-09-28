using DataManager.Log;
using Prism.Ioc;
using System;

namespace Secs.Manager
{
    /// <summary>
    /// 管理器基类 - 提供统一的管理器结构和异常处理机制
    /// 根据项目规范实现IDisposable模式和线程安全机制
    /// </summary>
    public abstract class ManagerBase : IDisposable
    {
        protected IContainerProvider ContainerProvider { get; set; }
        
        internal SecsControl SecsControl => ContainerProvider?.Resolve<SecsControl>();
        internal VIDManager VIDManager => ContainerProvider?.Resolve<VIDManager>();
        internal DeviceManager DeviceManager => ContainerProvider?.Resolve<DeviceManager>();
        internal StatusManager StatusManager => ContainerProvider?.Resolve<StatusManager>();
 
        public ManagerBase(IContainerProvider container)
        {
            ContainerProvider = container;
        }

        #region IDisposable Support

        private bool _disposed;

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