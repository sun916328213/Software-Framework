using CommunicationNew.Core.Interfaces;

namespace CommunicationNew.Core.Models
{
    /// <summary>
    /// 数据点集合 —— 管理一组数据点，支持按名称/索引器访问
    /// </summary>
    public class DataPointCollection : IEnumerable<IDataPoint>
    {
        private readonly Dictionary<string, IDataPoint> _points = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<IDataPoint> _ordered = new();

        /// <summary>数据点数量</summary>
        public int Count => _ordered.Count;

        /// <summary>按名称索引器</summary>
        public IDataPoint this[string name]
        {
            get => _points[name] ?? throw new KeyNotFoundException($"数据点 '{name}' 不存在");
        }

        /// <summary>按索引器</summary>
        public IDataPoint this[int index] => _ordered[index];

        /// <summary>所有数据点</summary>
        // 返回只读视图，防止调用方通过强转绕过 Add/Remove 并破坏字典与顺序表的一致性。
        public IEnumerable<IDataPoint> All => _ordered.AsReadOnly();

        /// <summary>添加数据点</summary>
        public void Add(IDataPoint point)
        {
            if (_points.ContainsKey(point.Name))
                throw new ArgumentException($"数据点 '{point.Name}' 已存在");
            _points[point.Name] = point;
            _ordered.Add(point);
        }

        /// <summary>批量添加数据点</summary>
        public void AddRange(IEnumerable<IDataPoint> points)
        {
            ArgumentNullException.ThrowIfNull(points);
            var pending = points.ToArray();
            var names = new HashSet<string>(_points.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var point in pending)
            {
                ArgumentNullException.ThrowIfNull(point);
                if (!names.Add(point.Name))
                {
                    throw new ArgumentException($"数据点 '{point.Name}' 已存在", nameof(points));
                }
            }

            // 整批数据通过校验后才提交，避免枚举或重名失败留下部分新增结果。
            foreach (var point in pending) Add(point);
        }

        /// <summary>按定义创建并添加数据点</summary>
        public IDataPoint AddDefinition(DataPointDefinition def)
        {
            ArgumentNullException.ThrowIfNull(def);

            // 定义自身是唯一实例化入口，集合只负责所有权登记，避免复制字段映射规则。
            var point = def.ToDataPoint();
            Add(point);
            return point;
        }

        /// <summary>按定义列表批量创建</summary>
        public void AddDefinitions(IEnumerable<DataPointDefinition> defs)
        {
            ArgumentNullException.ThrowIfNull(defs);

            // 定义转换也在 AddRange 提交前完整执行，任何无效定义都不会造成部分写入。
            AddRange(defs.Select(static definition =>
            {
                ArgumentNullException.ThrowIfNull(definition);
                return definition.ToDataPoint();
            }));
        }

        /// <summary>移除数据点</summary>
        public bool Remove(string name)
        {
            if (_points.Remove(name, out var point))
            {
                _ordered.Remove(point);
                return true;
            }
            return false;
        }

        /// <summary>清空</summary>
        public void Clear()
        {
            _points.Clear();
            _ordered.Clear();
        }

        /// <summary>是否存在</summary>
        public bool Contains(string name) => _points.ContainsKey(name);

        public IEnumerator<IDataPoint> GetEnumerator() => _ordered.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
