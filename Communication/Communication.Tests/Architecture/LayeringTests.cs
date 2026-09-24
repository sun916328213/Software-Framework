using System.Reflection;
using CommunicationNew.Abstractions;
using CommunicationNew.Core;
using Xunit;

namespace CommunicationNew.Tests.Architecture;

public sealed class LayeringTests
{
    private static readonly Assembly CommunicationAssembly = typeof(IConnection).Assembly;

    [Theory]
    [InlineData("CommunicationNew.Abstractions", "CommunicationNew.Abstractions")]
    [InlineData("CommunicationNew.Core", "CommunicationNew.Abstractions", "CommunicationNew.Core")]
    [InlineData("CommunicationNew.Transports", "CommunicationNew.Abstractions", "CommunicationNew.Transports")]
    [InlineData("CommunicationNew.Runtime", "CommunicationNew.Abstractions", "CommunicationNew.Transports", "CommunicationNew.Runtime")]
    public void Public_layer_surface_only_references_allowed_internal_namespaces(
        string layerNamespace,
        params string[] allowedNamespaces)
    {
        var violations = CommunicationAssembly.ExportedTypes
            .Where(type => IsInNamespace(type, layerNamespace))
            // 带 Obsolete 的旧类型由专门兼容测试管理，不扩散到当前分层接口。
            .Where(type => type.GetCustomAttribute<ObsoleteAttribute>() is null)
            .SelectMany(GetPublicSurfaceTypes)
            .Where(type => type.Assembly == CommunicationAssembly)
            .Where(type => !allowedNamespaces.Any(allowed => IsInNamespace(type, allowed)))
            .Select(type => type.FullName)
            .Distinct()
            .OrderBy(name => name)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Core_factory_seam_does_not_expose_composition_or_protocol_types()
    {
        var violations = new[]
            {
                typeof(ICommunicationDeviceFactory),
                typeof(CommunicationManager),
            }
            .SelectMany(GetPublicSurfaceTypes)
            .Where(type => type.Assembly == CommunicationAssembly)
            .Where(type => !IsInNamespace(type, "CommunicationNew.Abstractions"))
            .Where(type => !IsInNamespace(type, "CommunicationNew.Core"))
            .Select(type => type.FullName)
            .Distinct()
            .OrderBy(name => name)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Legacy_default_factory_paths_are_explicitly_isolated()
    {
        var injectedConstructor = typeof(CommunicationManager).GetConstructor(
            new[] { typeof(ICommunicationDeviceFactory) });
        var parameterlessConstructor = typeof(CommunicationManager).GetConstructor(Type.EmptyTypes);

        Assert.NotNull(injectedConstructor);
        Assert.False(injectedConstructor.GetParameters()[0].IsOptional);
        Assert.NotNull(parameterlessConstructor);
        Assert.NotNull(parameterlessConstructor.GetCustomAttribute<ObsoleteAttribute>());
        var legacyFactory = CommunicationAssembly.GetType(
            "CommunicationNew.Core.CommunicationFactory",
            throwOnError: true)!;
        Assert.NotNull(legacyFactory.GetCustomAttribute<ObsoleteAttribute>());
    }

    [Fact]
    public void Legacy_composition_factory_is_only_an_obsolete_core_alias()
    {
        var legacyFactory = CommunicationAssembly.GetType(
            "CommunicationNew.Composition.ICommunicationDeviceFactory",
            throwOnError: true)!;

        Assert.True(typeof(ICommunicationDeviceFactory).IsAssignableFrom(legacyFactory));
        Assert.Empty(legacyFactory.GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.NotNull(legacyFactory.GetCustomAttribute<ObsoleteAttribute>());
    }

    [Fact]
    public void Default_composition_entry_returns_core_factory_seam()
    {
        var defaults = CommunicationAssembly.GetType(
            "CommunicationNew.Composition.CommunicationDefaults",
            throwOnError: true)!;
        var currentEntry = defaults.GetMethod("CreateDefaultFactory", Type.EmptyTypes);
        var legacyEntry = defaults.GetMethod("CreateFactory", Type.EmptyTypes);

        Assert.NotNull(currentEntry);
        Assert.Equal(typeof(ICommunicationDeviceFactory), currentEntry.ReturnType);
        Assert.NotNull(legacyEntry);
        Assert.NotNull(legacyEntry.GetCustomAttribute<ObsoleteAttribute>());
    }

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

    [Fact]
    public void Abstractions_public_surface_does_not_expose_third_party_assemblies()
    {
        var violations = CommunicationAssembly.ExportedTypes
            .Where(type => IsInNamespace(type, "CommunicationNew.Abstractions"))
            .SelectMany(GetPublicSurfaceTypes)
            .Where(type => type.Assembly != CommunicationAssembly)
            .Where(type => !IsAllowedFrameworkType(type))
            .Select(type => $"{type.FullName} ({type.Assembly.GetName().Name})")
            .Distinct()
            .OrderBy(name => name)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Protocols_public_surface_does_not_expose_third_party_sdk_types()
    {
        var violations = CommunicationAssembly.ExportedTypes
            .Where(type => IsInNamespace(type, "CommunicationNew.Protocols"))
            .SelectMany(GetPublicSurfaceTypes)
            .Where(type => type.Assembly != CommunicationAssembly)
            .Where(type => !IsAllowedFrameworkType(type))
            .Select(type => $"{type.FullName} ({type.Assembly.GetName().Name})")
            .Distinct()
            .OrderBy(name => name)
            .ToArray();

        // 协议 SDK 只能停留在 internal adapter 内，公共设备 API 不应迫使调用方引用具体 SDK。
        Assert.Empty(violations);
    }

    [Fact]
    public void Built_in_protocol_devices_use_the_new_connection_seam()
    {
        var violations = CommunicationAssembly.ExportedTypes
            .Where(type => IsInNamespace(type, "CommunicationNew.Protocols"))
            .Where(type => !type.IsAbstract)
            .Where(type => typeof(CommunicationDeviceBase).IsAssignableFrom(type))
            .Where(type => !typeof(IConnection).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .OrderBy(name => name)
            .ToArray();

        // 新协议设备必须进入小型生命周期 seam，不能只依赖冻结的旧设备接口。
        Assert.Empty(violations);
    }

    [Fact]
    public void Address_capabilities_are_exposed_only_by_devices_with_real_address_semantics()
    {
        string[] expectedCapabilities =
        [
            "CommunicationNew.Protocols.Http.HttpClientDevice",
            "CommunicationNew.Protocols.Mitsubishi.McClientDevice",
            "CommunicationNew.Protocols.Modbus.ModbusRtu.ModbusRtuClientDevice",
            "CommunicationNew.Protocols.Modbus.ModbusRtu.ModbusRtuServerDevice",
            "CommunicationNew.Protocols.Modbus.ModbusTcp.ModbusTcpClientDevice",
            "CommunicationNew.Protocols.Modbus.ModbusTcp.ModbusTcpServerDevice",
            "CommunicationNew.Protocols.OpcUa.OpcUaClientDevice",
            "CommunicationNew.Protocols.OpcUa.OpcUaServerDevice",
            "CommunicationNew.Protocols.WebApi.WebApiClientDevice",
        ];

        var protocolDevices = CommunicationAssembly.ExportedTypes
            .Where(type => IsInNamespace(type, "CommunicationNew.Protocols"))
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

    [Fact]
    public void Raw_transport_capabilities_are_exposed_only_by_matching_socket_and_serial_devices()
    {
        string[] expectedRawSenders =
        [
            "CommunicationNew.Protocols.Serial.SerialPortDevice",
            "CommunicationNew.Protocols.Socket.TcpClientDevice",
            "CommunicationNew.Protocols.Socket.TcpServerDevice",
            "CommunicationNew.Protocols.Socket.UdpClientDevice",
            "CommunicationNew.Protocols.Socket.UdpServerDevice",
        ];
        string[] expectedRequestReplyClients =
        [
            "CommunicationNew.Protocols.Serial.SerialPortDevice",
            "CommunicationNew.Protocols.Socket.TcpClientDevice",
            "CommunicationNew.Protocols.Socket.UdpClientDevice",
        ];

        var protocolDevices = CommunicationAssembly.ExportedTypes
            .Where(type => IsInNamespace(type, "CommunicationNew.Protocols"))
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

    private static bool IsInNamespace(Type type, string expectedNamespace) =>
        type.Namespace == expectedNamespace ||
        type.Namespace?.StartsWith(expectedNamespace + ".", StringComparison.Ordinal) == true;

    private static bool IsAllowedFrameworkType(Type type)
    {
        var assemblyName = type.Assembly.GetName().Name ?? "";
        return assemblyName is "netstandard" or "mscorlib" or "Microsoft.Extensions.Logging.Abstractions" ||
            assemblyName.StartsWith("System", StringComparison.Ordinal);
    }

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
