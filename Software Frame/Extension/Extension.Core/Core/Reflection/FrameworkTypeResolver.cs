// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：FrameworkTypeResolver、Alias。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Reflection、System.IO、System.Text.Json。导入不代表所有类型都会在运行时加载。
using System.Reflection;
using System.IO;
using System.Text.Json;

#if !FLOW_EDITOR_TYPE_RESOLVER && !VISION_TYPE_RESOLVER
namespace Extension.Core.Reflection;
#elif FLOW_EDITOR_TYPE_RESOLVER
namespace FlowEditor.Core;
#else
namespace Vision.Core.Tools;
#endif

/// <summary>Resolves persisted CLR names using exact namespace migration aliases.</summary>
/// <remarks>
/// FrameworkTypeResolver 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
/// </remarks>
public static class FrameworkTypeResolver
{
    /// <summary>
    /// Alias 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// </summary>
    private sealed record Alias(string Type, string Assembly);
    private static readonly Lazy<IReadOnlyDictionary<string, Alias>> Aliases = new(LoadAliases);

    /// <summary>Resolves a current or migrated type name; unknown names return null.</summary>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ResolvePersistedType。
    /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、GetType；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public static Type? ResolvePersistedType(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        try
        {
            return Type.GetType(name, ResolveAssembly, ResolveType, throwOnError: false);
        }
        catch (Exception error) when (error is ArgumentException or FileNotFoundException or FileLoadException or TypeLoadException or BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ResolveAssembly。
    /// 正文中的关键协作调用包括 Load、Equals；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private static Assembly? ResolveAssembly(AssemblyName name)
    {
        try
        {
            // Persisted UI identities migrate directly to the unified assembly; no old DLL is loaded.
            var migratedName = name.Name switch
            {
                "FlowEditor" => "FlowEditor.Controls",
                "WPFControl" or "WPFControl.Controls" or "WPFControl.Application" => "WPFControls",
                "Packing Machine" => "MachineApp",
                _ => null
            };
            return Assembly.Load(migratedName is null ? name : new AssemblyName(migratedName));
        }
        catch (Exception error) when (error is FileNotFoundException or FileLoadException)
        {
            // S5 uses a direct assembly cutover; a missing Extension must not search Core.
            if (string.Equals(name.Name, "Extension", StringComparison.OrdinalIgnoreCase)) throw;
            // A migrated type may now belong to a different assembly; the exact alias resolves it below.
            return name.Name == "FlowEditor" ? typeof(FrameworkTypeResolver).Assembly : null;
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ResolveType。
    /// 正文中的关键协作调用包括 IndexOf、Min、TryGetValue、GetType、Load；具体条件和先后次序见方法体。
    /// </summary>
    private static Type? ResolveType(Assembly? assembly, string name, bool ignoreCase)
    {
        var nested = name.IndexOf('+');
        var generic = name.IndexOf('`');
        var suffixStart = nested < 0 ? generic : generic < 0 ? nested : Math.Min(nested, generic);
        var key = suffixStart < 0 ? name : name[..suffixStart];
        var suffix = suffixStart < 0 ? string.Empty : name[suffixStart..];
        if (Aliases.Value.TryGetValue(key, out var alias))
            return Assembly.Load(new AssemblyName(alias.Assembly)).GetType(alias.Type + suffix, false, ignoreCase);
        if (assembly is not null) return assembly.GetType(name, false, ignoreCase);
        foreach (var candidate in AppDomain.CurrentDomain.GetAssemblies())
            if (candidate.GetType(name, false, ignoreCase) is { } type) return type;
        return typeof(object).Assembly.GetType(name, false, ignoreCase);
    }

    /// <summary>
    /// 执行加载操作；对应入口为 LoadAliases。
    /// 正文中的关键协作调用包括 GetManifestResourceStream、Deserialize；具体条件和先后次序见方法体。
    /// </summary>
    private static IReadOnlyDictionary<string, Alias> LoadAliases()
    {
        using var stream = typeof(FrameworkTypeResolver).Assembly.GetManifestResourceStream("Framework.TypeAliases.json")
            ?? throw new InvalidOperationException("Missing framework type migration aliases.");
        return JsonSerializer.Deserialize<Dictionary<string, Alias>>(stream)
            ?? throw new InvalidOperationException("Invalid framework type migration aliases.");
    }
}
