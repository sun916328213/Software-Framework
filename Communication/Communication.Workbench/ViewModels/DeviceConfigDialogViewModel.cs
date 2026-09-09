using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Input;
using CommunicationNew.Core;
using Communication.Workbench.Services;

namespace Communication.Workbench.ViewModels;

/// <summary>单个参数编辑字段（反射驱动，含枚举/布尔/数值/文本四种编辑形态）。</summary>
public sealed class ParamField : INotifyPropertyChanged
{
    private object? _value;
    private string _text = string.Empty;
    private bool _hasError;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ParamField(PropertyInfo property, object target)
    {
        Property = property;
        Target = target;
        Value = property.GetValue(target);
        Text = ValueToString(Value);
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

    /// <summary>当前字符串表示（编辑入口）。</summary>
    public string Text
    {
        get => _text;
        set
        {
            if (SetField(ref _text, value))
            {
                ApplyText(value);
            }
        }
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetField(ref _hasError, value);
    }

    /// <summary>可选项（枚举类型自动生成下拉）。</summary>
    public IReadOnlyList<string>? Options { get; init; }

    public bool IsOption => Options is { Count: > 0 };

    public bool IsBoolean => Property.PropertyType == typeof(bool);

    /// <summary>可编辑下拉（串口号：下拉选 + 可手工输入）。</summary>
    public bool IsEditableCombo { get; set; }

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

    public object? Value
    {
        get => _value;
        private set => SetField(ref _value, value);
    }

    private void ApplyText(string text)
    {
        try
        {
            Value = ConvertText(text, Property.PropertyType);
            HasError = false;
            OnPropertyChanged(nameof(IsChecked));
        }
        catch
        {
            HasError = true;
        }
    }

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

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

        return Convert.ChangeType(text.Trim(), type, System.Globalization.CultureInfo.InvariantCulture);
    }

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
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private bool SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name!));
        return true;
    }
}

/// <summary>设备配置对话框视图模型：选择协议 → 动态生成参数表单 → 返回 CommunicationParam。</summary>
public sealed class DeviceConfigDialogViewModel : IDialogAware, INotifyPropertyChanged
{
    private ProtocolType _selectedProtocol = ProtocolType.ModbusTcp;
    private DeviceRole _selectedRole = DeviceRole.Client;
    private string _deviceName = string.Empty;
    private CommunicationParam _param = null!;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DeviceConfigDialogViewModel()
    {
        ProtocolOptions = ProtocolCatalog.All();
        RoleOptions =
        [
            new RoleOption(DeviceRole.Client, "客户端"),
            new RoleOption(DeviceRole.Server, "服务端"),
        ];
        RebuildParam();
        DeviceName = SuggestName();
    }

    public string Title => "新建通信设备";

    /// <summary>由 DialogService 注入的关闭回调。</summary>
    public DialogCloseListener RequestClose { get; }

    public ObservableCollection<ParamField> Fields { get; } = [];

    /// <summary>通用通信参数（超时/重试/重连/队列容量），对应 CommunicationParam 基类，单独成组编辑。</summary>
    public ObservableCollection<ParamField> CommonFields { get; } = [];

    public IReadOnlyList<ProtocolCatalog.ProtocolInfo> ProtocolOptions { get; }

    public IReadOnlyList<RoleOption> RoleOptions { get; }

    public IReadOnlyList<string> SerialPortNames { get; } = ProtocolCatalog.GetSerialPortNames();

    public ProtocolCatalog.ProtocolInfo SelectedProtocolInfo =>
        ProtocolOptions.First(p => p.Protocol == _selectedProtocol);

    public ProtocolType SelectedProtocol
    {
        get => _selectedProtocol;
        set
        {
            if (_selectedProtocol == value)
            {
                return;
            }

            _selectedProtocol = value;
            RebuildParam();
            RaiseAllChanged();
        }
    }

    public int SelectedProtocolIndex
    {
        get => ProtocolOptions.ToList().FindIndex(p => p.Protocol == _selectedProtocol);
        set
        {
            if (value >= 0 && value < ProtocolOptions.Count)
            {
                SelectedProtocol = ProtocolOptions[value].Protocol;
            }
        }
    }

    public DeviceRole SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (_selectedRole == value)
            {
                return;
            }

            _selectedRole = value;
            RebuildParam();
            RaiseAllChanged();
        }
    }

    public int SelectedRoleIndex
    {
        get => _selectedRole == DeviceRole.Client ? 0 : 1;
        set => SelectedRole = value <= 0 ? DeviceRole.Client : DeviceRole.Server;
    }

    public bool RoleServerEnabled => SelectedProtocolInfo.SupportsServer;

    public string AddressHint => SelectedProtocolInfo.AddressHint;

    public string DeviceName
    {
        get => _deviceName;
        set
        {
            _deviceName = value;
            if (_param is not null)
            {
                _param.DeviceName = value;
            }
        }
    }

    public bool CanConfirm() => !string.IsNullOrWhiteSpace(DeviceName)
        && Fields.All(f => !f.HasError)
        && CommonFields.All(f => !f.HasError);

    public bool CanCloseDialog() => true;

    public void OnDialogClosed()
    {
    }

    public void OnDialogOpened(IDialogParameters parameters)
    {
        if (parameters.TryGetValue<ProtocolType>("protocol", out var protocol))
        {
            _selectedProtocol = protocol;
        }

        RebuildParam();
        DeviceName = SuggestName();
        RaiseAllChanged();
    }

    private string SuggestName() =>
        $"{SelectedProtocolInfo.DisplayName}-{(SelectedRole == DeviceRole.Client ? "Client" : "Server")}";

    public void Confirm()
    {
        if (!CanConfirm())
        {
            return;
        }

        foreach (var field in Fields.Concat(CommonFields))
        {
            field.Commit();
        }

        _param.DeviceName = DeviceName.Trim();
        _param.DeviceId = Guid.NewGuid().ToString("N");
        RequestClose.Invoke(new DialogParameters
        {
            { "param", _param },
        }, ButtonResult.OK);
    }

    public void Cancel() => RequestClose.Invoke(ButtonResult.Cancel);

    // ===== 命令（按钮绑定必须是 ICommand，绑定普通方法会静默失败）=====

    public ICommand ConfirmCommand => new RelayCommand(_ => Confirm());

    public ICommand CancelCommand => new RelayCommand(_ => Cancel());

    private void RebuildParam()
    {
        var protocol = SelectedProtocolInfo;
        if (!protocol.SupportsServer && _selectedRole == DeviceRole.Server)
        {
            _selectedRole = DeviceRole.Client;
        }

        _param = ProtocolCatalog.CreateParam(_selectedProtocol, _selectedRole);
        _param.DeviceName = DeviceName;

        Fields.Clear();
        CommonFields.Clear();
        foreach (var property in EditableProperties(_param))
        {
            var field = new ParamField(property, _param)
            {
                Options = property.PropertyType.IsEnum
                    ? Enum.GetNames(property.PropertyType)
                    : property.Name == nameof(CommunicationNew.Protocols.Serial.SerialParam.PortName)
                        ? SerialPortNames.ToList()
                        : null,
                IsEditableCombo = property.Name == nameof(CommunicationNew.Protocols.Serial.SerialParam.PortName),
            };
            Fields.Add(field);
        }
        foreach (var property in CommonProperties())
        {
            var value = property.GetValue(_param);
            var field = new ParamField(property, _param)
            {
                Options = property.PropertyType.IsEnum
                    ? Enum.GetNames(property.PropertyType)
                    : null,
            };
            _ = value;
            CommonFields.Add(field);
        }
    }

    /// <summary>基类通用参数（超时/重试/重连/队列容量），全部走 CommunicationNew 基类语义。</summary>
    private static IEnumerable<PropertyInfo> CommonProperties() =>
        typeof(CommunicationParam).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.Name is
                nameof(CommunicationParam.TimeoutMs) or
                nameof(CommunicationParam.RetryCount) or
                nameof(CommunicationParam.RetryDelayMs) or
                nameof(CommunicationParam.AutoReconnect) or
                nameof(CommunicationParam.ReconnectIntervalMs) or
                nameof(CommunicationParam.EventQueueCapacity) or
                nameof(CommunicationParam.RequestQueueCapacity) or
                nameof(CommunicationParam.ServerRequestCapacity));

    /// <summary>排除基类公共字段（超时/重试等在通用区域单独编辑）、[Obsolete] 兼容别名与集合/复杂类型。</summary>
    private static IEnumerable<PropertyInfo> EditableProperties(CommunicationParam param)
    {
        var baseProps = typeof(CommunicationParam).GetProperties().Select(p => p.Name).ToHashSet();
        return param.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !baseProps.Contains(p.Name) && p.CanWrite)
            .Where(p => p.GetCustomAttribute<ObsoleteAttribute>() is null)
            .Where(p => IsEditableType(p.PropertyType));
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

    private void RaiseAllChanged()
    {
        OnPropertyChanged(nameof(SelectedProtocol));
        OnPropertyChanged(nameof(SelectedProtocolIndex));
        OnPropertyChanged(nameof(SelectedRoleIndex));
        OnPropertyChanged(nameof(RoleServerEnabled));
        OnPropertyChanged(nameof(AddressHint));
        OnPropertyChanged(nameof(DeviceName));
    }

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>角色选项显示。</summary>
public record RoleOption(DeviceRole Role, string DisplayName);
