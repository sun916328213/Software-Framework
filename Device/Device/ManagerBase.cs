using Extension.Struct;
using Extension.Tool;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Device
{
    /// <summary>
    /// 设备管理类基类
    /// </summary>
    /// <typeparam name="T">设备类型，必须实现IDevice接口</typeparam>
    /// <typeparam name="TParam">设备参数类型</typeparam>
    public abstract class ManagerBase<T, TParam> : IDisposable where T : IDevice
    {
        public readonly ConcurrentList<T> Devices = new ConcurrentList<T>();

        public abstract void Config(List<TParam> param);

        public abstract T Create(TParam param);

        public virtual void Connect()
        {
            foreach (var device in Devices)
            {
                device.Connect();
            }
        }

        public virtual async Task ConnectAsync()
        {
            await Task.Run(() => { Connect(); });
        }


        public virtual void Disconnect()
        {
            foreach (var device in Devices)
            {
                device.DisConnect();
            }
        }

        /// <summary>
        /// 添加设备
        /// </summary>
        /// <param name="device">设备实例</param>
        public void Add(T device)
        {
            Devices.Add(device);
        }

        /// <summary>
        /// 移除设备
        /// </summary>
        /// <param name="device">设备实例</param>
        public void Remove(T device)
        {
            Devices.Remove(device);
        }

        /// <summary>
        /// 清除所有设备
        /// </summary>
        public void Clear()
        {
            Devices.Clear();
        }

        public void Dispose()
        {
            Disconnect();
            Devices.Dispose();
        }

        /// <summary>
        /// 根据设备名称获取设备
        /// </summary>
        /// <param name="deviceName">设备名称</param>
        /// <returns>设备实例</returns>
        public T this[string deviceName] => Devices.FirstOrDefault(x => x.Name == deviceName);

        /// <summary>
        /// 根据索引获取设备
        /// </summary>
        /// <param name="index">设备索引</param>
        /// <returns>设备实例</returns>
        public T this[int index] => Devices[index];

        /// <summary>
        /// 获取设备列表
        /// </summary>
        /// <returns></returns>
        public List<T> DeviceCollection()=> Devices.ToList();

        /// <summary>
        /// 获取设备名称列表
        /// </summary>
        /// <returns></returns>
        public List<string> DeviceNameCollection() => Devices.Select(x=>x.Name).ToList();

        /// <summary>
        /// 设备数量
        /// </summary>
        public int Count => Devices.Count;
    }
}