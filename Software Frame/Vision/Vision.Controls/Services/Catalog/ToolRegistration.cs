#nullable enable
using Vision.Controls.Models;
using Vision.Controls.ViewModels;
using Vision.Core.AI;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Controls.Catalog;

/// <summary>一次注册同时生成原始工具工厂和 Controls 条目；声明目录时不创建工具或 SDK 会话。</summary>
internal sealed record ToolRegistration(string Key, Func<object> Factory, ToolEntry Entry)
{
    public static ToolRegistration Halcon<TParameters, TResult>(string name, string category,
        Func<Vision.Tools.Halcon.Internal.HalconToolBase<TParameters, TResult>> factory)
        where TParameters : class, new() where TResult : ToolResult, new()
        => Register($"{name}（{category})", "HALCON", name, factory,
            tool => (new HalconToolCardAdapter<TParameters, TResult>(tool), tool.Parameters, null), producesOutputImage: typeof(IOutputImageResult).IsAssignableFrom(typeof(TResult)), producesFixture: typeof(IFixtureResult).IsAssignableFrom(typeof(TResult)));

    public static ToolRegistration OpenCv<TParameters, TResult>(string name, string category,
        Func<Vision.Tools.OpenCv.Internal.OpenCvToolBase<TParameters, TResult>> factory,
        Action<TParameters>? configure = null, bool isDefault = false,
        Action<TParameters, VisionImage>? setTemplate = null)
        where TParameters : class, new() where TResult : ToolResult, new()
        => Register($"{name}（{category})", "OpenCV", name, factory, tool =>
        {
            configure?.Invoke(tool.Parameters);
            return (new OpenCvToolCardAdapter<TParameters, TResult>(tool), tool.Parameters, null);
        }, isDefault, setTemplate == null ? null : (parameters, image) => setTemplate((TParameters)parameters, image), producesOutputImage: typeof(IOutputImageResult).IsAssignableFrom(typeof(TResult)), producesFixture: typeof(IFixtureResult).IsAssignableFrom(typeof(TResult)));

    // 沿既有GeometryTool公共数学和旧适配器，不把它宣称为新HALCON算法。
    public static ToolRegistration CommonGeometry() =>
        Register("Geometry（Measurement)", "Common", "Geometry",
            () => new Vision.Tools.Halcon.Measurement.GeometryTool(),
            tool => (new HalconToolCardAdapter<Vision.Tools.Halcon.Measurement.GeometryParameters, Vision.Tools.Halcon.Measurement.GeometryResult>(tool), tool.Parameters, null));

    public static ToolRegistration CommonTolerance() =>
        Register("Tolerance（Inspection)", "Common", "Tolerance", () => new Vision.Core.Tools.ToleranceTool(),
            tool => (new ToleranceToolCardAdapter(tool), tool.Parameters, null));

    public static ToolRegistration AI<TParameters, TResult>(string name,
        Func<Vision.Tools.AI.Internal.AIToolBase<TParameters, TResult>> factory)
        where TParameters : Vision.Tools.AI.AIModelParameters, new() where TResult : ToolResult, new()
        => Register(name, "AI", name, factory, tool =>
        {
            var adapter = new AIToolCardAdapter<TParameters, TResult>(tool);
            return (adapter, tool.Parameters, provider => adapter.Provider = provider);
        }, supportsModelLoading: true, producesOutputImage: typeof(IOutputImageResult).IsAssignableFrom(typeof(TResult)), producesFixture: typeof(IFixtureResult).IsAssignableFrom(typeof(TResult)));

    private static ToolRegistration Register<TTool>(string key, string backend, string name, Func<TTool> factory,
        Func<TTool, (IExecutableTool Card, object Parameters, Action<IAIProvider>? SetProvider)> adapt,
        bool isDefault = false, Action<object, VisionImage>? setTemplate = null, bool supportsModelLoading = false, bool producesOutputImage = false, bool producesFixture = false) where TTool : IDisposable
    {
        var definition = Vision.FlowNode.VisionNodeDefinitions.Get($"{backend}/{name}");
        return new(key, () => factory(), new ToolEntry($"{backend}/{name}", backend, () =>
        {
            var tool = factory();
            try { return adapt(tool); }
            catch (Exception error)
            {
                try { tool.Dispose(); }
                catch (Exception cleanupError) { throw new AggregateException("工具适配及清理均失败。", error, cleanupError); }
                throw;
            }
        }, setTemplate, isDefault, supportsModelLoading) { ProducesOutputImage = producesOutputImage, ProducesFixture = producesFixture, NodeParameterNames = definition.Parameters, NumericResultFields = definition.NumericOutputs, LineResultFields = definition.LineOutputs, CircleResultFields = definition.CircleOutputs, PointResultFields = definition.PointOutputs, BooleanResultFields = definition.BooleanOutputs });
    }
}
