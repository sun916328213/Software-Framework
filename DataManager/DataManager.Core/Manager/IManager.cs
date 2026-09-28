using System.Collections.Generic;

namespace DataManager
{
    /// <summary>
    /// 管理器抽象（P1-1）：统一 Config/Create/Add/Remove/Clear/TryGet 契约。
    /// </summary>
    public interface IManager<T, TParam>
    {
        IReadOnlyList<T> Items { get; }
        int Count { get; }
        void Config(List<TParam> param);
        T Create(TParam param);
        void Add(T item);
        bool Remove(T item);
        void Clear();
        bool TryGet(string name, out T? item);
    }
}
