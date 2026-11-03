using System.Collections;
using System.ComponentModel;
using System.Reflection;
using WPFCustomControls.PropertyGrid.Core.Models;
using WPFCustomControls.PropertyGrid.Core.ViewModels;

namespace WPFCustomControls.Tests.PropertyGrid;

public sealed class PropertyGridMetadataTests
{
    [Fact]
    public void TypedOptions_SeparateStableValueFromLabel_AndSupportWpfUntypedIndexLookup()
    {
        var target = new Target();
        using var row = Row(target, nameof(Target.Source));
        var option = Assert.Single(row.EnumOptions!);
        Assert.Equal("source-id", option.Value); Assert.Equal("来源显示名", option.DisplayName);
        // WPF在SelectedValuePath初始化期间可能使用原始值查询IList；不能抛InvalidCastException。
        Assert.Equal(-1, ((IList)row.EnumOptions!).IndexOf("source-id"));
        row.Value = option.Value;
        target.Options = [new("source-id", "已重命名")];
        target.Notify(nameof(Target.Options));
        Assert.Equal("source-id", row.Value);
        Assert.Equal("已重命名", Assert.Single(row.EnumOptions!).DisplayName);
    }

    [Fact]
    public void Visibility_CombinesWithSearch_PreservesOriginalRow_AndUnsubscribesOnDispose()
    {
        var target = new Target();
        var row = Row(target, nameof(Target.Count));
        var group = new PropertyGroupViewModel("动态分组");
        group.AddItem(row); group.ApplyFilter("数量");
        Assert.Empty(group.Items); Assert.Same(row, Assert.Single(group.AllItems));
        target.Show = true; target.Notify(nameof(Target.Show));
        Assert.Same(row, Assert.Single(group.Items));
        row.Value = 9; Assert.Equal(9, target.Count);
        target.Show = false; target.Notify(nameof(Target.Show));
        Assert.Empty(group.Items);
        group.ApplyFilter(""); Assert.Empty(group.Items);
        group.Dispose();
        target.Show = true; target.Notify(nameof(Target.Show));
        Assert.False(row.IsVisible);
    }

    [Fact]
    public void SelectedOption_FollowsStableValueAndReplacementLabel_WithoutStoringAnotherSelection()
    {
        var target = new Target(); using var row = Row(target, nameof(Target.Source));
        Assert.True(row.KeepOptionSelection);
        row.Value = Assert.Single(row.EnumOptions!).Value;
        Assert.Equal("source-id", target.Source);
        target.Options = [new("source-id", "改名之后")]; target.Notify(nameof(Target.Options));
        Assert.Equal("改名之后", row.SelectedOption!.DisplayName); Assert.Equal("source-id", row.Value);
        target.Options = [new("different-id", "另一个来源")]; target.Notify(nameof(Target.Options));
        Assert.Null(row.SelectedOption); Assert.Equal("source-id", row.Value); // 缺少候选不能擅改稳定值。
        row.Value = ""; Assert.Equal("", target.Source);
    }

    [Fact]
    public void FilePathMetadata_IsExplicit_AndWritesOriginalValue_WithoutChangingOrdinaryStrings()
    {
        var target = new Target();
        using var path = Row(target, nameof(Target.ImagePath)); using var ordinary = Row(target, nameof(Target.Source));
        Assert.True(path.IsFilePath); Assert.Equal("图像|*.png", path.FileDialogFilter);
        Assert.False(ordinary.IsFilePath); Assert.Equal("", ordinary.FileDialogFilter);
        path.Value = "chosen.png"; Assert.Equal("chosen.png", target.ImagePath);
        Assert.False(path.IsPortConfigurable);
    }

    private static PropertyItemViewModel Row(Target target, string name)
    {
        var property = typeof(Target).GetProperty(name)!;
        return new(target, property, property.GetCustomAttribute<NodePropertyAttribute>());
    }
    private sealed class Target : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Notify(string name) => PropertyChanged?.Invoke(this, new(name));
        public IReadOnlyList<EnumOption> Options { get; set; } = [new("source-id", "来源显示名")];
        [NodeProperty(ItemsSource = nameof(Options), KeepOptionSelection = true)]
        public string Source { get; set; } = "";
        [NodeProperty(FileDialogFilter = "图像|*.png", PortConfigurable = false)]
        public string ImagePath { get; set; } = "";
        public bool Show { get; set; }
        [NodeProperty(DisplayName = "数量", IsVisibleProperty = nameof(Show))]
        public int Count { get; set; }
    }
}
