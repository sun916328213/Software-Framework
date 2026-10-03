// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：PropertyGridBuilderService、CachedTypeInfo、PropertyDescriptor。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.Concurrent、System.Collections.Generic、System.Linq、System.Reflection、WPFCustomControls.PropertyGrid.Core.Composition、WPFCustomControls.PropertyGrid.Core.Models。导入不代表所有类型都会在运行时加载。
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WPFCustomControls.PropertyGrid.Core.Composition;
using WPFCustomControls.PropertyGrid.Core.Models;
using WPFCustomControls.PropertyGrid.Core.ViewModels;

namespace WPFCustomControls.PropertyGrid.Core.Services;

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

    private readonly IUiDispatcher _dispatcher;

    /// <summary>
    /// 初始化 PropertyGridBuilderService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public PropertyGridBuilderService()
        : this(NullUiDispatcher.Instance)
    {
    }

    /// <summary>
    /// 初始化 PropertyGridBuilderService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 ThrowIfNull；具体条件和先后次序见方法体。
    /// </summary>
    public PropertyGridBuilderService(IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// CachedTypeInfo 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
    /// </summary>
    private sealed class CachedTypeInfo
    {
        public List<PropertyDescriptor> DisplayProps { get; } = new();
    }

    /// <summary>
    /// PropertyDescriptor 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
    /// </summary>
    private readonly record struct PropertyDescriptor(PropertyInfo Info, NodePropertyAttribute Attr);

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 BuildGroups。
    /// 正文中的关键协作调用包括 GetOrAdd、GetType、BuildCache、ToList、OrderBy；具体条件和先后次序见方法体。
    /// </remarks>
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
                vm.AddItem(new PropertyItemViewModel(target, item.Info, item.Attr, portConfig, _dispatcher));
            }
            result.Add(vm);
        }
        return result;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 CollectBindableProperties。
    /// 正文中的关键协作调用包括 GetOrAdd、GetProperties、Add；具体条件和先后次序见方法体。
    /// </remarks>
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 BuildCache。
    /// 正文中的关键协作调用包括 AddRange、GetProperties、Add、GetCustomAttribute；具体条件和先后次序见方法体。
    /// </summary>
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
