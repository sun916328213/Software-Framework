using System.ComponentModel;
using System.Reflection;
using System.Windows;
using Vision.Core;
using Vision.Core.Results;
using WPFCustomControls.PropertyGrid.Core.Models;
using WPFCustomControls.PropertyGrid.Core.ViewModels;

#nullable enable
namespace Vision.Controls.Presentation;

/// <summary>共用 Core 行的元数据、值访问、搜索及订阅释放；专用编辑器保持原 DTO 绑定和校验语义。</summary>
public sealed class VisionPropertyItem : PropertyItemViewModel
{
    private readonly Func<VisionPropertyItem, FrameworkElement> _createEditor;
    private readonly PropertyInfo? _visibilityProperty;
    private readonly PropertyInfo? _displayNameProperty;
    private string _validationText = string.Empty;
    internal object Target { get; }
    internal PropertyInfo Property { get; }
    internal ToolPropertyAttribute Metadata { get; }
    public string Label => (_displayNameProperty?.GetValue(Target) as string ?? DisplayName) + ":";
    public string ValidationText => _validationText;
    public bool HasValidationIssues => _validationText.Length != 0;
    public bool IsFullWidth => Metadata.EditorType is E_PropertyEditorType.DataGridList or E_PropertyEditorType.DataClassList;
    public bool IsRowVisible => HasValidationIssues || (_visibilityProperty?.GetValue(Target) is not bool visible || visible);
    public string HelpText { get; }

    internal VisionPropertyItem(object target, PropertyInfo property, ToolPropertyAttribute metadata,
        Func<VisionPropertyItem, FrameworkElement> createEditor)
        : base(target, property, new NodePropertyAttribute
        {
            DisplayName = metadata.DisplayName, Description = metadata.Description ?? string.Empty,
            Group = metadata.Group ?? string.Empty, Order = metadata.Order,
            Min = metadata.Min, Max = metadata.Max,
            Step = metadata.Step == 0 ? (metadata.EditorType == E_PropertyEditorType.Int ? 1 : 0.001) : metadata.Step,
            ReadOnly = PropertyEditorBindingHelper.IsEditableProperty(property) ? 0 : 1,
            PortConfigurable = false
        })
    {
        Target = target; Property = property; Metadata = metadata; _createEditor = createEditor;
        var visibility = string.IsNullOrWhiteSpace(metadata.IsVisibleProperty) ? null
            : target.GetType().GetProperty(metadata.IsVisibleProperty, BindingFlags.Public | BindingFlags.Instance);
        _visibilityProperty = visibility?.PropertyType == typeof(bool) ? visibility : null;
        _displayNameProperty = string.IsNullOrWhiteSpace(metadata.DisplayNameProperty) ? null
            : target.GetType().GetProperty(metadata.DisplayNameProperty, BindingFlags.Public | BindingFlags.Instance);
        if ((_visibilityProperty != null || _displayNameProperty != null) && target is INotifyPropertyChanged notifier)
            PropertyChangedEventManager.AddHandler(notifier, OnVisibilityPropertyChanged, string.Empty);
        HelpText = metadata.Description ?? string.Empty;
        if (property.PropertyType.IsEnum)
            HelpText += "\n可选值:\n" + string.Join("\n", Enum.GetValues(property.PropertyType).Cast<object>().Select(value =>
            {
                var description = property.PropertyType.GetField(value.ToString()!)?.GetCustomAttribute<DescriptionAttribute>()?.Description;
                return $"- {value}{(string.IsNullOrEmpty(description) ? string.Empty : $" ({description})")}";
            }));
    }
    internal FrameworkElement CreateEditor() => _createEditor(this);

    internal void SetValidation(ValidationResult? result)
    {
        var text = string.Join("\n", result?.Issues.Where(issue => issue != null && issue.Member == PropertyName)
            .Select(issue => $"[{issue.Severity}] {issue.Message}") ?? []);
        if (_validationText == text) return;
        _validationText = text;
        OnPropertyChanged(nameof(ValidationText));
        OnPropertyChanged(nameof(HasValidationIssues));
        OnPropertyChanged(nameof(IsRowVisible)); // 有错误的隐藏字段必须可见，以便修复；不改变原全量校验。
    }

    private void OnVisibilityPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == _visibilityProperty?.Name)
            OnPropertyChanged(nameof(IsRowVisible));
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == _displayNameProperty?.Name)
            OnPropertyChanged(nameof(Label));
    }
}
