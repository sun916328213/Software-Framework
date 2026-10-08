// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SecsGemOptionsTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Tests、SecsGem.Core、Xunit。导入不代表所有类型都会在运行时加载。
using SecsGem.Tests;
using SecsGem.Core;
using Xunit;

namespace SecsGem.Tests.Core;

/// <summary>
/// SecsGemOptionsTests 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class SecsGemOptionsTests
{
    /// <summary>
    /// 测试相关入口 Default_Hsms_Options_Are_Valid；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Validate；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Default_Hsms_Options_Are_Valid()
    {
        new SecsGemOptions().Validate();
    }

    /// <summary>
    /// 测试相关入口 Port_Outside_Tcp_Range_Is_Rejected；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Validate；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Port_Outside_Tcp_Range_Is_Rejected(int port)
    {
        var options = new SecsGemOptions { Port = port };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// 测试相关入口 Device_Id_Outside_Hsms_Range_Is_Rejected；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Validate；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(32768)]
    public void Device_Id_Outside_Hsms_Range_Is_Rejected(int deviceId)
    {
        var options = new SecsGemOptions { DeviceId = deviceId };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// 测试相关入口 Empty_Address_Is_Rejected；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Validate；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Empty_Address_Is_Rejected()
    {
        var options = new SecsGemOptions { IpAddress = " " };

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    /// <summary>
    /// 测试相关入口 Nonpositive_Buffer_Size_Is_Rejected；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Validate；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Nonpositive_Buffer_Size_Is_Rejected()
    {
        var options = new SecsGemOptions { BufferSize = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }
}
