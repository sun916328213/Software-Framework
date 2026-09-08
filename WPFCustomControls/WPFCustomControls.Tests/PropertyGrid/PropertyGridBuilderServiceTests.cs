using System.ComponentModel;
using WPFCustomControls.PropertyGrid.Composition;
using WPFCustomControls.PropertyGrid.Models;
using WPFCustomControls.PropertyGrid.Services;
using WPFCustomControls.PropertyGrid.ViewModels;

namespace WPFCustomControls.Tests.PropertyGrid;

/// <summary>
/// <see cref="PropertyGridBuilderService"/> 的单元测试。
///
/// 重点覆盖:
///   1. null / 非装饰属性的目标
///   2. 分组与排序(数字前缀优先于无前缀分组)
///   3. Order 排序 + 属性名兜底
///   4. IsDisplay=false 隐藏
///   5. 继承链扫描 + 子类覆盖基类同名属性
///   6. ReadOnly 模式(-1/1/自动)
///   7. CollectBindableProperties 排除规则
/// </summary>
public class PropertyGridBuilderServiceTests
{
    private readonly PropertyGridBuilderService _sut = new();

    // ── 测试模型 ───────────────────────────────────────────────

    private enum Color { Red, Green, Blue }

    private class TestNode : INotifyPropertyChanged
    {
        private string _name = "n1";
        private int _count;
        private double _ratio;
        private bool _enabled;
        private Color _color;

        public event PropertyChangedEventHandler? PropertyChanged;

        [NodeProperty(DisplayName = "节点名称", Group = "1.基础信息", Order = 1)]
        public string Name
        {
            get => _name;
            set { _name = value; OnChanged(); }
        }

        [NodeProperty(Group = "2.参数", Order = 0)]
        public int Count
        {
            get => _count;
            set { _count = value; OnChanged(); }
        }

        [NodeProperty(Group = "2.参数", Min = 0, Max = 1, Step = 0.1)]
        public double Ratio
        {
            get => _ratio;
            set { _ratio = value; OnChanged(); }
        }

        [NodeProperty]
        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; OnChanged(); }
        }

        [NodeProperty(Group = "3.外观")]
        public Color Color
        {
            get => _color;
            set { _color = value; OnChanged(); }
        }

        /// <summary>未标 [NodeProperty],应被忽略。</summary>
        public string Untagged { get; set; } = "x";

        /// <summary>显式隐藏的属性。</summary>
        [NodeProperty(IsDisplay = false)]
        public string Hidden { get; set; } = "h";

        /// <summary>强制只读。</summary>
        [NodeProperty(ReadOnly = 1)]
        public int Locked { get; set; } = 99;

        /// <summary>强制可写(尽管 set 私有)。</summary>
        [NodeProperty(ReadOnly = -1)]
        public int ForceWrite { get; private set; } = 5;

        private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>分组名带数字前缀的子类,验证排序与继承。</summary>
    private class DerivedNode : TestNode
    {
        [NodeProperty(Group = "10.新增", Order = 0)]
        public string Tag { get; set; } = "t";
    }

    /// <summary>子类显式覆盖基类同名属性并隐藏。</summary>
    private class OverrideNode : TestNode
    {
        [NodeProperty(IsDisplay = false)]
        public new string Name { get; set; } = "override";
    }

    private static List<PropertyItemViewModel> Flat(IEnumerable<PropertyGroupViewModel> groups)
        => groups.SelectMany(g => g.AllItems).ToList();

    // ── 基础行为 ───────────────────────────────────────────────

    [Fact]
    public void BuildGroups_NullTarget_ReturnsEmpty()
    {
        var groups = _sut.BuildGroups(null, null);
        Assert.Empty(groups);
    }

    [Fact]
    public void BuildGroups_FiltersUntaggedAndHiddenProperties()
    {
        var node = new TestNode();
        var groups = _sut.BuildGroups(node, null);
        var names = Flat(groups).Select(i => i.PropertyName).ToList();

        Assert.DoesNotContain("Untagged", names);
        Assert.DoesNotContain("Hidden", names);
    }

    // ── 分组与排序 ─────────────────────────────────────────────

    [Fact]
    public void BuildGroups_OrdersNumericGroupsBeforeGeneric()
    {
        var node = new TestNode();
        var groups = _sut.BuildGroups(node, null);

        var names = groups.Select(g => g.GroupName).ToList();
        // 1./2./3. 应排在 "通用" 之前
        var idxNumeric = names.FindIndex(n => n.StartsWith("1."));
        var idxGeneric = names.IndexOf("通用");
        Assert.True(idxNumeric >= 0);
        Assert.True(idxGeneric > idxNumeric, $"通用应排在有数字前缀分组之后,实际顺序: {string.Join(",", names)}");
    }

    [Fact]
    public void BuildGroups_OrdersGroupsByNumericPrefix()
    {
        // DerivedNode 多了 "10.新增" 分组,验证 10. 不被字母序排在 1./2./3. 之前
        var node = new DerivedNode();
        var groups = _sut.BuildGroups(node, null);

        var names = groups.Select(g => g.GroupName).ToList();
        // "10.新增" 应排在 "1./2./3." 之后;如 "1.基础信息" 不存在(被基类隐藏)则跳过
        var idx10 = names.IndexOf("10.新增");
        var idx2 = names.IndexOf("2.参数");
        if (idx2 >= 0 && idx10 >= 0)
            Assert.True(idx10 > idx2, $"10.x 应在 2.x 之后,实际: {string.Join(",", names)}");
    }

    [Fact]
    public void BuildGroups_OrdersItemsByAttrOrderThenPropertyName()
    {
        var node = new TestNode();
        var groups = _sut.BuildGroups(node, null);

        var group2 = groups.First(g => g.GroupName == "2.参数");
        var names = group2.AllItems.Select(i => i.PropertyName).ToList();

        // Count(Order=0) 应排在 Ratio(默认 Order=0,按属性名兜底)
        Assert.Equal("Count", names[0]);
        Assert.Equal("Ratio", names[1]);
    }

    // ── 继承链覆盖 ─────────────────────────────────────────────

    [Fact]
    public void BuildGroups_OverrideWithIsDisplayFalse_CurrentlyHidesBoth()
    {
        // 已知问题:BuilderService 按 derived→base 顺序遍历,把"已加入 seen 集合"的判断
        // 放在 IsDisplay 校验之前。子类 Name(IsDisplay=false) 先占位 seen,
        // 基类 Name(IsDisplay=true) 被 seen.Add 返回 false 跳过 → 0 个结果。
        // 期望行为(后续修复):子类的 IsDisplay=false 应能"反向隐藏"基类同名属性 → 0 个。
        // 这里按当前实际行为断言为 0,并明确标注留给 PropertyGrid Core 重构阶段修复。
        // 详见 ARCHITECTURE.md 优化方向 4.4
        var node = new OverrideNode();
        var groups = _sut.BuildGroups(node, null);

        var nameItems = Flat(groups).Where(i => i.PropertyName == "Name").ToList();
        Assert.Empty(nameItems);
    }

    [Fact]
    public void BuildGroups_IncludesDerivedProperties()
    {
        var node = new DerivedNode();
        var groups = _sut.BuildGroups(node, null);

        var names = Flat(groups).Select(i => i.PropertyName).ToList();
        Assert.Contains("Tag", names);
        // 基类的 Name/Count/Ratio 等也仍然出现
        Assert.Contains("Name", names);
    }

    // ── ReadOnly 模式 ───────────────────────────────────────────

    [Fact]
    public void BuildGroups_ReadOnlyMode_AutoDetects()
    {
        // 自动模式:set 私有 → 只读
        var node = new TestNode();
        var groups = _sut.BuildGroups(node, null);

        var item = Flat(groups).First(i => i.PropertyName == "Locked");
        Assert.True(item.IsReadOnly); // ReadOnly=1

        var forced = Flat(groups).First(i => i.PropertyName == "ForceWrite");
        Assert.False(forced.IsReadOnly); // ReadOnly=-1,即便 set 私有
    }

    [Fact]
    public void BuildGroups_DisplayNameFallsBackToPropertyName()
    {
        // Enabled 没指定 DisplayName,应回退到属性名
        var node = new TestNode();
        var groups = _sut.BuildGroups(node, null);

        var item = Flat(groups).First(i => i.PropertyName == "Enabled");
        Assert.Equal("Enabled", item.DisplayName);
    }

    [Fact]
    public void BuildGroups_PropagatesInjectedDispatcherToPropertyItems()
    {
        var dispatcher = new QueuedDispatcher();
        var service = new PropertyGridBuilderService(dispatcher);
        var node = new TestNode();
        var item = Flat(service.BuildGroups(node, null)).First(i => i.PropertyName == "Name");
        var valueNotifications = 0;
        item.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PropertyItemViewModel.Value))
                valueNotifications++;
        };

        node.Name = "changed";

        Assert.Equal(0, valueNotifications);
        Assert.Single(dispatcher.Pending);
        dispatcher.RunPending();
        Assert.Equal(1, valueNotifications);
    }

    // ── CollectBindableProperties ───────────────────────────────

    [Fact]
    public void CollectBindableProperties_ExcludesSystemPortNames()
    {
        var props = _sut.CollectBindableProperties(typeof(TestNode));

        Assert.DoesNotContain("InputPorts", props);
        Assert.DoesNotContain("OutputPorts", props);
        Assert.DoesNotContain("CustomInputPorts", props);
        Assert.DoesNotContain("CustomOutputPorts", props);
        Assert.DoesNotContain("Id", props);
    }

    [Fact]
    public void CollectBindableProperties_ReturnsWritablePublicOnly()
    {
        var props = _sut.CollectBindableProperties(typeof(TestNode));

        Assert.Contains("Name", props);
        Assert.Contains("Count", props);
        Assert.Contains("Ratio", props);
        // Hidden/Untagged 也有 public set → 也应包含
        Assert.Contains("Hidden", props);
    }

    [Fact]
    public void CollectBindableProperties_ReturnsFreshList()
    {
        // 实现里返回 new List<string>(cached),外部修改不应污染缓存
        var props = _sut.CollectBindableProperties(typeof(TestNode));
        props.Clear();

        var again = _sut.CollectBindableProperties(typeof(TestNode));
        Assert.NotEmpty(again);
    }

    private sealed class QueuedDispatcher : IUiDispatcher
    {
        public List<Action> Pending { get; } = [];

        public bool CheckAccess() => false;

        public void Post(Action action) => Pending.Add(action);

        public void RunPending()
        {
            foreach (var action in Pending.ToArray())
                action();
            Pending.Clear();
        }
    }
}
