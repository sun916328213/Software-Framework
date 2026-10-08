// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：DataPointDefinitionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Interfaces、Communication.Core.Models、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core.Interfaces;
using Communication.Core.Models;
using Xunit;

namespace Communication.Tests.Core;

/// <summary>
/// DataPointDefinitionTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class DataPointDefinitionTests
{
    /// <summary>
    /// 测试相关入口 Zero_length_uses_type_default_for_both_materialization_paths；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ToDataPoint、AddDefinition、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Zero_length_uses_type_default_for_both_materialization_paths()
    {
        var definition = new DataPointDefinition
        {
            Name = "Counter",
            Address = "HR1",
            DataType = DataPointType.Int32,
            LengthBytes = 0,
        };

        var standalone = definition.ToDataPoint();
        var collection = new DataPointCollection();
        var added = collection.AddDefinition(definition);

        Assert.Equal(4, standalone.Length);
        Assert.Equal(4, added.Length);
    }

    /// <summary>
    /// 测试相关入口 Negative_length_is_rejected_when_definition_is_materialized；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、ToDataPoint；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Negative_length_is_rejected_when_definition_is_materialized()
    {
        var definition = new DataPointDefinition
        {
            Name = "Counter",
            Address = "HR1",
            DataType = DataPointType.Int32,
            LengthBytes = -1,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => definition.ToDataPoint());
    }

    /// <summary>
    /// 测试相关入口 Unsupported_data_type_is_rejected_even_when_length_is_configured；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、ToDataPoint；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Unsupported_data_type_is_rejected_even_when_length_is_configured()
    {
        var definition = new DataPointDefinition
        {
            Name = "Unknown",
            Address = "HR1",
            DataType = (DataPointType)int.MaxValue,
            LengthBytes = 8,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => definition.ToDataPoint());
    }

    /// <summary>
    /// 测试相关入口 Fixed_width_data_type_rejects_length_shorter_than_its_value；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、ToDataPoint；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Fixed_width_data_type_rejects_length_shorter_than_its_value()
    {
        var definition = new DataPointDefinition
        {
            Name = "Counter",
            Address = "HR1",
            DataType = DataPointType.Int32,
            LengthBytes = 2,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => definition.ToDataPoint());
    }

    /// <summary>
    /// 测试相关入口 Blank_name_is_rejected_when_definition_is_materialized；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、ToDataPoint；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Blank_name_is_rejected_when_definition_is_materialized()
    {
        var definition = new DataPointDefinition
        {
            Name = " ",
            Address = "HR1",
        };

        Assert.Throws<ArgumentException>(() => definition.ToDataPoint());
    }

    /// <summary>
    /// 测试相关入口 Blank_address_is_rejected_when_definition_is_materialized；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、ToDataPoint；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Blank_address_is_rejected_when_definition_is_materialized()
    {
        var definition = new DataPointDefinition
        {
            Name = "Counter",
            Address = " ",
        };

        Assert.Throws<ArgumentException>(() => definition.ToDataPoint());
    }
}
