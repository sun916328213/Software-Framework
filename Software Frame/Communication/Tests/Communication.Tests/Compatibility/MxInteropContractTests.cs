using System.Reflection;
using System.Runtime.InteropServices;
using Communication.Runtime.Protocols.Mitsubishi;
using Xunit;

namespace Communication.Tests.Compatibility;

public sealed class MxInteropContractTests
{
    [Theory]
    [InlineData("_control", "ActUtlType64Lib", "ActUtlType64")]
    [InlineData("_supportMessages", "ActSupportMsg64Lib", "ActSupportMsg64")]
    public void EmbeddedInterfacesPreserveVendorComIdentityAndMethods(string field, string library, string name)
    {
        var embedded = typeof(MxComponentMcClientAdapter)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.FieldType;
        var vendor = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, $"Interop.{library}.dll"))
            .GetType($"{library}.{name}", throwOnError: true)!;

        Assert.True(embedded.IsInterface);
        Assert.NotNull(embedded.GetCustomAttribute<TypeIdentifierAttribute>());
        Assert.Equal(vendor.GUID, embedded.GUID);
        var vendorMethods = vendor.GetInterfaces().Append(vendor).SelectMany(type => type.GetMethods()).ToArray();
        var embeddedMethods = embedded.GetInterfaces().Append(embedded).SelectMany(type => type.GetMethods()).ToArray();
        var required = field == "_control"
            ? new[] { "set_ActLogicalStationNumber", "set_ActPassword", "Open", "Close", "ReadDeviceBlock2", "WriteDeviceBlock2", "SetDevice2" }
            : new[] { "GetErrorMessage" };
        foreach (var nameOfMethod in required)
        {
            var method = Assert.Single(vendorMethods, method => method.Name == nameOfMethod);
            var actual = Assert.Single(embeddedMethods, method => method.Name == nameOfMethod);
            Assert.Equal(method.ReturnType.FullName, actual.ReturnType.FullName);
            Assert.Equal(method.GetParameters().Select(p => p.ParameterType.FullName),
                actual.GetParameters().Select(p => p.ParameterType.FullName));
            Assert.Equal(method.GetCustomAttribute<DispIdAttribute>()?.Value,
                actual.GetCustomAttribute<DispIdAttribute>()?.Value);
        }
        Assert.DoesNotContain(typeof(MxComponentMcClientAdapter).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name!.StartsWith("Interop.Act", StringComparison.Ordinal));
    }
}
