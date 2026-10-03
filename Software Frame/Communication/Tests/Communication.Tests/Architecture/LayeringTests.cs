// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LayeringTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Reflection、Communication.Core.Abstractions、Communication.Core、Communication.Runtime.Core、Xunit。导入不代表所有类型都会在运行时加载。
using System.Reflection;
using Communication.Core.Abstractions;
using Communication.Core;
using Communication.Runtime.Core;
using Xunit;

namespace Communication.Tests.Architecture;

/// <summary>
/// P11 物理四层守卫：Core / Runtime 已拆为独立程序集，旧单程序集假设按归属改写。
/// Core 程序集 = <see cref="IConnection"/> 所在；Runtime 程序集 = <see cref="CommunicationManager"/> 所在。
/// </summary>
public sealed class LayeringTests
{
    private static readonly Assembly CoreAssembly = typeof(IConnection).Assembly;
    private static readonly Assembly RuntimeAssembly = typeof(CommunicationManager).Assembly;

    /// <summary>
    /// 验证 Public_layer_surface_only_references_allowed_internal_namespaces 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToArray、OrderBy、Distinct、Select、Where；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("core", "Communication.Core.Abstractions", "Communication.Core.Abstractions")]
    [InlineData("core", "Communication.Core", "Communication.Core.Abstractions", "Communication.Core")]
    [InlineData("runtime", "Communication.Runtime.Transports", "Communication.Core.Abstractions", "Communication.Runtime.Transports", "Communication.Core", "Communication.Runtime")]
    [InlineData("runtime", "Communication.Runtime", "Communication.Core.Abstractions", "Communication.Runtime.Transports", "Communication.Runtime", "Communication.Core")]
    [InlineData("runtime", "Communication.Runtime.Protocols", "Communication.Runtime.Core", "Communication.Core.Abstractions", "Communication.Core", "Communication.Runtime.Transports", "Communication.Runtime", "Communication.Runtime.Protocols")]
    [InlineData("runtime", "Communication.Runtime.Composition", "Communication.Core.Abstractions", "Communication.Core", "Communication.Runtime.Composition", "Communication.Runtime.Protocols", "Communication.Runtime.Transports", "Communication.Runtime")]
    public void Public_layer_surface_only_references_allowed_internal_namespaces(
        string host,
        string layerNamespace,
        params string[] allowedNamespaces)
    {
        var hostAssembly = host == "core" ? CoreAssembly : RuntimeAssembly;
        var violations = hostAssembly.ExportedTypes
            .Where(type => IsInNamespace(type, layerNamespace))
            // 带 Obsolete 的旧类型由专门兼容测试管理，不扩散到当前分层接口。
            .Where(type => type.GetCustomAttribute<ObsoleteAttribute>() is null)
            // P11：Runtime 宿主的 Core 命名空间实现（Base/Manager/Factory/AutoReader）由专项测试约束。
            .Where(type => !(host == "runtime" && IsInNamespace(type, "Communication.Runtime.Core")))
            .SelectMany(GetPublicSurfaceTypes)
            .Where(type => type.Assembly == CoreAssembly || type.Assembly == RuntimeAssembly)
            .Where(type => !allowedNamespaces.Any(allowed => IsInNamespace(type, allowed)))
            .Select(type => type.FullName)
            .Distinct()
            .OrderBy(name => name)
            .ToArray();

        Assert.Empty(violations);
    }

    /// <summary>
    /// 验证 Core_factory_seam_does_not_expose_composition_or_protocol_types 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToArray、OrderBy、Distinct、Select、Where；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Core_factory_seam_does_not_expose_composition_or_protocol_types()
    {
        var violations = new[]
            {
                typeof(ICommunicationDeviceFactory),
                typeof(CommunicationManager),
            }
            .SelectMany(GetPublicSurfaceTypes)
            .Where(type => type.Assembly == CoreAssembly || type.Assembly == RuntimeAssembly)
            .Where(type => !IsInNamespace(type, "Communication.Core.Abstractions"))
            .Where(type => !IsInNamespace(type, "Communication.Core"))
            .Select(type => type.FullName)
            .Distinct()
            .OrderBy(name => name)
            .ToArray();

        Assert.Empty(violations);
    }

    /// <summary>
    /// 验证 Manager_requires_injected_core_factory_and_legacy_entries_are_absent 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetConstructor、NotNull、False、GetParameters、Null；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Manager_requires_injected_core_factory_and_legacy_entries_are_absent()
    {
        var constructor = typeof(CommunicationManager).GetConstructor(new[] { typeof(ICommunicationDeviceFactory) });
        Assert.NotNull(constructor);
        Assert.False(constructor.GetParameters()[0].IsOptional);
        Assert.Null(typeof(CommunicationManager).GetConstructor(Type.EmptyTypes));
        Assert.Null(RuntimeAssembly.GetType("Communication.Runtime.Core.CommunicationFactory"));
    }

    /// <summary>
    /// 验证 Legacy_composition_factory_interface_is_removed 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Null、GetType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Legacy_composition_factory_interface_is_removed()
    {
        Assert.Null(RuntimeAssembly.GetType("Communication.Runtime.Composition.ICommunicationDeviceFactory"));
    }

    /// <summary>
    /// 验证 Default_composition_entry_returns_only_core_factory_seam 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetMethod、Equal、Null；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Default_composition_entry_returns_only_core_factory_seam()
    {
        var current = typeof(Communication.Runtime.Composition.CommunicationDefaults).GetMethod("CreateDefaultFactory", Type.EmptyTypes);
        Assert.Equal(typeof(ICommunicationDeviceFactory), current!.ReturnType);
        Assert.Null(typeof(Communication.Runtime.Composition.CommunicationDefaults).GetMethod("CreateFactory", Type.EmptyTypes));
    }

    /// <summary>
    /// 验证 Legacy_device_interface_member_set_is_frozen 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、OrderBy、Select、GetProperties、GetEvents；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Legacy_device_interface_member_set_is_frozen()
    {
        var type = typeof(ICommunicationDevice);

        Assert.Equal(
            new[]
            {
                "DeviceId",
                "DeviceName",
                "IsConnected",
                "ProtocolType",
                "Role",
                "TimeoutMs",
            },
            type.GetProperties().Select(property => property.Name).OrderBy(name => name));
        Assert.Equal(
            new[] { "ConnectionStateChanged", "DataReceived", "ErrorOccurred" },
            type.GetEvents().Select(@event => @event.Name).OrderBy(name => name));
        Assert.Equal(
            new[]
            {
                "ConnectAsync",
                "DisconnectAsync",
                "ReadAsync",
                "SendAndReceiveAsync",
                "SendAsync",
                "WriteAsync",
            },
            type.GetMethods()
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .OrderBy(name => name));
    }

    /// <summary>
    /// 验证 Abstractions_public_surface_does_not_expose_third_party_assemblies 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToArray、OrderBy、Distinct、Select、Where；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Abstractions_public_surface_does_not_expose_third_party_assemblies()
    {
        var violations = CoreAssembly.ExportedTypes
            .Where(type => IsInNamespace(type, "Communication.Core.Abstractions"))
            .SelectMany(GetPublicSurfaceTypes)
            .Where(type => type.Assembly != CoreAssembly)
            .Where(type => !IsAllowedFrameworkType(type))
            .Select(type => $"{type.FullName} ({type.Assembly.GetName().Name})")
            .Distinct()
            .OrderBy(name => name)
            .ToArray();

        Assert.Empty(violations);
    }

    /// <summary>
    /// 验证 Core_public_surface_does_not_expose_third_party_sdk_types 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToArray、OrderBy、Distinct、Select、Where；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Core_public_surface_does_not_expose_third_party_sdk_types()
    {
        var violations = CoreAssembly.ExportedTypes
            .Where(type => IsInNamespace(type, "Communication.Core")
                || IsInNamespace(type, "Communication.Runtime.Protocols"))
            .Where(type => type.GetCustomAttribute<ObsoleteAttribute>() is null)
            .SelectMany(GetPublicSurfaceTypes)
            .Where(type => type.Assembly != CoreAssembly)
            .Where(type => !IsAllowedFrameworkType(type))
            .Select(type => $"{type.FullName} ({type.Assembly.GetName().Name})")
            .Distinct()
            .OrderBy(name => name)
            .ToArray();

        // Core（含参数 DTO）不得迫使调用方引用 FluentModbus / OPC UA / MX 等协议 SDK。
        Assert.Empty(violations);
    }

    /// <summary>
    /// 验证 Protocols_public_surface_does_not_expose_third_party_sdk_types 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToArray、OrderBy、Distinct、Select、Where；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Protocols_public_surface_does_not_expose_third_party_sdk_types()
    {
        var violations = RuntimeAssembly.ExportedTypes
            .Where(type => IsInNamespace(type, "Communication.Runtime.Protocols"))
            .SelectMany(GetPublicSurfaceTypes)
            .Where(type => type.Assembly != RuntimeAssembly)
            // P11：协议面引用 Core 契约（Abstractions/Core/参数 DTO）属第一方，不视为第三方 SDK 泄漏。
            .Where(type => !(type.Assembly == CoreAssembly
                && (IsInNamespace(type, "Communication.Core.Abstractions") || IsInNamespace(type, "Communication.Core") || IsInNamespace(type, "Communication.Runtime.Protocols"))))
            .Where(type => !IsAllowedFrameworkType(type))
            .Select(type => $"{type.FullName} ({type.Assembly.GetName().Name})")
            .Distinct()
            .OrderBy(name => name)
            .ToArray();

        // 协议 SDK 只能停留在 internal adapter 内，公共设备 API 不应迫使调用方引用具体 SDK。
        Assert.Empty(violations);
    }

    /// <summary>
    /// 验证 Built_in_protocol_devices_use_the_new_connection_seam 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToArray、OrderBy、Select、Where、IsInNamespace；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Built_in_protocol_devices_use_the_new_connection_seam()
    {
        var violations = RuntimeAssembly.ExportedTypes
            .Where(type => IsInNamespace(type, "Communication.Runtime.Protocols"))
            .Where(type => !type.IsAbstract)
            .Where(type => typeof(CommunicationDeviceBase).IsAssignableFrom(type))
            .Where(type => !typeof(IConnection).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .OrderBy(name => name)
            .ToArray();

        // 新协议设备必须进入小型生命周期 seam，不能只依赖冻结的旧设备接口。
        Assert.Empty(violations);
    }

    /// <summary>
    /// 验证 Address_capabilities_are_exposed_only_by_devices_with_real_address_semantics 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToArray、Where、IsInNamespace、IsAssignableFrom、OrderBy；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Address_capabilities_are_exposed_only_by_devices_with_real_address_semantics()
    {
        string[] expectedCapabilities =
        [
            "Communication.Runtime.Protocols.Http.HttpClientDevice",
            "Communication.Runtime.Protocols.Mitsubishi.McClientDevice",
            "Communication.Runtime.Protocols.Modbus.ModbusRtu.ModbusRtuClientDevice",
            "Communication.Runtime.Protocols.Modbus.ModbusRtu.ModbusRtuServerDevice",
            "Communication.Runtime.Protocols.Modbus.ModbusTcp.ModbusTcpClientDevice",
            "Communication.Runtime.Protocols.Modbus.ModbusTcp.ModbusTcpServerDevice",
            "Communication.Runtime.Protocols.OpcUa.OpcUaClientDevice",
            "Communication.Runtime.Protocols.OpcUa.OpcUaServerDevice",
            "Communication.Runtime.Protocols.WebApi.WebApiClientDevice",
        ];

        var protocolDevices = RuntimeAssembly.ExportedTypes
            .Where(type => IsInNamespace(type, "Communication.Runtime.Protocols"))
            .Where(type => !type.IsAbstract)
            .Where(type => typeof(CommunicationDeviceBase).IsAssignableFrom(type))
            .ToArray();

        var readers = protocolDevices
            .Where(type => typeof(IAddressReader).IsAssignableFrom(type))
            .Select(type => type.FullName!)
            .OrderBy(name => name)
            .ToArray();
        var writers = protocolDevices
            .Where(type => typeof(IAddressWriter).IsAssignableFrom(type))
            .Select(type => type.FullName!)
            .OrderBy(name => name)
            .ToArray();

        // 旧基类继续保留 ReadAsync/WriteAsync 成员，但不能再替所有协议宣称地址能力。
        Assert.False(typeof(IAddressReader).IsAssignableFrom(typeof(CommunicationDeviceBase)));
        Assert.False(typeof(IAddressWriter).IsAssignableFrom(typeof(CommunicationDeviceBase)));
        Assert.Equal(expectedCapabilities, readers);
        Assert.Equal(expectedCapabilities, writers);
    }

    /// <summary>
    /// 验证 Raw_transport_capabilities_are_exposed_only_by_matching_socket_and_serial_devices 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToArray、Where、IsInNamespace、IsAssignableFrom、OrderBy；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Raw_transport_capabilities_are_exposed_only_by_matching_socket_and_serial_devices()
    {
        string[] expectedRawSenders =
        [
            "Communication.Runtime.Protocols.Serial.SerialPortDevice",
            "Communication.Runtime.Protocols.Socket.TcpClientDevice",
            "Communication.Runtime.Protocols.Socket.TcpServerDevice",
            "Communication.Runtime.Protocols.Socket.UdpClientDevice",
            "Communication.Runtime.Protocols.Socket.UdpServerDevice",
        ];
        string[] expectedRequestReplyClients =
        [
            "Communication.Runtime.Protocols.Serial.SerialPortDevice",
            "Communication.Runtime.Protocols.Socket.TcpClientDevice",
            "Communication.Runtime.Protocols.Socket.UdpClientDevice",
        ];

        var protocolDevices = RuntimeAssembly.ExportedTypes
            .Where(type => IsInNamespace(type, "Communication.Runtime.Protocols"))
            .Where(type => !type.IsAbstract)
            .Where(type => typeof(CommunicationDeviceBase).IsAssignableFrom(type))
            .ToArray();

        var rawSenders = protocolDevices
            .Where(type => typeof(IRawSender).IsAssignableFrom(type))
            .Select(type => type.FullName!)
            .OrderBy(name => name)
            .ToArray();
        var requestReplyClients = protocolDevices
            .Where(type => typeof(IRequestReplyClient).IsAssignableFrom(type))
            .Select(type => type.FullName!)
            .OrderBy(name => name)
            .ToArray();
        var receiveSources = protocolDevices
            .Where(type => typeof(IReceiveSource<ReceivedPacket>).IsAssignableFrom(type))
            .Select(type => type.FullName!)
            .OrderBy(name => name)
            .ToArray();

        // HTTP 等高层协议即使保留旧字节入口，也不能被误识别为原始传输。
        Assert.Equal(expectedRawSenders, rawSenders);
        Assert.Equal(expectedRequestReplyClients, requestReplyClients);
        Assert.Equal(expectedRawSenders, receiveSources);
    }

    /// <summary>
    /// 验证 Core_assembly_does_not_reference_runtime_or_protocol_sdks 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToHashSet、Select、GetReferencedAssemblies、DoesNotContain；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Core_assembly_does_not_reference_runtime_or_protocol_sdks()
    {
        var references = CoreAssembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("Communication.Runtime", references);
        // 兼容门面（程序集名 CommunicationNew）已于 2026-09-29 删除；保留此断言防止 Core 反向依赖门面。
        Assert.DoesNotContain("CommunicationNew", references);
        Assert.DoesNotContain("FluentModbus", references);
        Assert.DoesNotContain("Opc.Ua.Client", references);
        Assert.DoesNotContain("Opc.Ua.Core", references);
        Assert.DoesNotContain("Opc.Ua.Server", references);
        Assert.DoesNotContain("Prism.Core", references);
        Assert.DoesNotContain("System.IO.Pipelines", references);
    }

    /// <summary>
    /// 验证 Runtime_assembly_references_core 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToHashSet、Select、GetReferencedAssemblies、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Runtime_assembly_references_core()
    {
        var references = RuntimeAssembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Communication.Core", references);
    }

    /// <summary>
    /// 验证 Runtime_hosts_only_known_core_namespace_implementations 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToArray、OrderBy、Select、Where、IsInNamespace；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Runtime_hosts_only_known_core_namespace_implementations()
    {
        var hosted = RuntimeAssembly.ExportedTypes
            .Where(type => IsInNamespace(type, "Communication.Runtime.Core"))
            .Select(type => type.FullName)
            .OrderBy(name => name)
            .ToArray();

        // P11：命名空间保持兼容（避免全仓改 using），但程序集归属收口到以下实现。
        Assert.Equal(
            new[]
            {
                "Communication.Runtime.Core.CommunicationDeviceBase",
                "Communication.Runtime.Core.CommunicationManager",
                "Communication.Runtime.Core.Services.DataPointAutoReader",
            },
            hosted);
    }

    /// <summary>
    /// 验证 Parameter_snapshot_seam_is_implemented_by_device_base 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 True、IsAssignableFrom、NotNull、GetMethod、nameof；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Parameter_snapshot_seam_is_implemented_by_device_base()
    {
        Assert.True(typeof(ICommunicationParameterSource).IsAssignableFrom(typeof(CommunicationDeviceBase)));
        Assert.NotNull(typeof(CommunicationDeviceBase).GetMethod(nameof(ICommunicationParameterSource.GetParameterSnapshot)));
    }

    /// <summary>
    /// 验证 Manager_implements_core_contract 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 True、IsAssignableFrom；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Manager_implements_core_contract()
    {
        Assert.True(typeof(ICommunicationManager).IsAssignableFrom(typeof(CommunicationManager)));
    }

    /// <summary>
    /// 验证 IsInNamespace 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
    private static bool IsInNamespace(Type type, string expectedNamespace) =>
        type.Namespace == expectedNamespace ||
        type.Namespace?.StartsWith(expectedNamespace + ".", StringComparison.Ordinal) == true;

    /// <summary>
    /// 验证 IsAllowedFrameworkType 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetName、StartsWith；具体条件和先后次序见方法体。
    /// </summary>
    private static bool IsAllowedFrameworkType(Type type)
    {
        var assemblyName = type.Assembly.GetName().Name ?? "";
        return assemblyName is "netstandard" or "mscorlib" or "Microsoft.Extensions.Logging.Abstractions" ||
            assemblyName.StartsWith("System", StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 GetPublicSurfaceTypes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Expand、GetInterfaces、Where、GetConstructors、GetCustomAttribute；具体条件和先后次序见方法体。
    /// </summary>
    private static IEnumerable<Type> GetPublicSurfaceTypes(Type type)
    {
        if (type.BaseType is not null)
        {
            foreach (var referencedType in Expand(type.BaseType))
            {
                yield return referencedType;
            }
        }

        foreach (var interfaceType in type.GetInterfaces())
        {
            foreach (var referencedType in Expand(interfaceType))
            {
                yield return referencedType;
            }
        }

        foreach (var constructor in type.GetConstructors()
            .Where(member => member.GetCustomAttribute<ObsoleteAttribute>() is null))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                foreach (var referencedType in Expand(parameter.ParameterType))
                {
                    yield return referencedType;
                }
            }
        }

        foreach (var field in type.GetFields()
            .Where(member => member.GetCustomAttribute<ObsoleteAttribute>() is null))
        {
            foreach (var referencedType in Expand(field.FieldType))
            {
                yield return referencedType;
            }
        }

        foreach (var property in type.GetProperties()
            .Where(member => member.GetCustomAttribute<ObsoleteAttribute>() is null))
        {
            foreach (var referencedType in Expand(property.PropertyType))
            {
                yield return referencedType;
            }
        }

        foreach (var eventInfo in type.GetEvents()
            .Where(member => member.GetCustomAttribute<ObsoleteAttribute>() is null))
        {
            foreach (var referencedType in Expand(eventInfo.EventHandlerType!))
            {
                yield return referencedType;
            }
        }

        foreach (var method in type.GetMethods()
            .Where(method => !method.IsSpecialName)
            .Where(member => member.GetCustomAttribute<ObsoleteAttribute>() is null))
        {
            foreach (var referencedType in Expand(method.ReturnType))
            {
                yield return referencedType;
            }
            foreach (var parameter in method.GetParameters())
            {
                foreach (var referencedType in Expand(parameter.ParameterType))
                {
                    yield return referencedType;
                }
            }
        }
    }

    /// <summary>
    /// 验证 Expand 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Expand、GetElementType、GetGenericTypeDefinition、GetGenericArguments；具体条件和先后次序见方法体。
    /// </summary>
    private static IEnumerable<Type> Expand(Type type)
    {
        if (type.IsByRef || type.IsArray || type.IsPointer)
        {
            foreach (var elementType in Expand(type.GetElementType()!))
            {
                yield return elementType;
            }
            yield break;
        }

        if (type.IsGenericParameter)
        {
            yield break;
        }

        yield return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        if (!type.IsGenericType)
        {
            yield break;
        }

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var referencedType in Expand(argument))
            {
                yield return referencedType;
            }
        }
    }
}
