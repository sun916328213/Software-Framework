using System.Reflection;
using System.Xml.Linq;
using DataManager.Excel;
using DataManager.Log;
using DataManager.SQL;

namespace DataManager.Tests.Architecture;

/// <summary>
/// P12 物理四层守卫：Core / Runtime 已拆为独立程序集。
/// Core 程序集 = <see cref="ISQL"/> 所在；Runtime 程序集 = <see cref="SQLManager"/> 所在。
/// </summary>
public sealed class LayeringTests
{
    private static readonly Assembly CoreAssembly = typeof(ISQL).Assembly;
    private static readonly Assembly RuntimeAssembly = typeof(SQLManager).Assembly;

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

    [Fact]
    public void Runtime_assembly_references_core()
    {
        var references = RuntimeAssembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("DataManager.Core", references);
    }

    [Fact]
    public void Runtime_hosts_only_known_manager_implementations()
    {
        // P12：Manager 实现与默认工厂归属 Runtime；命名空间保持不变，避免调用方改 using。
        Assert.True(typeof(IManager<ISQL, DataManager.SQL.Definition.SQLParam>).IsAssignableFrom(typeof(SQLManager)));
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

    [Fact]
    public void Legacy_manager_apis_remain_reachable_after_facade_removal()
    {
        // 2026-09-29：兼容门面（程序集 DataManager）已删除。原先由「门面转发」承接的旧 API，
        // 现在直接由 Runtime 承载；这里守住三个代表性入口仍然可加载、类型身份不变。
        var sqlManagerType = RuntimeAssembly.GetType("DataManager.SQL.SQLManager", throwOnError: true)!;
        var logManagerType = RuntimeAssembly.GetType("DataManager.Log.LogManager", throwOnError: true)!;
        var excelManagerType = RuntimeAssembly.GetType("DataManager.Excel.ExcelManager", throwOnError: true)!;

        Assert.Equal(typeof(SQLManager), sqlManagerType);
        Assert.Equal(typeof(LogManager), logManagerType);
        Assert.Equal(typeof(ExcelManager), excelManagerType);
    }

    [Fact]
    public void Controls_is_core_only()
    {
        var csproj = LoadCsproj("DataManager", "DataManager.Controls", "DataManager.Controls.csproj");
        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.Contains("DataManager.Core"));
        Assert.DoesNotContain(projects, r => r.Contains("DataManager.Runtime"));
        Assert.DoesNotContain(projects, r => r.EndsWith($"DataManager{Path.DirectorySeparatorChar}DataManager.csproj", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Workbench_composes_core_runtime_and_controls()
    {
        var csproj = LoadCsproj("DataManager", "DataManager.Workbench", "DataManager.Workbench.csproj");
        var projects = ProjectReferences(csproj);
        Assert.Contains(projects, r => r.Contains("DataManager.Core"));
        Assert.Contains(projects, r => r.Contains("DataManager.Runtime"));
        Assert.Contains(projects, r => r.Contains("DataManager.Controls"));
    }

    [Fact]
    public void Compat_facade_project_is_removed()
    {
        // 2026-09-29：兼容门面 DataManager/DataManager（AssemblyName=DataManager）已删除；
        // 消费者改为直接引用 DataManager.Core / DataManager.Runtime。
        // 保留此断言防止门面被重新引入（架构清晰前不再考虑兼容层）。
        var path = Path.Combine([RepoRoot(), "DataManager", "DataManager", "DataManager.csproj"]);
        Assert.False(File.Exists(path), $"兼容门面不应重新引入：{path}");
    }

    private static bool IsAllowedFrameworkType(Type type)
    {
        var assemblyName = type.Assembly.GetName().Name ?? "";
        return assemblyName is "netstandard" or "mscorlib" or "System.Private.CoreLib"
            || assemblyName.StartsWith("System", StringComparison.Ordinal)
            || assemblyName.StartsWith("Microsoft.Extensions", StringComparison.Ordinal)
            || assemblyName.StartsWith("CommunityToolkit", StringComparison.Ordinal)
            || assemblyName is "Extension" or "DataManager.Core";
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Frame.sln")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "未找到仓库根目录（Frame.sln）。");
        return dir.FullName;
    }

    private static XDocument LoadCsproj(params string[] relativeParts)
    {
        var path = Path.Combine([RepoRoot(), .. relativeParts]);
        Assert.True(File.Exists(path), $"缺少工程文件：{path}");
        return XDocument.Load(path);
    }

    private static HashSet<string> ProjectReferences(XDocument csproj) =>
        csproj.Descendants("ProjectReference")
            .Select(e => (e.Attribute("Include")?.Value ?? string.Empty).Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> PackageReferences(XDocument csproj) =>
        csproj.Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
