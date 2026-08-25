using Extension.DesignPattern.SingletonPattern;
using Extension.Tool;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataManager
{
    /// <summary>
    /// 设备管理类基类
    /// </summary>
    public abstract class ManagerBase<T, TParam>   
    {
        public readonly ConcurrentList<T> DataManagers = new ConcurrentList<T>();

        public abstract void Config (List<TParam> param);

        public abstract T Create (TParam param);

        /// <summary>
        /// 获取设备名称的抽象方法，子类需要实现此方法
        /// </summary>
        /// <param name="device">设备实例</param>
        /// <returns>设备名称</returns>
        protected abstract string GetName(T device);

        /// <summary>
        /// 添加
        /// </summary>
        /// <param name="device"></param>
        public void Add(T device)
        {
            DataManagers.Add(device);
        }

        /// <summary>
        /// 移除
        /// </summary>
        /// <param name="device"></param>
        public void Remove(T device)
        {
            DataManagers.Remove(device);
        }

        /// <summary>
        /// 清除所有
        /// </summary>
        public void Clear()
        {
            DataManagers.Clear();
        }

        /// <summary>
        /// 获取设备
        /// </summary>
        /// <param name="deviceName">设备名称</param>
        /// <returns></returns>
        public T this[string deviceName] => DataManagers.FirstOrDefault(x => GetName(x) == deviceName);


        /// <summary>
        /// 获取设备
        /// </summary>
        /// <param name="index">设备索引</param>
        /// <returns></returns>
        public T this[int index] => DataManagers[index];

        /// <summary>
        /// 设备数量
        /// </summary>
        public int Count => DataManagers.Count;
    }
}