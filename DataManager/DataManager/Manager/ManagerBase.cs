using Extension.DesignPattern.SingletonPattern;
using Extension.Tool;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataManager
{
    /// <summary>
    /// 设备管理类基类（P1-1：实现 IManager，新增 Items/TryGet/重名检查；
    /// P4-2：DataManagers 收为 private，外部经 Items/索引器/TryGet 访问）。
    /// </summary>
    public abstract class ManagerBase<T, TParam> : IManager<T, TParam>
    {
        private readonly ConcurrentList<T> DataManagers = new ConcurrentList<T>();

        public IReadOnlyList<T> Items => DataManagers.ToList().AsReadOnly();

        public abstract void Config (List<TParam> param);

        public abstract T Create (TParam param);

        /// <summary>
        /// 获取设备名称的抽象方法，子类需要实现此方法
        /// </summary>
        /// <param name="device">设备实例</param>
        /// <returns>设备名称</returns>
        protected abstract string GetName(T device);

        /// <summary>
        /// 添加（重名抛 InvalidOperationException，避免静默覆盖）
        /// </summary>
        /// <param name="device"></param>
        public void Add(T device)
        {
            var name = GetName(device);
            if (DataManagers.Any(x => GetName(x) == name))
                throw new InvalidOperationException($"已存在同名实例: {name}");
            DataManagers.Add(device);
        }

        /// <summary>
        /// 内部添加（跳过重名检查，供 Create 使用 Block 语义由子类保证，或兼容旧数据）
        /// </summary>
        protected void AddInternal(T device)
        {
            DataManagers.Add(device);
        }

        /// <summary>
        /// 移除
        /// </summary>
        /// <param name="device"></param>
        public bool Remove(T device)
        {
            return DataManagers.Remove(device);
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
        public T? this[string deviceName] => TryGet(deviceName, out var item) ? item : default;


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

        public bool TryGet(string name, out T? item)
        {
            item = DataManagers.FirstOrDefault(x => GetName(x) == name);
            return item != null;
        }

        public bool Contains(string name) => DataManagers.Any(x => GetName(x) == name);
    }
}