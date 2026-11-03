using System.Reflection;
using System.Runtime.ExceptionServices;
using Vision.Core;
using Vision.Core.Capabilities;
using Vision.Core.Engine;
using Vision.Tools.Execution;

namespace Vision.Tools;

/// <summary>
/// 从冻结选择创建唯一执行器。只加载已选模块，未知实现、设备或运行库错误直接报告，不回退。
/// </summary>
public static class VisionEngineFactory
{
    public static IVisionEngine Create(VisionEngineSelection selection)
    {
        return CreateConfigured(selection, ActivateModule);
    }

    /// <summary>仅核对当前已接通的身份和设备；不加载程序集或探测运行库。</summary>
    public static void ValidateSelection(VisionEngineSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.VisionBackendId is not ("opencv" or "halcon"))
        {
            throw new NotSupportedException($"尚未接通的视觉实现：{selection.VisionBackendId}。不会回退OpenCV。");
        }

        if (selection.AiBackendId != null
            && (selection.AiBackendId != "halcon" || selection.AiDeviceId != "cpu"))
        {
            throw new NotSupportedException("AI当前仅接通halcon/cpu；不会忽略未知实现或回退CPU。");
        }
    }

    // 只替换最末端模块激活；仍执行生产配置校验和设备传递，供边界测试使用。
    internal static IVisionEngine CreateConfigured(
        VisionEngineSelection selection,
        Func<VisionExecutionDomain, string, string?, IVisionBackend> activate)
    {
        ValidateSelection(selection);
        ArgumentNullException.ThrowIfNull(activate);
        return Create(selection, (domain, id) => activate(domain, id,
            domain == VisionExecutionDomain.Ai ? selection.AiDeviceId : null));
    }

    private static IVisionBackend ActivateModule(VisionExecutionDomain domain, string id, string? deviceId)
    {
        var (assembly, name) = (domain, id) switch
        {
            (VisionExecutionDomain.Vision, "opencv") => ("Vision.Tools.OpenCv", "Vision.Tools.OpenCv.Engine.OpenCvBackend"),
            (VisionExecutionDomain.Vision, "halcon") => ("Vision.Tools.Halcon", "Vision.Tools.Halcon.Engine.HalconBackend"),
            (VisionExecutionDomain.Ai, "halcon") => ("Vision.AI.Halcon", "Vision.AI.Halcon.Engine.HalconClassificationBackend"),
            _ => throw new NotSupportedException("未注册的执行域/模块组合。")
        };
        var type = Assembly.Load(assembly).GetType(name, throwOnError: true)!;
        try
        {
            return (IVisionBackend)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null, args: domain == VisionExecutionDomain.Ai ? [deviceId!] : [], culture: null)!;
        }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    /// <summary>内部装配缝验证所有权和部分失败清理；不接受任意插件路径，也不是公开业务入口。</summary>
    internal static IVisionEngine Create(
        VisionEngineSelection selection,
        Func<VisionExecutionDomain, string, IVisionBackend> activate)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(activate);
        List<IVisionBackend> created = [];
        try
        {
            IVisionBackend Activate(VisionExecutionDomain domain, string id)
            {
                var backend = activate(domain, id) ?? throw new InvalidOperationException("后端激活返回空值。");
                if (!created.Any(existing => ReferenceEquals(existing, backend)))
                {
                    created.Add(backend);
                }

                if (backend.Id != id || backend.Domain != domain)
                {
                    throw new InvalidOperationException("模块身份与已选配置不符。");
                }

                return backend;
            }

            var vision = Activate(VisionExecutionDomain.Vision, selection.VisionBackendId);
            var ai = selection.AiBackendId is { } id ? Activate(VisionExecutionDomain.Ai, id) : null;
            return new VisionEngine(selection, vision, ai);
        }
        catch (Exception original)
        {
            List<Exception> errors = [original];
            foreach (var backend in created.AsEnumerable().Reverse())
            {
                try
                {
                    backend.Dispose();
                }
                catch (Exception cleanup)
                {
                    errors.Add(cleanup);
                }
            }

            if (errors.Count > 1)
            {
                throw new AggregateException("引擎装配及清理失败。", errors);
            }

            throw;
        }
    }
}
