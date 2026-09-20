using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Communication.Controls.Services;
using CommunicationNew.Core;
using CommunicationNew.Protocols.Serial;
using Prism.Services.Dialogs;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace Communication.Controls.ViewModels;

/// <summary>单个参数编辑字段（反射驱动，含枚举/布尔/数值/文本四种编辑形态）。</summary>
public sealed partial class ParamField : ObservableObject
{
    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    public ParamField(PropertyInfo property, object target)
    {
        Property = property;
        Target = target;
        Value = property.GetValue(target);
        _text = ValueToString(Value);
        Description = ParamMetadata.GetLabel(property.Name);
        Hint = ParamMetadata.GetHint(property.Name);
    }

    public PropertyInfo Property { get; }

    public object Target { get; }

    /// <summary>中文显示名（查 ParamMetadata，查不到回退英文）。</summary>
    public string Description { get; }

    /// <summary>中文提示（含英文名，绑定到 ToolTip）。</summary>
    public string Hint { get; }

    public string Key => Property.Name;

    /// <summary>可选项（枚举类型自动生成下拉）。</summary>
    public IReadOnlyList<string>? Options { get; init; }

    /// <summary>可编辑下拉（串口号：下拉选 + 可手工输入）。</summary>
    public bool IsEditableCombo { get; init; }

    public bool IsOption => Options is { Count: > 0 };

    public bool IsBoolean => Property.PropertyType == typeof(bool);

    public object? Value { get; private set; }

    /// <summary>布尔编辑值。</summary>
    public bool IsChecked
    {
        get => Value is true;
        set
        {
            Value = value;
            Text = value.ToString();
            HasError = false;
        }
    }

    partial void OnTextChanged(string value)
    {
        try
        {
            Value = ConvertText(value, Property.PropertyType);
            HasError = false;
            OnPropertyChanged(nameof(IsChecked));
        }
        catch
        {
            HasError = true;
        }
    }

    /// <summary>把编辑结果写回参数对象（枚举/布尔/byte[]/可空类型分别处理，避免 Convert.ChangeType 误转）。</summary>
    public void Commit()
    {
        var type = Property.PropertyType;
        var value = Value;
        if (value is null)
        {
            if (!type.IsValueType || Nullable.GetUnderlyingType(type) is not null)
            {
                Property.SetValue(Target, null);
            }
            return;
        }

        var target = Nullable.GetUnderlyingType(type) ?? type;
        object? converted;
        if (target.IsEnum)
        {
            converted = Enum.Parse(target, value.ToString()!, ignoreCase: true);
        }
        else if (target == typeof(byte[]))
        {
            converted = value is byte[] bytes ? bytes : ConvertHexText(value.ToString() ?? string.Empty);
        }
        else if (target == typeof(bool))
        {
            converted = value is bool b ? b : bool.Parse(value.ToString()!);
        }
        else if (target == typeof(string))
        {
            converted = value.ToString();
        }
        else
        {
            converted = Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture);
        }
        Property.SetValue(Target, converted);
    }

    private static object? ConvertText(string text, Type type)
    {
        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable is not null)
        {
            return string.IsNullOrWhiteSpace(text) ? null : ConvertText(text, nullable);
        }

        if (type.IsEnum)
        {
            return Enum.Parse(type, text.Trim(), ignoreCase: true);
        }

        if (type == typeof(byte[]))
        {
            return string.IsNullOrWhiteSpace(text) ? null : ConvertHexText(text);
        }

        if (type == typeof(bool))
        {
            return bool.Parse(text.Trim());
        }

        if (type == typeof(string))
        {
            return UnescapeControl(text.Trim());
        }

        return Convert.ChangeType(text.Trim(), type, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>控制字符在单行框里不可见（分隔符默认 CRLF 会显示成空框），编辑时统一转成 \r \n \t 字面量。</summary>
    private static string EscapeControl(string text) => text
        .Replace("\r", @"\r")
        .Replace("\n", @"\n")
        .Replace("\t", @"\t");

    private static string UnescapeControl(string text) => text
        .Replace(@"\r", "\r")
        .Replace(@"\n", "\n")
        .Replace(@"\t", "\t");

    /// <summary>byte[] 参数按十六进制文本编辑（空格分隔，可为空表示 null）。</summary>
    private static byte[]? ConvertHexText(string text)
    {
        var hex = new string([.. text.Where(c => !char.IsWhiteSpace(c))]);
        if (hex.Length == 0)
        {
            return null;
        }

        if (hex.Length % 2 != 0)
        {
            throw new FormatException("十六进制长度必须为偶数。");
        }

        return Convert.FromHexString(hex);
    }

    private static string ValueToString(object? value) => value switch
    {
        null => string.Empty,
        bool b => b ? bool.TrueString : bool.FalseString,
        byte[] bytes => BitConverter.ToString(bytes).Replace("-", " "),
        string s => EscapeControl(s),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
    };
}

/// <summary>角色选项显示。</summary>
public record RoleOption(DeviceRole Role, string DisplayName);

/// <summary>设备配置对话框视图模型：选择协议 → 反射生成参数表单 → 回传 CommunicationParam。</summary>
public sealed partial class DeviceConfigDialogViewModel : ObservableObject, IDialogAware
{
    [ObservableProperty]
    private ProtocolType _selectedProtocol = ProtocolType.ModbusTcp;

    [ObservableProperty]
    private DeviceRole _selectedRole = DeviceRole.Client;

    [ObservableProperty]
    private string _deviceName = string.Empty;

    private CommunicationParam _param = null!;

    private bool _isEdit;

    /// <summary>批量赋值时压住协议/角色变更引发的表单重建（编辑模式入口用）。</summary>
    private bool _suppressRebuild;

    /// <summary>用户手改过名称后不再跟着协议变；自动改名期间也不要把这次改动当成手改。</summary>
    private bool _nameEditedByUser;

    private bool _applyingSuggestedName;

    /// <summary>编辑模式：由调用方传入已有 param 进入，此时协议/角色锁定，只改参数。</summary>
    public bool IsEdit => _isEdit;

    /// <summary>协议/角色下拉是否可用：编辑模式锁定（换协议等于换设备，请新建）。</summary>
    public bool CanChangeProtocol => !_isEdit;

    public string ConfirmText => _isEdit ? "保存" : "创建";

    public DeviceConfigDialogViewModel()
    {
        // 原始收发协议排最前（调试最常用），地址读写协议其次；同名排序保持稳定。
        ProtocolOptions = ProtocolCatalog.All()
            .OrderBy(p => p.SupportsRaw ? 0 : 1)
            .ThenBy(p => p.Protocol.ToString(), StringComparer.Ordinal)
            .ToList();

        // 按能力分组下拉：「原始收发（TCP / UDP / 串口）」与「按地址读写（帧协议）」两段，
        // TCP Socket 与 Modbus TCP 这类近名项不再排在一起，避免选错。
        var view = CollectionViewSource.GetDefaultView(ProtocolOptions);
        view.GroupDescriptions.Add(new PropertyGroupDescription(
            nameof(ProtocolCatalog.ProtocolInfo.Protocol),
            new ProtocolCapabilityGroupConverter()));

        RoleOptions =
        [
            new RoleOption(DeviceRole.Client, "客户端"),
            new RoleOption(DeviceRole.Server, "服务端"),
        ];
        Fields.CollectionChanged += (_, _) => Validate();
        CommonFields.CollectionChanged += (_, _) => Validate();
        RebuildParam();
        _deviceName = SuggestName();
    }

    public string Title => _isEdit ? "编辑通信设备" : "新建通信设备";

    /// <summary>Prism 8 的对话框关闭事件（不是 Prism 9 的 DialogCloseListener）。</summary>
    public event Action<IDialogResult>? RequestClose;

    public ObservableCollection<ParamField> Fields { get; } = [];

    /// <summary>通用通信参数（超时/重试/重连/队列容量），来自 CommunicationParam 基类。</summary>
    public ObservableCollection<ParamField> CommonFields { get; } = [];

    public IReadOnlyList<ProtocolCatalog.ProtocolInfo> ProtocolOptions { get; }

    public IReadOnlyList<RoleOption> RoleOptions { get; }

    public IReadOnlyList<string> SerialPortNames { get; } = ProtocolCatalog.GetSerialPortNames();

    public ProtocolCatalog.ProtocolInfo SelectedProtocolInfo =>
        ProtocolOptions.First(p => p.Protocol == SelectedProtocol);

    public int SelectedProtocolIndex
    {
        get => ProtocolOptions.ToList().FindIndex(p => p.Protocol == SelectedProtocol);
        set
        {
            if (value >= 0 && value < ProtocolOptions.Count)
            {
                SelectedProtocol = ProtocolOptions[value].Protocol;
            }
        }
    }

    public int SelectedRoleIndex
    {
        get => SelectedRole == DeviceRole.Client ? 0 : 1;
        set => SelectedRole = value <= 0 ? DeviceRole.Client : DeviceRole.Server;
    }

    public bool RoleServerEnabled => SelectedProtocolInfo.SupportsServer;

    /// <summary>角色下拉是否可用：协议不支持服务端 / 编辑模式锁定，二者任一即禁用。</summary>
    public bool RoleComboEnabled => RoleServerEnabled && CanChangeProtocol;

    /// <summary>表单右上角提示：地址协议给地址样例，无地址语义的协议给收发形态说明。</summary>
    public string FormHint => SelectedProtocolInfo.SupportsAddressRead
        ? $"地址样例：{SelectedProtocolInfo.AddressHint}"
        : SelectedProtocolInfo.AddressRegexHint;

    public bool CanCloseDialog() => true;

    public void OnDialogClosed()
    {
    }

    public void OnDialogOpened(IDialogParameters parameters)
    {
        // 编辑模式：调用方传入的是 GetParam 取到的深拷贝，改坏了点取消直接丢弃，不影响在线设备。
        if (parameters.TryGetValue<CommunicationParam>("param", out var existing) && existing is not null)
        {
            _isEdit = true;
            _param = existing;
            // 走属性赋值（顺带触发校验），但压住重建：否则 SelectedProtocol 的变更会丢弃传进来的已有参数。
            _suppressRebuild = true;
            try
            {
                SelectedProtocol = existing.ProtocolType;
                SelectedRole = existing.Role;
                DeviceName = existing.DeviceName;
            }
            finally
            {
                _suppressRebuild = false;
            }
            BuildFields();
            RaiseFormStates();
            return;
        }

        // 每次新建都从"名称跟随协议"的初始状态开始（对话框实例可能被复用）。
        _nameEditedByUser = false;
        if (parameters.TryGetValue<ProtocolType>("protocol", out var protocol))
        {
            SelectedProtocol = protocol;
        }

        RebuildParam();
        SyncSuggestedName();
    }

    partial void OnSelectedProtocolChanged(ProtocolType value)
    {
        if (_suppressRebuild)
        {
            return;
        }

        RebuildParam();
        SyncSuggestedName();
        RaiseFormStates();
    }

    partial void OnSelectedRoleChanged(DeviceRole value)
    {
        if (_suppressRebuild)
        {
            return;
        }

        RebuildParam();
        SyncSuggestedName();
        RaiseFormStates();
    }

    partial void OnDeviceNameChanged(string value)
    {
        if (!_applyingSuggestedName)
        {
            _nameEditedByUser = true;
        }

        if (_param is not null)
        {
            _param.DeviceName = value;
        }
        Validate();
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        foreach (var field in Fields.Concat(CommonFields))
        {
            field.Commit();
        }

        _param.DeviceName = DeviceName.Trim();
        // 编辑模式保留原 DeviceId（调用方靠它做替换）；新建模式才分配。
        if (string.IsNullOrWhiteSpace(_param.DeviceId))
        {
            _param.DeviceId = Guid.NewGuid().ToString("N");
        }
        RequestClose?.Invoke(new DialogResult(ButtonResult.OK, new DialogParameters { { "param", _param } }));
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));

    private bool CanConfirm() => !string.IsNullOrWhiteSpace(DeviceName)
        && Fields.All(f => !f.HasError)
        && CommonFields.All(f => !f.HasError);

    private string SuggestName() =>
        $"{SelectedProtocolInfo.DisplayName}-{(SelectedRole == DeviceRole.Client ? "Client" : "Server")}";

    /// <summary>
    /// 名称跟着协议/角色走：一台设备在列表里靠名字最好认，
    /// 但用户一旦自己改过名字就以他写的为准，不能选个协议把人家的名字冲掉。
    /// </summary>
    private void SyncSuggestedName()
    {
        if (_isEdit || _nameEditedByUser)
        {
            return;
        }

        _applyingSuggestedName = true;
        try
        {
            DeviceName = SuggestName();
        }
        finally
        {
            _applyingSuggestedName = false;
        }
    }

    private void RebuildParam()
    {
        if (!SelectedProtocolInfo.SupportsServer && SelectedRole == DeviceRole.Server)
        {
            SelectedRole = DeviceRole.Client;
        }

        _param = ProtocolCatalog.CreateParam(SelectedProtocol, SelectedRole);
        _param.DeviceName = DeviceName;

        BuildFields();
    }

    /// <summary>按当前 _param 重建参数表单（新建/编辑共用；编辑模式的 _param 是调用方传进来的已有实例）。</summary>
    private void BuildFields()
    {
        Fields.Clear();
        CommonFields.Clear();
        foreach (var name in ProtocolCatalog.VisibleProtocolProperties(_param))
        {
            if (FindEditableProperty(_param, name) is not { } property)
            {
                continue;
            }

            var isPort = property.Name == nameof(SerialParam.PortName);
            Fields.Add(NewField(property,
                isPort ? SerialPortNames.ToList() : EnumOptions(property),
                isPort));
        }
        foreach (var name in ProtocolCatalog.VisibleCommonProperties(_param))
        {
            if (FindEditableProperty(_param, name) is not { } property)
            {
                continue;
            }

            CommonFields.Add(NewField(property, EnumOptions(property), isEditableCombo: false));
        }
        Validate();

        ParamField NewField(PropertyInfo property, IReadOnlyList<string>? options, bool isEditableCombo)
        {
            var field = new ParamField(property, _param)
            {
                Options = options,
                IsEditableCombo = isEditableCombo,
            };
            field.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ParamField.HasError))
                {
                    Validate();
                }
            };
            return field;
        }
    }

    /// <summary>CommunityToolkit 命令不挂 CommandManager，表单状态变化后要主动复查可用态。</summary>
    private void Validate() => ConfirmCommand.NotifyCanExecuteChanged();

    /// <summary>
    /// 按名字取可编辑属性：白名单里出现但参数上不存在、不可写或类型无法在表单编辑的名字直接跳过。
    /// </summary>
    private static PropertyInfo? FindEditableProperty(object target, string name)
    {
        var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (property is null || !property.CanWrite
            || property.GetCustomAttribute<ObsoleteAttribute>() is not null
            || !IsEditableType(property.PropertyType))
        {
            return null;
        }

        return property;
    }

    private static IReadOnlyList<string>? EnumOptions(PropertyInfo property)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        return type.IsEnum ? Enum.GetNames(type) : null;
    }

    private static bool IsEditableType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t.IsEnum || t == typeof(bool) || t == typeof(string))
        {
            return true;
        }

        return t.IsPrimitive || t == typeof(decimal) || t == typeof(byte[]);
    }

    private void RaiseFormStates()
    {
        OnPropertyChanged(nameof(IsEdit));
        OnPropertyChanged(nameof(CanChangeProtocol));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ConfirmText));
        OnPropertyChanged(nameof(SelectedProtocolInfo));
        OnPropertyChanged(nameof(SelectedProtocolIndex));
        OnPropertyChanged(nameof(SelectedRoleIndex));
        OnPropertyChanged(nameof(RoleServerEnabled));
        OnPropertyChanged(nameof(RoleComboEnabled));
        OnPropertyChanged(nameof(FormHint));
        OnPropertyChanged(nameof(DeviceName));
        ConfirmCommand.NotifyCanExecuteChanged();
    }
}

/// <summary>协议能力分组：按是否支持原始收发把下拉分成两段。</summary>
public sealed class ProtocolCapabilityGroupConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ProtocolType protocol && ProtocolCatalog.Describe(protocol).SupportsRaw
            ? "原始收发（TCP / UDP / 串口）"
            : "按地址读写（帧协议）";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
