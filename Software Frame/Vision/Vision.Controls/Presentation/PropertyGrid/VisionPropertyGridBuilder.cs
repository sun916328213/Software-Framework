using System.ComponentModel;
using System.Reflection;
using System.Windows;
using Vision.Core;
using WPFCustomControls.PropertyGrid.Core.Models;
using WPFCustomControls.PropertyGrid.Core.Services;
using WPFCustomControls.PropertyGrid.Core.ViewModels;

#nullable enable
namespace Vision.Controls.Presentation;

/// <summary>Vision 元数据到通用属性行的适配；不向 Core/Tools 注入界面依赖。</summary>
internal sealed class VisionPropertyGridBuilder(Func<VisionPropertyItem, FrameworkElement> createEditor) : IPropertyGridBuilderService
{
    public bool AutoGenerateProperties { get; set; }
    public string? PropertyNames { get; set; }
    public ToolPropertyArea? PropertyArea { get; set; }

    public IReadOnlyList<PropertyGroupViewModel> BuildGroups(object? target, IPortConfigurable? portConfig)
    {
        if (target == null) return [];
        var allowed = PropertyNames?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var properties = target.GetType().GetProperties()
            .Where(p => allowed == null || allowed.Contains(p.Name))
            .Select(p => (Property: p, Metadata: Metadata(p)))
            .Where(item => PropertyEditorBindingHelper.IsDisplayableProperty(item.Property, item.Metadata))
            .Where(item => PropertyArea == null || item.Metadata!.Area == PropertyArea)
            .GroupBy(item => item.Metadata!.Group).OrderBy(group => group.Key);
        var groups = new List<PropertyGroupViewModel>();
        foreach (var group in properties)
        {
            var model = new PropertyGroupViewModel(group.Key ?? string.Empty);
            foreach (var item in group.OrderBy(item => item.Metadata!.Order))
                model.AddItem(new VisionPropertyItem(target, item.Property, item.Metadata!, createEditor));
            groups.Add(model);
        }
        return groups;
    }

    public List<string> CollectBindableProperties(Type type) => [];

    private ToolPropertyAttribute? Metadata(PropertyInfo property)
    {
        var declared = property.GetCustomAttribute<ToolPropertyAttribute>();
        if (declared != null || !AutoGenerateProperties) return declared;
        if (property.GetIndexParameters().Length != 0 || property.GetMethod?.IsPublic != true ||
            property.GetCustomAttribute<BrowsableAttribute>()?.Browsable == false ||
            property.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() != null) return null;
        var type = property.PropertyType;
        E_PropertyEditorType? editor = type == typeof(string) ? E_PropertyEditorType.Text :
            type == typeof(int) ? E_PropertyEditorType.Int : type == typeof(bool) ? E_PropertyEditorType.Checkbox :
            type == typeof(double) || type == typeof(float) || type == typeof(decimal) ? E_PropertyEditorType.Double :
            type.IsEnum ? E_PropertyEditorType.SingleSelect : type == typeof(Vision.Core.Geometry.Point2D) ? E_PropertyEditorType.Point : null;
        return editor == null ? null : new ToolPropertyAttribute
        {
            DisplayName = property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? property.Name,
            Description = property.GetCustomAttribute<DescriptionAttribute>()?.Description,
            Group = property.GetCustomAttribute<CategoryAttribute>()?.Category ?? "参数", EditorType = editor.Value
        };
    }
}


