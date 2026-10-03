// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：PortData、IToolInputPort、InputPort、ToolInputPorts、IToolOutputPort、OutputPort、ToolOutputPorts、PortBindingData、TypeCompat、ToolBus。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core、System、System.Collections、System.Collections.Concurrent、System.Collections.Generic、System.Linq。导入不代表所有类型都会在运行时加载。
using Vision.Core;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Vision.Core.Tools
{
    /// <summary>
    /// PortData 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public abstract class PortData<T>
    {
        public string ToolId { get; }
        public string PortName { get; }
        public Type PortType { get; }
        /// <summary>
        /// 初始化 PortData 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public PortData(string toolId, string portName) { ToolId = toolId; PortName = portName; PortType = typeof(T); }
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// </summary>
        public override string ToString() => $"{ToolId}:{PortName}";

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 KeyStr。
        /// </summary>
        protected virtual string KeyStr() => $"{ToolId}:{PortName}";

    }

    /// <summary>
    /// IToolInputPort 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public interface IToolInputPort 
    {
        string PortName { get; }

        PortBindingData BindingData { get; set; }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 Get。
        /// </summary>
        object Get(ToolBus bus);
    }

    /// <summary>
    /// InputPort 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// 继承或实现的契约：PortData&lt;T&gt;、IToolInputPort。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public sealed class InputPort<T>: PortData<T>, IToolInputPort
    {
        /// <summary>
        /// 初始化 InputPort 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public InputPort(string toolId, string portName) :base(toolId, portName) { }

        public PortBindingData BindingData { get; set; }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 KeyStr。
        /// </summary>
        protected override string KeyStr() => $"{BindingData.ToolId}:{BindingData.PortName}";

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 Get。
        /// 正文中的关键协作调用包括 Get、KeyStr、IsNullOrEmpty、Split、TrimStart；具体条件和先后次序见方法体。
        /// </summary>
        public object Get(ToolBus bus)
        {
            object v = bus.Get(KeyStr());
            //if (v.GetType() != PortType)
            //{
            //    throw new Exception("获取数据类型与预定类型不相符！！！");
            //}

            if (!string.IsNullOrEmpty( BindingData.PropertyPath))
            {
                string[] parts = BindingData.PropertyPath.Trim().TrimStart('.').Split('.');
                object current = v;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public ;
                foreach (var name in parts)
                {
                    if (current is null) return null;

                    var t = current.GetType();
                    var prop = t.GetProperty(name, flags);
                    if (prop != null)
                    {
                        current = prop.GetValue(current);
                        continue;
                    }

                    var field = t.GetField(name, flags);
                    if (field != null)
                    {
                        current = field.GetValue(current);
                        continue;
                    }

                    // 没有同名成员
                    throw new MissingMemberException($"{t.FullName} 不存在成员 {name}");
                }
                if (current.GetType() != PortType)
                {
                    throw new Exception("获取数据类型与预定类型不相符！！！");
                }
                return current;
            }

            return (T)v;
        }
    }

    /// <summary>
    /// ToolInputPorts 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public class ToolInputPorts 
    {
        /// <summary>
        /// 初始化 ToolInputPorts 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ToolInputPorts(IEnumerable<IToolInputPort> ports)
        {
            inputports = new Dictionary<string, IToolInputPort>(StringComparer.Ordinal);
            foreach (var p in ports)
                inputports[p.PortName] = p;
        }

        private readonly Dictionary<string, IToolInputPort> inputports = new Dictionary<string, IToolInputPort>();

        public object this[string key] { get => inputports[key];}
    }

    /// <summary>
    /// IToolOutputPort 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public interface IToolOutputPort
    {
        string PortName { get; }

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 Set。
        /// </summary>
        void Set(ToolBus bus, object value);
    }

    /// <summary>
    /// OutputPort 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// 继承或实现的契约：PortData&lt;T&gt;、IToolOutputPort。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public sealed class OutputPort<T> : PortData<T>, IToolOutputPort
    {
        /// <summary>
        /// 初始化 OutputPort 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public OutputPort(string toolId, string portName) : base(toolId, portName) { }

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 Set。
        /// 正文中的关键协作调用包括 GetType、Put、KeyStr；具体条件和先后次序见方法体。
        /// </summary>
        public void Set(ToolBus bus, object value)
        {
            if (value.GetType() != PortType)
            {
                throw new Exception("输入数据类型与预定类型不相符！！！");
            }
            bus.Put(KeyStr(), value);
        }
    }

    /// <summary>
    /// ToolOutputPorts 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public class ToolOutputPorts
    {
        /// <summary>
        /// 初始化 ToolOutputPorts 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ToolOutputPorts(IEnumerable<IToolOutputPort> ports) 
        {
            outPorts = new Dictionary<string, IToolOutputPort>(StringComparer.Ordinal);
            foreach (var p in ports)
                outPorts[p.PortName] = p;  
        }

        private readonly Dictionary<string, IToolOutputPort> outPorts ;

        public IToolOutputPort this[string key] { get => outPorts[key]; }
    }

    /// <summary>
    /// PortBindingData 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public sealed class PortBindingData
    {
        /// <summary>
        /// 初始化 PortBindingData 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public PortBindingData(string toolId, string portName, string propertyPath = null) 
        {
            ToolId = toolId; PortName = portName;
            PropertyPath = propertyPath;
        }

        // 绑定来源：哪个工具的哪个输出端口
        public string ToolId { get; }
        public string PortName { get; }
        public string PropertyPath { get;  }            // 子属性路径，如 "Pose.R" 或 "X"
        public List<PortBindingData> Children { get; set; } = new List<PortBindingData>();
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// 正文中的关键协作调用包括 IsNullOrEmpty；具体条件和先后次序见方法体。
        /// </summary>
        public override string ToString()
            => $"{ToolId}:{PortName}" +  (string.IsNullOrEmpty(PropertyPath) ? "" : $".{PropertyPath}");
    }

    /// <summary>
    /// TypeCompat 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public static class TypeCompat
    {
        // 你可以把“兼容”定得更宽松：同型/可赋值/数值扩大/隐式 operator/自定义转换器
        public static bool IsCompatible(Type source, Type target)
        {
            if (target.IsAssignableFrom(source)) return true;
            return false;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 IsEnumerable。
        /// 正文中的关键协作调用包括 GetElementType、FirstOrDefault、Concat、GetInterfaces、GetGenericTypeDefinition；具体条件和先后次序见方法体。
        /// </summary>
        public static bool IsEnumerable(Type t, out Type elemType)
        {
            if (t.IsArray) { elemType = t.GetElementType(); return true; }
            var ienum = t.GetInterfaces().Concat(new[] { t })
                          .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            elemType = ienum?.GetGenericArguments()[0];
            return elemType != null;
        }
    }



    /// <summary>
    /// 工具流通的数据总线
    /// </summary>
    public class ToolBus
    {
        private readonly ConcurrentDictionary<string, object> mValueStroe = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, Type> mValueTypes = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);

        // 覆盖发布（无历史）
        public void Put(string key, object value)
        {
            Type t = value.GetType();
            // 类型锁定：首次设定后必须一致
            mValueTypes.AddOrUpdate(
                key,
                tp => t,
                (tp, old) => old == t ? 
                    old
                    : throw new InvalidOperationException($"Slot {key} type locked as {old.Name}, cannot publish {t.Name}.")
            );
            mValueStroe.AddOrUpdate(
                key,
                tp => value,
                (tp, old) => 
                {
                    if (mValueStroe[key] != null)//如果是引用类型需要释放掉
                    {
                        if (mValueStroe[key] is IDisposable dsp)
                        {
                            dsp.Dispose();
                            dsp = null;
                        }
                    }
                    return value;
                }
            );
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 Get。
        /// 正文中的关键协作调用包括 TryGetValue、GetType；具体条件和先后次序见方法体。
        /// </summary>
        public object Get(string key)
        {
            if (!mValueStroe.TryGetValue(key, out object v))
                throw new InvalidOperationException($"No data in slot: {key}");
            Type t = mValueTypes[key];
            if (v.GetType() == t) 
                return v;

            //类型不统一
            throw new InvalidOperationException($"Slot {key} type mismatch. Locked {v.GetType().Name}, requested {t.Name}.");
        }

    }
























    //public abstract class BindNode
    //{
    //    public string Display { get; set; } = "";
    //    public Type NodeType { get; set; } = typeof(object);
    //    public PortBindingData BindingData { get; set; } 
    //    public Func<IEnumerable<BindNode>> ChildrenGetter { get; set; }

    //    public virtual IEnumerable<BindNode> Children()
    //        => ChildrenGetter?.Invoke() ?? Enumerable.Empty<BindNode>();
    //}

    //public static class LiveExpand
    //{
    //    public static IEnumerable<BindNode> BuildMatchingTree(ToolBus bus, string toolId, string portName, Type targetType)
    //    {
    //        if (!bus.TryPeek(toolId, portName, out var value) || value == null)
    //            yield break;

    //        var spec = new PortBindingData(new PortData(toolId, portName));
    //        foreach (var node in ExpandRecursive(bus, spec, value, targetType))
    //            yield return node;
    //    }

    //    private static IEnumerable<BindNode> ExpandRecursive(ToolBus bus, PortBindingData spec, object cur, Type targetType)
    //    {
    //        var t = cur.GetType();

    //        // 1️⃣ 如果当前类型就是目标类型 → 这是叶节点
    //        if (t == targetType)
    //        {
    //            yield return new MatchNode(spec, t, cur);
    //            yield break;
    //        }

    //        // 2️⃣ 如果当前是基础类型（但不是目标类型） → 跳过
    //        if (TypeHelper.IsPrimitiveLike(t))
    //            yield break;

    //        // 3️⃣ 列表
    //        if (cur is IList il)
    //        {
    //            for (int i = 0; i < Math.Min(il.Count, 200); i++)
    //            {
    //                var item = il[i];
    //                if (item == null) continue;
    //                var itemSpec = spec with { Index = i };
    //                foreach (var node in ExpandRecursive(bus, itemSpec, item, targetType))
    //                    yield return node;
    //            }
    //            yield break;
    //        }

    //        // 4️⃣ 字典
    //        if (cur is IDictionary dict)
    //        {
    //            foreach (var key in dict.Keys)
    //            {
    //                var val = dict[key];
    //                if (val == null) continue;
    //                var itemSpec = spec with { Key = key.ToString() };
    //                foreach (var node in ExpandRecursive(bus, itemSpec, val, targetType))
    //                    yield return node;
    //            }
    //            yield break;
    //        }

    //        // 5️⃣ 普通对象 → 递归属性
    //        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    //        {
    //            if (p.GetIndexParameters().Length > 0) continue;
    //            object? val;
    //            try { val = p.GetValue(cur); } catch { continue; }
    //            if (val == null) continue;

    //            var subSpec = spec with
    //            {
    //                PropertyPath = string.IsNullOrEmpty(spec.PropertyPath)
    //                               ? p.Name
    //                               : $"{spec.PropertyPath}.{p.Name}"
    //            };

    //            foreach (var node in ExpandRecursive(bus, subSpec, val, targetType))
    //                yield return node;
    //        }
    //    }

    //    private sealed class MatchNode : BindNode
    //    {
    //        public MatchNode(BindingSpec spec, Type type, object? value)
    //        {
    //            Spec = spec;
    //            NodeType = type;
    //            Display = $"{spec.ToString()} = {Preview(value)}";
    //            ChildrenGetter = () => Enumerable.Empty<BindNode>();
    //        }
    //    }

    //    private static string Preview(object? v)
    //    {
    //        if (v == null) return "null";
    //        if (TypeHelper.IsPrimitiveLike(v.GetType())) return v.ToString() ?? "";
    //        return v.GetType().Name;
    //    }
    //}



}
