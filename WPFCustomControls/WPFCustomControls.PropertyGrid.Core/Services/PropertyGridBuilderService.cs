using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WPFCustomControls.PropertyGrid.Models;
using WPFCustomControls.PropertyGrid.ViewModels;

namespace WPFCustomControls.PropertyGrid.Services;

/// <summary>
/// <see cref="IPropertyGridBuilderService"/> 默认实现。
/// 通过反射 + [NodeProperty] 特性，把目标对象的属性转换为分组 ViewModel 结构化数据。
///
/// <para>
/// <b>Core 子集（零 WPF 依赖）</b>：本类及其使用的 Models / ViewModels 都不引用 <c>System.Windows.*</c>。
/// 详见 <c>ARCHITECTURE.md</c> 优化方向 4.4。
/// </para>
/// </summary>
public class PropertyGridBuilderService : IPropertyGridBuilderService
{
    /// <summary>按类型缓存反射扫描结果，避免切换选中节点时反复反射。</summary>
    private static readonly ConcurrentDictionary<Type, CachedTypeInfo> Cache = new();

    /// <summary>可绑定属性名缓存。</summary>
    private static readonly ConcurrentDictionary<Type, List<string>> BindableCache = new();

    private sealed class CachedTypeInfo
    {
        public List<PropertyDescriptor> DisplayProps { get; } = new();
    }

    private readonly record struct PropertyDescriptor(PropertyInfo Info, NodePropertyAttribute Attr);

    /// <inheritdoc />
    public IReadOnlyList<PropertyGroupViewModel> BuildGroups(object? target, IPortConfigurable? portConfig)
    {
        var result = new List<PropertyGroupViewModel>();
        if (target == null) return result;

        var cached = Cache.GetOrAdd(target.GetType(), static t => BuildCache(t));

        var propGroups = cached.DisplayProps
            .GroupBy(x => string.IsNullOrEmpty(x.Attr.Group) ? "通用" : x.Attr.Group)
            .OrderBy(g => GroupSortKey(g.Key), System.StringComparer.Ordinal)
            .ToList();

        foreach (var group in propGroups)
        {
            var vm = new PropertyGroupViewModel(group.Key);
            foreach (var item in group
                         .OrderBy(x => x.Attr.Order)
                         .ThenBy(x => x.Info.Name, System.StringComparer.Ordinal))
            {
                vm.AddItem(new PropertyItemViewModel(target, item.Info, item.Attr, portConfig));
            }
            result.Add(vm);
        }
        return result;
    }

    /// <inheritdoc />
    public List<string> CollectBindableProperties(Type type)
    {
        var cached = BindableCache.GetOrAdd(type, static t =>
        {
            var list = new List<string>();
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // 排除流/端口相关属性，只保留有 public setter 的可写属性
                if (p.Name is "Id" or "InputPorts" or "OutputPorts" or "CustomInputPorts"
                    or "CustomOutputPorts" or "ActionButtons" or "IsEditable" or "IsDraggable") continue;
                if (p.SetMethod == null || !p.SetMethod.IsPublic) continue;
                list.Add(p.Name);
            }
            return list;
        });
        return new List<string>(cached);
    }

    private static CachedTypeInfo BuildCache(Type type)
    {
        var cache = new CachedTypeInfo();

        // 遍历类型层级（DeclaredOnly），收集所有带 [NodeProperty] 的公共属性
        var allProps = new List<PropertyInfo>();
        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            allProps.AddRange(t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        }

        // 同名字段按"最派生优先"去重：子类 override/hide 会覆盖基类声明，
        // 保证子类可用 [NodeProperty(IsDisplay=false)] 隐藏基类属性。
        //
        // 先用属性名占 seen 槽（不区分 IsDisplay），再判断是否纳入 DisplayProps。
        // 这样无论是 IsDisplay=true 还是 false，派生类声明都会屏蔽同名基类属性，
        // 避免基类属性"穿透"隐藏。
        var seen = new HashSet<string>();
        foreach (var prop in allProps)
        {
            if (!seen.Add(prop.Name)) continue;
            var attr = prop.GetCustomAttribute<NodePropertyAttribute>();
            if (attr == null || !attr.IsDisplay) continue;
            cache.DisplayProps.Add(new PropertyDescriptor(prop, attr));
        }
        return cache;
    }

    /// <summary>
    /// 分组排序键：按分组名开头的数字前缀做数值排序（修复 "10.x" 排在 "2.x" 前的字母序问题），
    /// 无数字前缀的分组（如"通用"）统一排到有数字前缀分组之后。
    /// </summary>
    private static string GroupSortKey(string groupName)
    {
        var i = 0;
        while (i < groupName.Length && char.IsDigit(groupName[i])) i++;
        if (i == 0) return "9999999999" + groupName;
        return groupName.Substring(0, i).PadLeft(10, '0') + groupName.Substring(i);
    }
}