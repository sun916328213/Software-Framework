// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：NamespaceMigrationTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Controls.Services、Communication.Core、Communication.Core.Protocols.Socket、System.IO、System.Text.Json、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Controls.Services;
using Communication.Core;
using Communication.Core.Protocols.Socket;
using System.IO;
using System.Text.Json;
using Xunit;

namespace Communication.Workbench.Tests;

/// <summary>
/// NamespaceMigrationTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class NamespaceMigrationTests
{
    /// <summary>
    /// 测试相关入口 RemovedHttpCapacityAliasMigratesOnlyWhenCurrentFieldIsAbsent；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetTempFileName、Serialize、WriteAllText、Load、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData(false, 7)]
    [InlineData(true, 11)]
    public void RemovedHttpCapacityAliasMigratesOnlyWhenCurrentFieldIsAbsent(bool includeCurrent, int expected)
    {
        var path = Path.GetTempFileName();
        try
        {
            var parameterJson = includeCurrent ? "{\"MaxConnections\":7,\"ServerRequestCapacity\":11}" : "{\"MaxConnections\":7}";
            var document = JsonSerializer.Serialize(new[] { new
            {
                ParamType = typeof(Communication.Core.Protocols.Http.HttpParam).AssemblyQualifiedName!,
                Json = parameterJson
            } });
            File.WriteAllText(path, document);
            var (parameters, skipped) = DeviceConfigStore.Load(path);
            Assert.Equal(0, skipped);
            Assert.Equal(expected, Assert.IsType<Communication.Core.Protocols.Http.HttpParam>(Assert.Single(parameters)).ServerRequestCapacity);
            Assert.Equal(document, File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// 测试相关入口 LegacyConfigurationLoadsWithoutRewritingTheSourceFile；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetTempFileName、Serialize、WriteAllText、Load、IsType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LegacyConfigurationLoadsWithoutRewritingTheSourceFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            var original = new SocketParam { DeviceId = "legacy-id", DeviceName = "saved-device", ServerIP = "192.0.2.10", Port = 4321, ProtocolType = ProtocolType.Tcp };
            var document = JsonSerializer.Serialize(new[] { new
            {
                ParamType = "CommunicationNew.Protocols.Socket.SocketParam, Communication.Core",
                Json = JsonSerializer.Serialize(original)
            } });
            File.WriteAllText(path, document);
            var (parameters, skipped) = DeviceConfigStore.Load(path);
            var restored = Assert.IsType<SocketParam>(Assert.Single(parameters));
            Assert.Equal(0, skipped);
            Assert.Equal(original.DeviceId, restored.DeviceId);
            Assert.Equal(original.ServerIP, restored.ServerIP);
            Assert.Equal(original.Port, restored.Port);
            Assert.Equal(document, File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// 测试相关入口 UnknownLegacyConfigurationIsReportedAsSkipped；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetTempFileName、WriteAllText、Load、Empty、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void UnknownLegacyConfigurationIsReportedAsSkipped()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "[{\"ParamType\":\"CommunicationNew.UnknownParam, Communication.Core\",\"Json\":\"{}\"}]");
            var (parameters, skipped) = DeviceConfigStore.Load(path);
            Assert.Empty(parameters);
            Assert.Equal(1, skipped);
        }
        finally { File.Delete(path); }
    }
}
