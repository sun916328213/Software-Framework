// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ToolContractAsserts。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tests.Tools、System.Reflection、System.Text.Json、Vision.Core.Results、Xunit。导入不代表所有类型都会在运行时加载。
using Vision.Tests.Tools;
// ─────────────────────────────────────────────────────────────────────────────
// P10 —— 视觉 Tool 公共契约断言（计划 §P10「为视觉 Tool 建立公共 Contract Tests：
// 统一验证 Parameters、Validate、Run、Result、资源释放与异常语义」）。
//
// 各阶段自带的契约测试保留（行为深度），本文件提供跨后端统一口径：
// 同一个断言形状跑遍全部 P3/P4 传统 Tool；HALCON/OpenCV 同类按业务容差比较
// （见 Comparison），不要求逐像素一致。
// 约定：Run(null) 在校验失败时必须返回失败结果且不抛（各基类契约），
// 因此本套件全程无需 HALCON Runtime / 相机 / GPU.
// ─────────────────────────────────────────────────────────────────────────────

using System.Reflection;
using System.Text.Json;
using Vision.Core.Results;
using Xunit;

namespace Vision.Tests.Tools.Contracts;

/// <summary>跨 Tool 统一契约断言（静态 helper，供本目录套件使用）。</summary>
public static class ToolContractAsserts
{
    /// <summary>失败结果形状：Success=false + Validation 非空 + 有说明。</summary>
    public static void IsFailedResult(ToolResult result)
    {
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.NotNull(result.Validation);
        Assert.False(string.IsNullOrEmpty(result.Message));
    }

    /// <summary>Result 派生自 ToolResult 且公开签名无原生 SDK 类型。</summary>
    public static void IsCleanResult(Type resultType)
    {
        Assert.True(typeof(ToolResult).IsAssignableFrom(resultType), $"{resultType.Name} 必须派生自 ToolResult。");
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        foreach (var property in resultType.GetProperties(flags))
        {
            foreach (var exposed in Unwrap(property.PropertyType))
            {
                var assembly = exposed.Assembly.GetName().Name ?? string.Empty;
                Assert.False(
                    assembly.StartsWith("HalconDotNet", StringComparison.Ordinal) ||
                    assembly.StartsWith("MVTec", StringComparison.Ordinal) ||
                    assembly.StartsWith("OpenCvSharp", StringComparison.Ordinal),
                    $"{resultType.Name}.{property.Name} 暴露了原生类型：{exposed.FullName}");
            }
        }
    }

    /// <summary>参数 JSON 往返稳定（序列化→反序列化→再序列化文本一致）。</summary>
    public static void RoundTripsThroughJson(object parameters)
    {
        Assert.NotNull(parameters);
        var first = JsonSerializer.Serialize(parameters, parameters.GetType());
        var back = JsonSerializer.Deserialize(first, parameters.GetType());
        Assert.NotNull(back);
        Assert.Equal(first, JsonSerializer.Serialize(back, back.GetType()));
    }

    /// <summary>
    /// 测试相关入口 Unwrap；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetGenericArguments、GetName、StartsWith；具体条件和先后次序见方法体。
    /// </summary>
    private static IEnumerable<Type> Unwrap(Type type)
    {
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                if (argument.IsGenericParameter)
                    continue;
                var assembly = argument.Assembly.GetName().Name ?? string.Empty;
                if (assembly.StartsWith("HalconDotNet", StringComparison.Ordinal) ||
                    assembly.StartsWith("MVTec", StringComparison.Ordinal) ||
                    assembly.StartsWith("OpenCvSharp", StringComparison.Ordinal))
                    yield return argument;
            }

            yield break;
        }

        yield return type;
    }
}
