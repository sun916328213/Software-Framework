// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LayeringTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、System.Reflection、System.Text.RegularExpressions、System.Xml.Linq、DataManager.Core.Excel、DataManager.Runtime.Excel。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DataManager.Core.Excel;
using DataManager.Runtime.Excel;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;

namespace DataManager.Tests.Architecture;

/// <summary>
/// P12 物理四层守卫：Core / Runtime 已拆为独立程序集。
/// Core 程序集 = <see cref="ISQL"/> 所在；Runtime 程序集 = <see cref="SQLManager"/> 所在。
/// </summary>
public sealed class LayeringTests
{
    private static readonly Assembly CoreAssembly = typeof(ISQL).Assembly;
    private static readonly Assembly RuntimeAssembly = typeof(SQLManager).Assembly;

    /// <summary>
    /// 验证 Core_assembly_does_not_reference_runtime_or_provider_packages 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToHashSet、Select、GetReferencedAssemblies、DoesNotContain；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Core_assembly_does_not_reference_runtime_or_provider_packages()
    {
        var references = CoreAssembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("DataManager.Runtime", references);
        // 兼容门面（程序集名 DataManager）已于 2026-09-29 删除；保留此断言防止 Core 反向依赖门面。
        Assert.DoesNotContain("DataManager", references);
        Assert.DoesNotContain("Dapper", references);
        Assert.DoesNotContain("Dommel", references);
        Assert.DoesNotContain("SqlSugarCore", references);
        Assert.DoesNotContain("SqlSugar", references);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", references);
        Assert.DoesNotContain("EPPlus", references);
        Assert.DoesNotContain("MiniExcel", references);
        Assert.DoesNotContain("Serilog", references);
        Assert.DoesNotContain("NLog", references);
        Assert.DoesNotContain("log4net", references);
        Assert.DoesNotContain("Npgsql", references);
        Assert.DoesNotContain("MySqlConnector", references);
        Assert.DoesNotContain("Microsoft.Data.SqlClient", references);
    }

    /// <summary>
    /// 验证 Core_public_surface_does_not_expose_provider_types 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToArray、OrderBy、Distinct、Select、Where；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Core_public_surface_does_not_expose_provider_types()
    {
        var violations = CoreAssembly.ExportedTypes
            .SelectMany(GetPublicSurfaceTypes)
            .Where(type => type.Assembly != CoreAssembly)
            .Where(type => !IsAllowedFrameworkType(type))
            .Select(type => $"{type.FullName} ({type.Assembly.GetName().Name})")
            .Distinct()
            .OrderBy(name => name)
            .ToArray();

        // Core（含参数 DTO）不得迫使调用方引用 ORM / 日志 / Excel Provider。
        Assert.Empty(violations);
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

        Assert.Contains("DataManager.Core", references);
    }

    /// <summary>
    /// 验证 Runtime_hosts_only_known_manager_implementations 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 True、IsAssignableFrom、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Runtime_hosts_only_known_manager_implementations()
    {
        // P12：Manager 实现与默认工厂归属 Runtime；命名空间保持不变，避免调用方改 using。
        Assert.True(typeof(IManager<ISQL, DataManager.Core.SQL.Definition.SQLParam>).IsAssignableFrom(typeof(SQLManager)));
        Assert.True(typeof(ISQLManager).IsAssignableFrom(typeof(SQLManager)));
        Assert.True(typeof(ILogManager).IsAssignableFrom(typeof(LogManager)));
        Assert.True(typeof(IExcelManager).IsAssignableFrom(typeof(ExcelManager)));
        Assert.Equal(RuntimeAssembly, typeof(SQLManager).Assembly);
        Assert.Equal(RuntimeAssembly, typeof(LogManager).Assembly);
        Assert.Equal(RuntimeAssembly, typeof(ExcelManager).Assembly);
        Assert.Equal(CoreAssembly, typeof(ISQLManager).Assembly);
        Assert.Equal(CoreAssembly, typeof(ILogManager).Assembly);
        Assert.Equal(CoreAssembly, typeof(IExcelManager).Assembly);
    }

    /// <summary>
    /// 验证 Default_manager_constructors_use_builtin_factories 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 IsAssignableFrom；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Default_manager_constructors_use_builtin_factories()
    {
        // P12-2：无参构造继续可用（默认 Runtime 内建 factory），并接受可注入 factory。
        var sqlDefault = new SQLManager();
        var sqlInjected = new SQLManager(new SqlProviderFactory());
        var logDefault = new LogManager();
        var logInjected = new LogManager(new LogProviderFactory());
        var excelDefault = new ExcelManager();
        var excelInjected = new ExcelManager(new ExcelProviderFactory());

        Assert.IsAssignableFrom<ISQLManager>(sqlDefault);
        Assert.IsAssignableFrom<ISQLManager>(sqlInjected);
        Assert.IsAssignableFrom<ILogManager>(logDefault);
        Assert.IsAssignableFrom<ILogManager>(logInjected);
        Assert.IsAssignableFrom<IExcelManager>(excelDefault);
        Assert.IsAssignableFrom<IExcelManager>(excelInjected);
    }

    /// <summary>
    /// 验证 Legacy_manager_apis_remain_reachable_after_facade_removal 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetType、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Legacy_manager_apis_remain_reachable_after_facade_removal()
    {
        // 2026-09-29：兼容门面（程序集 DataManager）已删除。原先由「门面转发」承接的旧 API，
        // 现在直接由 Runtime 承载；这里守住三个代表性入口仍然可加载、类型身份不变。
        var sqlManagerType = RuntimeAssembly.GetType("DataManager.Runtime.SQL.SQLManager", throwOnError: true)!;
        var logManagerType = RuntimeAssembly.GetType("DataManager.Runtime.Log.LogManager", throwOnError: true)!;
        var excelManagerType = RuntimeAssembly.GetType("DataManager.Runtime.Excel.ExcelManager", throwOnError: true)!;

        Assert.Equal(typeof(SQLManager), sqlManagerType);
        Assert.Equal(typeof(LogManager), logManagerType);
        Assert.Equal(typeof(ExcelManager), excelManagerType);
    }

    /// <summary>
    /// 验证 Controls_references_core_and_runtime_only 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 LoadCsproj、ProjectReferences、Contains、DoesNotContain、EndsWith；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Controls_references_core_and_runtime_only()
    {
        var csproj = LoadCsproj("DataManager", "DataManager.Controls", "DataManager.Controls.csproj");
        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.Contains("DataManager.Core"));
        // Framework-Refactoring §4.4：Controls 统一注册本模块通用实现（Manager/Workspace/页面），允许依赖 Runtime。
        Assert.Contains(projects, r => r.Contains("DataManager.Runtime"));
        Assert.DoesNotContain(projects, r => r.Contains("Workbench"));
        Assert.DoesNotContain(projects, r => r.EndsWith($"DataManager{Path.DirectorySeparatorChar}DataManager.csproj", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 验证 Workbench_composes_core_runtime_and_controls 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 LoadCsproj、ProjectReferences、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Workbench_composes_core_runtime_and_controls()
    {
        var csproj = LoadCsproj("DataManager", "DataManager.Workbench", "DataManager.Workbench.csproj");
        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.Contains("DataManager.Core"));
        Assert.Contains(projects, r => r.Contains("DataManager.Runtime"));
        Assert.Contains(projects, r => r.Contains("DataManager.Controls"));
    }

    /// <summary>
    /// 验证 Compat_facade_project_is_removed 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Combine、RepoRoot、False、Exists；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Compat_facade_project_is_removed()
    {
        // 2026-09-29：兼容门面 DataManager/DataManager（AssemblyName=DataManager）已删除；
        // 消费者改为直接引用 DataManager.Core / DataManager.Runtime。
        // 保留此断言防止门面被重新引入（架构清晰前不再考虑兼容层）。
        var path = Path.Combine([RepoRoot(), "DataManager", "DataManager", "DataManager.csproj"]);
        Assert.False(File.Exists(path), $"兼容门面不应重新引入：{path}");
    }

    /// <summary>
    /// P3 边界审计 G1：Controls 只做表现层，不得直接绑定 ORM / 日志 / Excel Provider。
    /// 业务逻辑（指纹、实例生命周期、数据操作）若落进 Controls，最直接的表征就是开始 using
    /// Dapper/EFCore/SqlSugar/EPPlus/日志 Provider。既堵「包直接引用」，也堵「经 Extension 传递
    /// 带入后直接 using」——后者编译期无人拦得住，只能靠源码级守卫。
    /// </summary>
    [Fact]
    public void Controls_does_not_bind_orm_log_or_excel_providers()
    {
        var csproj = LoadCsproj("DataManager", "DataManager.Controls", "DataManager.Controls.csproj");
        var packages = PackageReferences(csproj);
        var packageViolations = ProviderPackages.Where(packages.Contains).OrderBy(name => name).ToArray();
        Assert.Empty(packageViolations);

        var sourceViolations = SourceFiles("DataManager.Controls")
            .Where(file => ProviderNamespaceUsing.IsMatch(StripComments(File.ReadAllText(file))))
            .Select(Path.GetFileName)
            .OrderBy(name => name)
            .ToArray();
        Assert.Empty(sourceViolations);
    }

    /// <summary>
    /// P3 G2：DataManager.Tests 是「无 WPF」的逻辑测试线——不启 WPF、不引 WPF 风味 Prism 容器包，
    /// 只直引 Core + Runtime，不借 Controls / Workbench 间接取依赖。
    /// </summary>
    [Fact]
    public void Logic_tests_line_stays_wpf_free_and_directly_references_core_and_runtime()
    {
        var csproj = LoadCsproj("DataManager", "Tests", "DataManager.Tests", "DataManager.Tests.csproj");
        Assert.False(HasUseWpf(csproj), "DataManager.Tests 是逻辑测试线，不应启用 WPF。");

        var packages = PackageReferences(csproj);
        Assert.Empty(WpfPrismPackages.Where(packages.Contains).OrderBy(name => name));

        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.Contains("DataManager.Core"));    // 直引下限（防假绿）
        Assert.Contains(projects, r => r.Contains("DataManager.Runtime"));
        Assert.DoesNotContain(projects, r => r.Contains("DataManager.Controls"));
        Assert.DoesNotContain(projects, r => r.Contains("DataManager.Workbench"));
    }

    /// <summary>
    /// P3 G3：WPF 集成测试线必须显式启用 WPF（与 MotionControl / Communication 两条线对齐），
    /// 并组合被验证的 Controls 与宿主 Workbench。
    /// </summary>
    [Fact]
    public void Workbench_tests_line_enables_wpf()
    {
        var csproj = LoadCsproj("DataManager", "Tests", "DataManager.Workbench.Tests", "DataManager.Workbench.Tests.csproj");
        Assert.True(HasUseWpf(csproj), "WPF 集成测试线必须显式 UseWPF=true。");

        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.Contains("DataManager.Controls"));
        Assert.Contains(projects, r => r.Contains("DataManager.Workbench"));
    }

    /// <summary>Controls 不得直接引用的 Provider 包（运行时实现归 Runtime）。</summary>
    private static readonly string[] ProviderPackages =
    {
        "Dapper", "Dommel", "SqlSugarCore", "EPPlus", "MiniExcel", "Google.Protobuf",
        "Serilog", "Serilog.Sinks.File", "NLog", "log4net",
        "Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.Sqlite", "Microsoft.EntityFrameworkCore.SqlServer",
        "MySql.EntityFrameworkCore", "Npgsql.EntityFrameworkCore.PostgreSQL",
        "Microsoft.Data.SqlClient", "Microsoft.Data.Sqlite", "MySqlConnector",
    };

    /// <summary>WPF 风味的 Prism 容器包：逻辑测试线不得引用（只需要 Prism.Core 的 IModule 契约）。</summary>
    private static readonly string[] WpfPrismPackages = { "Prism.DryIoc", "Prism.Wpf", "Prism.Unity" };

    /// <summary>Controls 源码里不得出现的 Provider using（带词边界，避免误伤同名前缀）。</summary>
    private static readonly Regex ProviderNamespaceUsing = new(
        @"^\s*using\s+(static\s+)?(Dapper|Dommel|SqlSugar\w*|Microsoft\.EntityFrameworkCore\w*|OfficeOpenXml\w*|MiniExcelLibs\w*|Google\.Protobuf\w*|Serilog\w*|NLog\w*|log4net\w*|Npgsql\w*|MySqlConnector\w*|Microsoft\.Data\.SqlClient\w*|Microsoft\.Data\.Sqlite\w*)\s*(;|=)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex BlockComments = new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex LineComments = new(@"//[^\n]*", RegexOptions.Compiled);

    /// <summary>剥掉块注释与行注释，避免「注释里写了 using」造成假阳性。</summary>
    private static string StripComments(string text) =>
        LineComments.Replace(BlockComments.Replace(text, " "), " ");

    /// <summary>
    /// 验证 HasUseWpf 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equals、FirstOrDefault、Descendants、Trim；具体条件和先后次序见方法体。
    /// </summary>
    private static bool HasUseWpf(XDocument csproj) =>
        string.Equals(csproj.Descendants("UseWPF").FirstOrDefault()?.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 验证 SourceFiles 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Where、EnumerateFiles、Combine、RepoRoot、Contains；具体条件和先后次序见方法体。
    /// </summary>
    private static IEnumerable<string> SourceFiles(string projectFolder) =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), "DataManager", projectFolder), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    /// <summary>
    /// 验证 IsAllowedFrameworkType 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetName、StartsWith；具体条件和先后次序见方法体。
    /// </summary>
    private static bool IsAllowedFrameworkType(Type type)
    {
        var assemblyName = type.Assembly.GetName().Name ?? "";
        return assemblyName is "netstandard" or "mscorlib" or "System.Private.CoreLib"
            || assemblyName.StartsWith("System", StringComparison.Ordinal)
            || assemblyName.StartsWith("Microsoft.Extensions", StringComparison.Ordinal)
            || assemblyName.StartsWith("CommunityToolkit", StringComparison.Ordinal)
            || assemblyName is "Extension" or "DataManager.Core";
    }

    /// <summary>
    /// 验证 RepoRoot 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Exists、Combine、True；具体条件和先后次序见方法体。
    /// </summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DataManager", "DataManager.Core", "DataManager.Core.csproj")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "未找到仓库根目录（DataManager/DataManager.Core/DataManager.Core.csproj）。");
        return dir.FullName;
    }

    /// <summary>
    /// 验证 LoadCsproj 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Combine、RepoRoot、True、Exists、Load；具体条件和先后次序见方法体。
    /// </summary>
    private static XDocument LoadCsproj(params string[] relativeParts)
    {
        var path = Path.Combine([RepoRoot(), .. relativeParts]);
        Assert.True(File.Exists(path), $"缺少工程文件：{path}");
        return XDocument.Load(path);
    }

    /// <summary>
    /// 验证 ProjectReferences 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToHashSet、Select、Descendants、Replace、Attribute；具体条件和先后次序见方法体。
    /// </summary>
    private static HashSet<string> ProjectReferences(XDocument csproj) =>
        csproj.Descendants("ProjectReference")
            .Select(e => (e.Attribute("Include")?.Value ?? string.Empty).Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 验证 PackageReferences 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToHashSet、Select、Descendants、Attribute；具体条件和先后次序见方法体。
    /// </summary>
    private static HashSet<string> PackageReferences(XDocument csproj) =>
        csproj.Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 验证 GetPublicSurfaceTypes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Expand、GetInterfaces、GetConstructors、GetParameters、GetFields；具体条件和先后次序见方法体。
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

        foreach (var constructor in type.GetConstructors())
        {
            foreach (var parameter in constructor.GetParameters())
            {
                foreach (var referencedType in Expand(parameter.ParameterType))
                {
                    yield return referencedType;
                }
            }
        }

        foreach (var field in type.GetFields())
        {
            foreach (var referencedType in Expand(field.FieldType))
            {
                yield return referencedType;
            }
        }

        foreach (var property in type.GetProperties())
        {
            foreach (var referencedType in Expand(property.PropertyType))
            {
                yield return referencedType;
            }
        }

        foreach (var eventInfo in type.GetEvents())
        {
            foreach (var referencedType in Expand(eventInfo.EventHandlerType!))
            {
                yield return referencedType;
            }
        }

        foreach (var method in type.GetMethods()
            .Where(method => !method.IsSpecialName))
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
