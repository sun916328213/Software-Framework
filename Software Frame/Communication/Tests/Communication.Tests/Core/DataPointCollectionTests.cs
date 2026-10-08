// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：DataPointCollectionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Interfaces、Communication.Core.Models、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core.Interfaces;
using Communication.Core.Models;
using Xunit;

namespace Communication.Tests.Core;

/// <summary>
/// DataPointCollectionTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class DataPointCollectionTests
{
    /// <summary>
    /// 测试相关入口 AddRange_failure_does_not_partially_change_the_collection；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Add、Throws、AddRange、Equal、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AddRange_failure_does_not_partially_change_the_collection()
    {
        var collection = new DataPointCollection();
        collection.Add(new DataPoint("Existing", DataPointType.Int16, "HR1"));
        var points = new IDataPoint[]
        {
            new DataPoint("New", DataPointType.Int16, "HR2"),
            new DataPoint("Existing", DataPointType.Int16, "HR3"),
        };

        Assert.Throws<ArgumentException>(() => collection.AddRange(points));

        Assert.Equal(1, collection.Count);
        Assert.False(collection.Contains("New"));
    }

    /// <summary>
    /// 测试相关入口 AddDefinitions_failure_does_not_partially_change_the_collection；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Add、Throws、AddDefinitions、Equal、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AddDefinitions_failure_does_not_partially_change_the_collection()
    {
        var collection = new DataPointCollection();
        collection.Add(new DataPoint("Existing", DataPointType.Int16, "HR1"));
        var definitions = new[]
        {
            new DataPointDefinition { Name = "New", Address = "HR2" },
            new DataPointDefinition { Name = "Existing", Address = "HR3" },
        };

        Assert.Throws<ArgumentException>(() => collection.AddDefinitions(definitions));

        Assert.Equal(1, collection.Count);
        Assert.False(collection.Contains("New"));
    }

    /// <summary>
    /// 测试相关入口 All_does_not_allow_bypassing_collection_invariants；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Add、IsAssignableFrom、Throws、Equal、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void All_does_not_allow_bypassing_collection_invariants()
    {
        var collection = new DataPointCollection();
        collection.Add(new DataPoint("Existing", DataPointType.Int16, "HR1"));
        var exposed = Assert.IsAssignableFrom<ICollection<IDataPoint>>(collection.All);

        Assert.Throws<NotSupportedException>(() =>
            exposed.Add(new DataPoint("Injected", DataPointType.Int16, "HR2")));

        Assert.Equal(1, collection.Count);
        Assert.False(collection.Contains("Injected"));
    }
}
