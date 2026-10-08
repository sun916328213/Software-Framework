// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：DeviceValueTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Interfaces、Communication.Core.Models、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core.Interfaces;
using Communication.Core.Models;
using Xunit;

namespace Communication.Tests.Core;

/// <summary>
/// DeviceValueTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class DeviceValueTests
{
    /// <summary>
    /// 测试相关入口 Failed_conversion_is_reported_instead_of_returning_default；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、GetValue、Contains、NotNull；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Failed_conversion_is_reported_instead_of_returning_default()
    {
        var value = new DeviceValue("not-a-number", DataPointType.String);

        var exception = Assert.Throws<InvalidCastException>(() => value.GetValue<int>());

        Assert.Contains(typeof(string).FullName!, exception.Message);
        Assert.Contains(typeof(int).FullName!, exception.Message);
        Assert.NotNull(exception.InnerException);
    }
}
