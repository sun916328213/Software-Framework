// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ProviderFactoryCompositionTests、Row、FactoryProbeException、SqlFactory、LogFactory、ExcelFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls、DataManager.Core.Excel、DataManager.Core.Excel.Definition、DataManager.Core.Log、DataManager.Core.Log.Definition、DataManager.Core.SQL。导入不代表所有类型都会在运行时加载。
using DataManager.Controls;
using DataManager.Core.Excel;
using DataManager.Core.Excel.Definition;
using DataManager.Core.Log;
using DataManager.Core.Log.Definition;
using DataManager.Core.SQL;
using DataManager.Core.SQL.Definition;
using Prism.DryIoc;
using Prism.Ioc;

namespace DataManager.Workbench.Tests;

/// <summary>
/// ProviderFactoryCompositionTests 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ProviderFactoryCompositionTests
{
    /// <summary>
    /// 验证 HostProviderFactoriesReachDefaultManagersAfterRepeatedRegistration 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterInstance、RegisterTypes、Resolve、Same、Throws；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void HostProviderFactoriesReachDefaultManagersAfterRepeatedRegistration()
    {
        var container = new DryIocContainerExtension();
        var sql = new SqlFactory();
        var log = new LogFactory();
        var excel = new ExcelFactory();
        container.RegisterInstance<ISqlProviderFactory>(sql);
        container.RegisterInstance<ILogProviderFactory>(log);
        container.RegisterInstance<IExcelProviderFactory>(excel);
        var module = new DataManagerModule();
        module.RegisterTypes(container);
        var manager = container.Resolve<ISQLManager>();
        module.RegisterTypes(container);
        Assert.Same(manager, container.Resolve<ISQLManager>());
        Assert.Same(sql, container.Resolve<ISqlProviderFactory>());
        Assert.Same(log, container.Resolve<ILogProviderFactory>());
        Assert.Same(excel, container.Resolve<IExcelProviderFactory>());
        Assert.Throws<FactoryProbeException>(() => manager.Create<Row>(new SQLParam
            { Name = "probe", ConnectionString = "Data Source=:memory:" }));
        Assert.Throws<FactoryProbeException>(() => container.Resolve<ILogManager>().Create(new LogParam { Name = "probe" }));
        Assert.Throws<FactoryProbeException>(() => container.Resolve<IExcelManager>().Create(new ExcelParam
            { Name = "probe", FilePath = ".", FileName = "probe.xlsx" }));
        Assert.Equal(1, sql.Calls);
        Assert.Equal(1, log.Calls);
        Assert.Equal(1, excel.Calls);
    }

    /// <summary>
    /// 验证 DefaultProviderFactoriesBelongToTheirContainers 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterTypes、NotSame、Resolve；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DefaultProviderFactoriesBelongToTheirContainers()
    {
        var first = new DryIocContainerExtension();
        var second = new DryIocContainerExtension();
        new DataManagerModule().RegisterTypes(first);
        new DataManagerModule().RegisterTypes(second);
        Assert.NotSame(first.Resolve<ISqlProviderFactory>(), second.Resolve<ISqlProviderFactory>());
        Assert.NotSame(first.Resolve<ILogProviderFactory>(), second.Resolve<ILogProviderFactory>());
        Assert.NotSame(first.Resolve<IExcelProviderFactory>(), second.Resolve<IExcelProviderFactory>());
    }

    /// <summary>
    /// Row 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// </summary>
    public sealed class Row;
    /// <summary>
    /// FactoryProbeException 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：Exception。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class FactoryProbeException : Exception;
    /// <summary>
    /// SqlFactory 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ISqlProviderFactory。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class SqlFactory : ISqlProviderFactory
    {
        public int Calls { get; private set; }
        /// <summary>
        /// 验证 Create 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public ISQL<T> Create<T>(SQLParam parameter) where T : class, new()
        { Calls++; throw new FactoryProbeException(); }
    }
    /// <summary>
    /// LogFactory 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ILogProviderFactory。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class LogFactory : ILogProviderFactory
    {
        public int Calls { get; private set; }
        /// <summary>
        /// 验证 Create 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public ILog Create(LogParam parameter) { Calls++; throw new FactoryProbeException(); }
    }
    /// <summary>
    /// ExcelFactory 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IExcelProviderFactory。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ExcelFactory : IExcelProviderFactory
    {
        public int Calls { get; private set; }
        /// <summary>
        /// 验证 Create 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public IExcel Create(ExcelParam parameter) { Calls++; throw new FactoryProbeException(); }
    }
}
