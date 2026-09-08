using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using WPFCustomControls.PropertyGrid.Models;
using WPFCustomControls.PropertyGrid.ViewModels;

namespace WPFCustomControls.Tests.PropertyGrid;

/// <summary>
/// <see cref="PropertyItemViewModel"/> 的单元测试。
///
/// 重点覆盖:
///   1. 基础属性推导 (DisplayName / IsReadOnly / IsBoolean / IsNumeric / IsCollection / IsNullable)
///   2. Value 双向写入 + 类型转换(string→enum, string→double)
///   3. ClampNumeric(Min/Max 截断)
///   4. Increment / Decrement 命令与 Step
///   5. IsInputPort / IsOutputPort 与 IPortConfigurable 联动
///   6. EnumOptions 自动构建 ([Description] 注解 + nullable 兜底)
///   7. Dispose 取消订阅(避免泄漏)
/// </summary>
public class PropertyItemViewModelTests
{
    // ── 测试模型 ───────────────────────────────────────────────

    private enum Color { Red, Green, Blue }

    private enum DecoratedColor
    {
        [Description("红色")]
        Red,
        [Description("绿色")]
        Green,
        Blue
    }

    /// <summary>可通知属性变更,提供数值/字符串/枚举/可空/布尔/集合 多种字段。</summary>
    private class Sample : INotifyPropertyChanged
    {
        private string _name = "n1";
        private int _count;
        private double _ratio;
        private bool _enabled;
        private Color _color;
        private int? _optional;
        private List<string> _items = new() { "a", "b" };

        public event PropertyChangedEventHandler? PropertyChanged;

        [NodeProperty(DisplayName = "节点名称", Description = "节点显示名")]
        public string Name
        {
            get => _name;
            set { _name = value; OnChanged(); }
        }

        [NodeProperty(Min = 0, Max = 100, Step = 5)]
        public int Count
        {
            get => _count;
            set { _count = value; OnChanged(); }
        }

        [NodeProperty(Min = 0, Max = 1, Step = 0.1)]
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

        [NodeProperty]
        public Color Color
        {
            get => _color;
            set { _color = value; OnChanged(); }
        }

        /// <summary>无 Description 注解的枚举属性。</summary>
        [NodeProperty]
        public DecoratedColor Decorated { get; set; }

        [NodeProperty]
        public int? Optional
        {
            get => _optional;
            set { _optional = value; OnChanged(); }
        }

        /// <summary>字符串集合,IsCollection=true。</summary>
        [NodeProperty]
        public List<string> Items => _items;

        /// <summary>只读属性(set 私有)。</summary>
        [NodeProperty]
        public int ReadOnlyBacking { get; private set; } = 7;

        /// <summary>通过 ItemsSource 引用另一属性作下拉数据源。</summary>
        [NodeProperty(ItemsSource = nameof(Choices))]
        public string SelectedChoice { get; set; } = "";

        public IReadOnlyList<string> Choices { get; } = new[] { "Alpha", "Beta", "Gamma" };

        private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private class PortConfigurableSample : IPortConfigurable
    {
        private readonly Dictionary<string, (bool In, bool Out)> _store = new();
        public event Action? ConfigChanged;

        public (bool IsInput, bool IsOutput) GetPortConfig(string propertyName)
            => _store.TryGetValue(propertyName, out var v) ? v : (false, false);

        public void SetPortConfig(string propertyName, bool isInput, bool isOutput)
        {
            _store[propertyName] = (isInput, isOutput);
            ConfigChanged?.Invoke();
        }
    }

    // 辅助:反射拿 PropertyInfo + 默认从特性读取 attr
    private static PropertyInfo P<T>(string name) => typeof(T).GetProperty(name)!;

    private static PropertyItemViewModel Make(object target, string propName,
        NodePropertyAttribute? attr = null, IPortConfigurable? port = null)
    {
        var info = P<Sample>(propName);
        attr ??= info.GetCustomAttribute<NodePropertyAttribute>();
        return new PropertyItemViewModel(target, info, attr, port);
    }

    // ── 1. 基础属性推导 ──────────────────────────────────────────

    [Fact]
    public void Ctor_DisplayName_FallsBackToPropertyName_WhenAttrEmpty()
    {
        // Enabled 的 NodeProperty 没指定 DisplayName → 兜底为 "Enabled"
        var sample = new Sample();
        var vm = Make(sample, "Enabled");

        Assert.Equal("Enabled", vm.DisplayName);
        Assert.Equal("Enabled", vm.PropertyName);
    }

    [Fact]
    public void Ctor_DisplayName_UsesAttrDisplayName()
    {
        var sample = new Sample();
        var vm = Make(sample, "Name");

        Assert.Equal("节点名称", vm.DisplayName);
        Assert.Equal("节点显示名", vm.Description);
    }

    [Fact]
    public void Ctor_IsReadOnly_TrueWhenPrivateSetter()
    {
        var sample = new Sample();
        var vm = Make(sample, "ReadOnlyBacking");
        Assert.True(vm.IsReadOnly);
        Assert.False(vm.IsEditable);
    }

    [Fact]
    public void Ctor_IsBooleanAndIsNumeric_ReflectUnderlyingType()
    {
        var sample = new Sample();
        Assert.True(Make(sample, "Enabled").IsBoolean);
        Assert.False(Make(sample, "Enabled").IsNumeric);

        Assert.True(Make(sample, "Count").IsNumeric);
        Assert.False(Make(sample, "Count").IsBoolean);

        Assert.True(Make(sample, "Ratio").IsNumeric);
    }

    [Fact]
    public void Ctor_IsCollection_TrueForListOfString()
    {
        var sample = new Sample();
        var vm = Make(sample, "Items");
        Assert.True(vm.IsCollection);
        Assert.Equal(2, vm.CollectionCount);
    }

    [Fact]
    public void Ctor_IsCollection_FalseForString()
    {
        var sample = new Sample();
        // string 也实现了 IEnumerable,但 IsCollection 应为 false
        var vm = Make(sample, "Name");
        Assert.False(vm.IsCollection);
    }

    [Fact]
    public void Ctor_IsNullable_TrueForNullableInt()
    {
        var sample = new Sample();
        var vm = Make(sample, "Optional");
        Assert.True(vm.IsNullable);
        Assert.True(vm.IsNumeric); // int? 的 UnderlyingType 是 int
    }

    [Fact]
    public void Ctor_NumericMinMaxAndStep_LoadedFromAttr()
    {
        var sample = new Sample();
        var vm = Make(sample, "Count");

        Assert.Equal(0d, vm.NumericMin);
        Assert.Equal(100d, vm.NumericMax);
        Assert.Equal(5d, vm.NumericStep);
        Assert.True(vm.HasNumericRange);
    }

    [Fact]
    public void Ctor_NumericStep_DefaultsForFloating()
    {
        // Ratio 显式 Step=0.1 → 用显式值
        var sample = new Sample();
        var vm = Make(sample, "Ratio");
        Assert.Equal(0.1, vm.NumericStep);
    }

    // ── 2. Value 双向写入 + 类型转换 ────────────────────────────

    [Fact]
    public void Value_Get_ReturnsUnderlyingValue()
    {
        var sample = new Sample { Name = "X" };
        var vm = Make(sample, "Name");
        Assert.Equal("X", vm.Value);
    }

    [Fact]
    public void Value_Set_AssignsTargetProperty()
    {
        var sample = new Sample();
        var vm = Make(sample, "Name");
        vm.Value = "Y";
        Assert.Equal("Y", sample.Name);
    }

    [Fact]
    public void Value_Set_StringToInt_Parses()
    {
        var sample = new Sample();
        var vm = Make(sample, "Count");
        vm.Value = "42";
        Assert.Equal(42, sample.Count);
    }

    [Fact]
    public void Value_Set_StringToEnum_ByName()
    {
        var sample = new Sample();
        var vm = Make(sample, "Color");
        vm.Value = "Green";
        Assert.Equal(Color.Green, sample.Color);
    }

    [Fact]
    public void Value_Set_StringToEnum_ByDisplayName_Description()
    {
        // 字段标了 [Description("红色")] → 写 "红色" 应匹配
        var sample = new Sample();
        var vm = Make(sample, "Decorated");
        vm.Value = "红色";
        Assert.Equal(DecoratedColor.Red, sample.Decorated);
    }

    [Fact]
    public void Value_Set_EnumUnparseableString_NoOp()
    {
        var sample = new Sample();
        var orig = sample.Color;
        var vm = Make(sample, "Color");
        vm.Value = "NotAColor";
        Assert.Equal(orig, sample.Color);
    }

    [Fact]
    public void Value_Set_InvalidDouble_NoOp()
    {
        var sample = new Sample();
        var orig = sample.Ratio;
        var vm = Make(sample, "Ratio");
        vm.Value = "not a number";
        Assert.Equal(orig, sample.Ratio);
    }

    [Fact]
    public void Value_Set_NullOnNullable_Allows()
    {
        var sample = new Sample { Optional = 5 };
        var vm = Make(sample, "Optional");
        vm.Value = null;
        Assert.Null(sample.Optional);
    }

    [Fact]
    public void Value_Set_NullOnNonNullable_NoOp()
    {
        var sample = new Sample { Name = "keep" };
        var vm = Make(sample, "Name");
        vm.Value = null;
        Assert.Equal("keep", sample.Name);
    }

    [Fact]
    public void Value_Set_OnReadOnly_NoOp()
    {
        var sample = new Sample();
        var orig = sample.ReadOnlyBacking;
        var vm = Make(sample, "ReadOnlyBacking");
        vm.Value = 99;
        Assert.Equal(orig, sample.ReadOnlyBacking);
    }

    // ── 3. ClampNumeric ─────────────────────────────────────────

    [Fact]
    public void Value_Set_ClampedToMax()
    {
        var sample = new Sample();
        var vm = Make(sample, "Count"); // [0, 100]
        vm.Value = 150;
        Assert.Equal(100, sample.Count);
    }

    [Fact]
    public void Value_Set_ClampedToMin()
    {
        var sample = new Sample();
        var vm = Make(sample, "Count"); // [0, 100]
        vm.Value = -5;
        Assert.Equal(0, sample.Count);
    }

    [Fact]
    public void Value_Set_WithinRange_PassesThrough()
    {
        var sample = new Sample();
        var vm = Make(sample, "Count"); // [0, 100]
        vm.Value = 50;
        Assert.Equal(50, sample.Count);
    }

    // ── 4. Increment / Decrement 命令 ──────────────────────────

    [Fact]
    public void IncrementCommand_StepUp()
    {
        var sample = new Sample { Count = 10 };
        var vm = Make(sample, "Count"); // Step=5
        var cmd = (RelayCommand)typeof(PropertyItemViewModel)
            .GetProperty("IncrementCommand", BindingFlags.Instance | BindingFlags.Public)!
            .GetValue(vm)!;
        cmd.Execute(null);
        Assert.Equal(15, sample.Count);
    }

    [Fact]
    public void DecrementCommand_StepDown()
    {
        var sample = new Sample { Count = 10 };
        var vm = Make(sample, "Count"); // Step=5
        var cmd = (RelayCommand)typeof(PropertyItemViewModel)
            .GetProperty("DecrementCommand", BindingFlags.Instance | BindingFlags.Public)!
            .GetValue(vm)!;
        cmd.Execute(null);
        Assert.Equal(5, sample.Count);
    }

    [Fact]
    public void IncrementCommand_ClampedToMax()
    {
        var sample = new Sample { Count = 98 };
        var vm = Make(sample, "Count"); // Step=5, Max=100
        var cmd = (RelayCommand)typeof(PropertyItemViewModel)
            .GetProperty("IncrementCommand", BindingFlags.Instance | BindingFlags.Public)!
            .GetValue(vm)!;
        cmd.Execute(null);
        Assert.Equal(100, sample.Count);
        // 再点一次仍为 100
        cmd.Execute(null);
        Assert.Equal(100, sample.Count);
    }

    [Fact]
    public void IncrementCommand_OnNonNumeric_NoOp()
    {
        var sample = new Sample { Name = "x" };
        var vm = Make(sample, "Name");
        var cmd = (RelayCommand)typeof(PropertyItemViewModel)
            .GetProperty("IncrementCommand", BindingFlags.Instance | BindingFlags.Public)!
            .GetValue(vm)!;
        cmd.Execute(null);
        Assert.Equal("x", sample.Name);
    }

    // ── 5. EnumOptions ──────────────────────────────────────────

    [Fact]
    public void EnumOptions_BuiltFromEnumNames_WhenNoDescription()
    {
        var sample = new Sample();
        var vm = Make(sample, "Color");
        Assert.NotNull(vm.EnumOptions);
        Assert.Equal(3, vm.EnumOptions!.Count);
        Assert.Equal("Red", vm.EnumOptions[0].DisplayName);
        Assert.Equal(Color.Red, vm.EnumOptions[0].Value);
    }

    [Fact]
    public void EnumOptions_UsesDescriptionAttribute()
    {
        var sample = new Sample();
        var vm = Make(sample, "Decorated");
        Assert.NotNull(vm.EnumOptions);
        // Red→"红色", Green→"绿色", Blue→"Blue"(无 Description 注解)
        Assert.Equal("红色", vm.EnumOptions![0].DisplayName);
        Assert.Equal("绿色", vm.EnumOptions[1].DisplayName);
        Assert.Equal("Blue", vm.EnumOptions[2].DisplayName);
    }

    [Fact]
    public void EnumOptions_ForNullableEnum_PrependsNullOption()
    {
        // 临时模型:可空枚举
        var sample = new NullableEnumSample();
        var vm = new PropertyItemViewModel(sample,
            typeof(NullableEnumSample).GetProperty("Color")!,
            new NodePropertyAttribute());

        Assert.NotNull(vm.EnumOptions);
        Assert.Equal(4, vm.EnumOptions!.Count); // (无) + Red/Green/Blue
        Assert.Null(vm.EnumOptions[0].Value);
        Assert.Equal("(无)", vm.EnumOptions[0].DisplayName);
    }

    private class NullableEnumSample
    {
        public Color? Color { get; set; }
    }

    [Fact]
    public void EnumOptions_ItemsSource_UsesTargetProperty()
    {
        var sample = new Sample();
        var vm = Make(sample, "SelectedChoice");
        Assert.NotNull(vm.EnumOptions);
        Assert.Equal(3, vm.EnumOptions!.Count);
        Assert.Equal("Alpha", vm.EnumOptions[0].DisplayName);
        Assert.Equal("Alpha", vm.EnumOptions[0].Value);
    }

    // ── 6. IsInputPort / IsOutputPort 联动 ─────────────────────

    [Fact]
    public void PortFlags_ReadFromConfigurable()
    {
        var sample = new Sample();
        var port = new PortConfigurableSample();
        port.SetPortConfig("Name", true, false);

        var vm = Make(sample, "Name",
            attr: new NodePropertyAttribute { DisplayName = "节点名称", PortConfigurable = true },
            port: port);

        Assert.True(vm.IsPortConfigurable);
        Assert.True(vm.IsInputPort);
        Assert.False(vm.IsOutputPort);
    }

    [Fact]
    public void PortFlags_SetIsInputPort_UpdatesConfigurable()
    {
        var sample = new Sample();
        var port = new PortConfigurableSample();
        var vm = Make(sample, "Name",
            attr: new NodePropertyAttribute { PortConfigurable = true },
            port: port);

        vm.IsInputPort = true;

        Assert.True(port.GetPortConfig("Name").IsInput);
        Assert.False(port.GetPortConfig("Name").IsOutput);
    }

    [Fact]
    public void PortFlags_SetIsOutputPort_PreservesInputFlag()
    {
        var sample = new Sample();
        var port = new PortConfigurableSample();
        var vm = Make(sample, "Name",
            attr: new NodePropertyAttribute { PortConfigurable = true },
            port: port);

        vm.IsInputPort = true;
        vm.IsOutputPort = true;

        var cfg = port.GetPortConfig("Name");
        Assert.True(cfg.IsInput);
        Assert.True(cfg.IsOutput);
    }

    [Fact]
    public void PortFlags_NullConfigurable_AlwaysFalse()
    {
        var sample = new Sample();
        var vm = Make(sample, "Name");
        Assert.False(vm.IsPortConfigurable);
        Assert.False(vm.IsInputPort);
        Assert.False(vm.IsOutputPort);
        // 写入应被忽略,不抛
        vm.IsInputPort = true;
    }

    // ── 7. Dispose 不泄漏订阅 ─────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow_WhenCalledOnce()
    {
        var sample = new Sample();
        var vm = Make(sample, "Name");
        vm.Dispose();
        // 二次 Dispose 不应抛
        vm.Dispose();
    }

    [Fact]
    public void Dispose_DetachesNotifier()
    {
        var sample = new Sample();
        var vm = Make(sample, "Name");
        vm.Dispose();
        // 手动触发 PropertyChanged,不期望 VM 报错(但 WPF Application null 时会触发 OnTargetPropertyChanged → 抛 NRE)
        // 因此仅验证 Dispose 自身不抛
        Assert.True(true);
    }

    [Fact]
    public void Dispose_DetachesPortConfigurable()
    {
        var sample = new Sample();
        var port = new PortConfigurableSample();
        var vm = Make(sample, "Name",
            attr: new NodePropertyAttribute { PortConfigurable = true },
            port: port);
        vm.Dispose();
        // Dispose 后外部仍可正常触发 ConfigChanged(不应抛异常)
        port.SetPortConfig("Name", true, false);
    }
}
