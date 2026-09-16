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
        private readonly object _syncRoot = new();

        public IReadOnlyList<T> Items
        {
            get
            {
                lock (_syncRoot)
                    return DataManagers.ToList().AsReadOnly();
            }
        }

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
            lock (_syncRoot)
            {
                var name = GetName(device);
                if (DataManagers.Any(x => GetName(x) == name))
                    throw new InvalidOperationException($"已存在同名实例: {name}");
                DataManagers.Add(device);
            }
        }

        /// <summary>
        /// 内部添加（跳过重名检查，供 Create 使用 Block 语义由子类保证，或兼容旧数据）
        /// </summary>
        protected void AddInternal(T device)
        {
            lock (_syncRoot)
                DataManagers.Add(device);
        }

        /// <summary>
        /// 在单一管理器锁内验证并替换指定名称集合，外部读取只能看到替换前或替换后的完整快照。
        /// </summary>
        /// <param name="ownedNames">允许本次替换移除的现有实例名称。</param>
        /// <param name="replacements">已经完成构造、尚未注册的新实例。</param>
        /// <returns>被替换移除的旧实例，由调用方在提交后释放。</returns>
        protected IReadOnlyList<T> ReplaceOwnedItems(
            IReadOnlyCollection<string> ownedNames,
            IReadOnlyList<T> replacements)
        {
            ArgumentNullException.ThrowIfNull(ownedNames);
            ArgumentNullException.ThrowIfNull(replacements);

            lock (_syncRoot)
            {
                var owned = new HashSet<string>(ownedNames, StringComparer.Ordinal);
                var current = DataManagers.ToList();
                var removed = current.Where(item => owned.Contains(GetName(item))).ToList();
                var retainedNames = new HashSet<string>(
                    current.Where(item => !owned.Contains(GetName(item))).Select(GetName),
                    StringComparer.Ordinal);
                var replacementNames = new HashSet<string>(StringComparer.Ordinal);

                foreach (var replacement in replacements)
                {
                    var name = GetName(replacement);
                    if (!replacementNames.Add(name))
                        throw new InvalidOperationException($"替换集合包含同名实例: {name}");
                    if (retainedNames.Contains(name))
                        throw new InvalidOperationException($"实例已由其他模块注册: {name}");
                }

                foreach (var item in removed)
                    DataManagers.Remove(item);
                foreach (var replacement in replacements)
                    DataManagers.Add(replacement);

                return removed.AsReadOnly();
            }
        }

        /// <summary>
        /// 移除
        /// </summary>
        /// <param name="device"></param>
        public bool Remove(T device)
        {
            lock (_syncRoot)
                return DataManagers.Remove(device);
        }

        /// <summary>
        /// 清除所有
        /// </summary>
        public void Clear()
        {
            lock (_syncRoot)
                DataManagers.Clear();
        }

        /// <summary>
        /// 获取设备
        /// </summary>
        /// <param name="deviceName">设备名称</param>
        /// <returns></returns>
        public T? this[string deviceName]
        {
            get
            {
                lock (_syncRoot)
                    return DataManagers.FirstOrDefault(x => GetName(x) == deviceName);
            }
        }


        /// <summary>
        /// 获取设备
        /// </summary>
        /// <param name="index">设备索引</param>
        /// <returns></returns>
        public T this[int index]
        {
            get
            {
                lock (_syncRoot)
                    return DataManagers[index];
            }
        }

        /// <summary>
        /// 设备数量
        /// </summary>
        public int Count
        {
            get
            {
                lock (_syncRoot)
                    return DataManagers.Count;
            }
        }

        public bool TryGet(string name, out T? item)
        {
            lock (_syncRoot)
            {
                item = DataManagers.FirstOrDefault(x => GetName(x) == name);
                return item != null;
            }
        }

        public bool Contains(string name)
        {
            lock (_syncRoot)
                return DataManagers.Any(x => GetName(x) == name);
        }
    }
}
