using Vision.Core.Imaging;
using Xunit;
using Vision.Core;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using System.Reflection;

namespace Vision.Tests.Core;

[Trait("Category", "Contract")]
public sealed class VisionEngineContractTests
{
    [Fact]
    public void NewFacade_UsesOneBusinessInterface_WithTypedExecutionAndExplicitLifetime()
    {
        var assembly = typeof(IVisionEngine).Assembly;
        var engine = assembly.GetType("Vision.Core.IVisionEngine");
        Assert.NotNull(engine);
        Assert.True(engine.IsInterface);
        Assert.Contains(typeof(IAsyncDisposable), engine.GetInterfaces());
        Assert.Equal(new[] { "CreateContextAsync", "ExecuteAsync", "PrepareModelAsync", "ReleaseContextAsync", "ValidateAsync", "get_Descriptor" },
            engine.GetMethods().Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal));
        var execute = Assert.Single(engine.GetMethods(), method => method.Name == "ExecuteAsync");
        Assert.True(execute.IsGenericMethodDefinition);
        Assert.Contains(execute.GetParameters(), parameter => parameter.ParameterType == typeof(CancellationToken));
    }

    [Fact]
    public void FrameAndMask_CopyCallerBuffers_AndRejectAmbiguousInput()
    {
        byte[] pixels = [3, 4, 5, 6];
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 2, 2, PixelFormat.Mono8, pixels);
        pixels[0] = 99;
        Assert.Equal(3, frame.Pixels[0]);
        Assert.Equal(2, frame.Stride);
        byte[] maskPixels = [0, 1, 255, 0];
        var mask = new VisionMask(2, 2, maskPixels);
        maskPixels[1] = 0;
        Assert.Equal(new byte[] { 0, 255, 255, 0 }, mask.Pixels.ToArray());
        Assert.Throws<ArgumentException>(() => new VisionMask(2, 2, new byte[4]));
        Assert.Throws<ArgumentException>(() => new VisionMask(2, 2, new byte[] { 0, 2, 0, 0 }));
        Assert.Throws<ArgumentException>(() => mask.EnsureSize(1, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 2, 2, PixelFormat.Mono8, new byte[4], -1));
        Assert.Throws<ArgumentException>(() => new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 2, 2, PixelFormat.Mono16, new byte[4]));
        Assert.Throws<ArgumentException>(() => new VisionFrame(Guid.Empty, Guid.NewGuid(), 2, 2, PixelFormat.Mono8, new byte[4]));
    }

    [Theory]
    [InlineData(double.NaN, 0, 1, 1)]
    [InlineData(0, double.PositiveInfinity, 1, 1)]
    [InlineData(0, -1, 1, 1)]
    [InlineData(0, 361, 1, 1)]
    [InlineData(0, 0, 0, 1)]
    [InlineData(0, 0, 2, 1)]
    [InlineData(0, 0, 1, double.NaN)]
    public void SearchRange_RejectsNonFiniteReversedAndUnsupportedValues(double start, double extent, double minimum, double maximum)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new TemplateSearchRange(start, extent, minimum, maximum));

    [Fact]
    public void SearchRange_UsesCircularIntervals_AndRespectsPreparedScaleRange()
    {
        var prepared = new TemplateSearchRange(170, 30, 0.5, 2);
        Assert.True(prepared.Contains(new(-175, 5, 0.7, 1.5)));
        Assert.False(prepared.Contains(new(-175, 20, 0.7, 1.5)));
        Assert.False(prepared.Contains(new(-175, 5, 0.4, 1.5)));
        Assert.True(new TemplateSearchRange(-180, 360).Contains(new(170, 20)));
    }

    [Fact]
    public void TemplateRequest_RequiresExplicitCapabilities_AndRejectsForeignContextAndBackend()
    {
        var context = new VisionContext(Guid.NewGuid(), Guid.NewGuid());
        var model = TemplateModel(context);
        var operation = new TemplateMatchOperation(Frame(), model, new(-20, 40, 0.8, 1.2), maximumMatches: 2,
            searchMask: new VisionMask(4, 4, Enumerable.Repeat((byte)1, 16).ToArray()));
        var all = VisionOperationFeatures.Rotation | VisionOperationFeatures.UniformScale | VisionOperationFeatures.TemplateMask |
            VisionOperationFeatures.SearchMask | VisionOperationFeatures.MultipleMatches;
        var descriptor = new VisionEngineDescriptor(context.EngineInstanceId, new("opencv"), [new(TemplateMatchOperation.Id, VisionExecutionDomain.Vision, all)]);
        Assert.Empty(operation.Validate(descriptor, context));
        var limited = new VisionEngineDescriptor(context.EngineInstanceId, new("opencv"), [new(TemplateMatchOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None)]);
        Assert.Contains(operation.Validate(limited, context), issue => issue.Code == "UnsupportedCapability");
        var wrongBackend = new VisionEngineDescriptor(context.EngineInstanceId, new("halcon"), descriptor.Operations);
        Assert.Contains(operation.Validate(wrongBackend, context), issue => issue.Code == "BackendMismatch");
        Assert.Contains(operation.Validate(descriptor, new(context.EngineInstanceId, Guid.NewGuid())), issue => issue.Code == "ContextMismatch");
        var wrongEngine = new VisionEngineDescriptor(Guid.NewGuid(), new("opencv"), descriptor.Operations);
        Assert.Contains(operation.Validate(wrongEngine, context), issue => issue.Code == "ContextMismatch");
        var outside = new TemplateMatchOperation(Frame(), model, new(0, 0, 0.1, 3));
        Assert.Contains(outside.Validate(descriptor, context), issue => issue.Code == "ModelRangeMismatch");
        Assert.Throws<ArgumentException>(() => new TemplateMatchOperation(Frame(), model, new(), searchMask: new VisionMask(1, 1, new byte[] { 1 })));
    }

    [Fact]
    public void MatchTransform_RotatesAndScalesAroundModelOrigin_NotImageOrigin()
    {
        var candidate = new TemplateMatchCandidate(new(100, 50), new(3, 2), 90, 2, 0.9);
        var transform = candidate.TemplateToImage;
        Assert.Equal(new Point2D(100, 50), transform.Transform(candidate.ModelOrigin));
        Assert.Equal(new Point2D(100, 52), transform.Transform(new(4, 2)));
        Assert.Equal(new Point2D(4, 2), transform.TransformInverse(new(100, 52)));
        Assert.Equal(2, candidate.Scale);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TemplateMatchCandidate(new(), new(), 0, -1, 0.9));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TemplateMatchCandidate(new(), new(), double.NaN, 1, 0.9));
        Assert.Throws<ArgumentException>(() => new TemplateMatchCandidate(new(), new(2, 2), 0, double.MaxValue, 0.9));
    }

    [Fact]
    public void SelectedBackends_AreSingleValues_AndAiRequiresAnExplicitDevice()
    {
        Assert.Equal("ni", new VisionEngineSelection("ni").VisionBackendId); // 开放实现身份，不在Core硬编码品牌表。
        Assert.Throws<ArgumentException>(() => new VisionEngineSelection(" opencv"));
        Assert.Throws<ArgumentNullException>(() => new VisionEngineSelection("opencv", "invi"));
        Assert.Throws<ArgumentException>(() => new VisionEngineSelection("opencv", aiDeviceId: "cpu"));
        Assert.Throws<ArgumentException>(() => new VisionEngineDescriptor(Guid.NewGuid(), new("opencv"),
            [new(AiClassificationOperation.Id, VisionExecutionDomain.Ai, VisionOperationFeatures.None)]));
        var context = new VisionContext(Guid.NewGuid(), Guid.NewGuid());
        var definition = new AiClassificationModelDefinition("not-loaded.onnx", new string('a', 64));
        var model = new VisionModelReference(context, Guid.NewGuid(), "invi", definition, 4);
        var operation = new AiClassificationOperation(Frame(), model, 0.7, 2);
        var descriptor = new VisionEngineDescriptor(context.EngineInstanceId, new("opencv", "invi", "cpu"),
            [new(AiClassificationOperation.Id, VisionExecutionDomain.Ai, VisionOperationFeatures.None)]);
        Assert.Empty(operation.Validate(descriptor, context));
        Assert.Throws<ArgumentException>(() => new AiClassificationModelDefinition("model.onnx", "path-is-not-a-content-hash"));
        Assert.Throws<ArgumentException>(() => new AiClassificationOperation(Frame(), TemplateModel(context)));
        Assert.Throws<ArgumentException>(() => new TemplateMatchOperation(Frame(), model, new()));
    }

    [Fact]
    public void CapabilityAndResultCollections_AreIndependentReadOnlySnapshots()
    {
        var capabilities = new List<VisionOperationCapability> { new(TemplateMatchOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None) };
        var descriptor = new VisionEngineDescriptor(Guid.NewGuid(), new("opencv"), capabilities);
        capabilities.Clear();
        Assert.Single(descriptor.Operations);
        Assert.Throws<NotSupportedException>(() => ((IList<VisionOperationCapability>)descriptor.Operations).Clear());
        Assert.Throws<ArgumentException>(() => new VisionEngineDescriptor(Guid.NewGuid(), new("opencv"), [descriptor.Operations[0], descriptor.Operations[0]]));
        var frame = Frame();
        var matches = new List<TemplateMatchCandidate> { new(new(1, 1), new(), 0, 1, 0.9) };
        var output = new TemplateMatchOutput(frame.FrameId, frame.CoordinateSpaceId, "Normalized correlation; backend-specific threshold calibration", matches);
        matches.Clear();
        Assert.Single(output.Matches);
        Assert.Throws<NotSupportedException>(() => ((IList<TemplateMatchCandidate>)output.Matches).Clear());
        var predictions = new List<AiClassPrediction> { new("pass", 0.9) };
        var ai = new AiClassificationOutput(frame.FrameId, frame.CoordinateSpaceId, predictions);
        predictions.Clear();
        Assert.Single(ai.Predictions);
        Assert.Throws<ArgumentOutOfRangeException>(() => new AiClassPrediction("pass", double.NaN));
    }

    [Theory]
    [InlineData(VisionExecutionStatus.Unsupported)]
    [InlineData(VisionExecutionStatus.Failed)]
    [InlineData(VisionExecutionStatus.Cancelled)]
    [InlineData(VisionExecutionStatus.TimedOut)]
    public void UnsuccessfulOutcome_CannotCarryPreviousResults(VisionExecutionStatus status)
    {
        var output = new AiClassificationOutput(Guid.NewGuid(), Guid.NewGuid(), []);
        var issue = new VisionIssue("TestFailure", "No current result.");
        Assert.Throws<ArgumentException>(() => new VisionOutcome<AiClassificationOutput>(status, output, issue));
        var outcome = new VisionOutcome<AiClassificationOutput>(status, null, issue);
        Assert.Null(outcome.Value);
        Assert.Same(issue, outcome.Issue);
        Assert.Throws<ArgumentNullException>(() => new VisionOutcome<AiClassificationOutput>(status, null));
    }

    [Fact]
    public void NoMatch_IsACompletedSearch_NotAnSdkFailure()
    {
        var output = new TemplateMatchOutput(Guid.NewGuid(), Guid.NewGuid(), "Shape similarity", []);
        var outcome = new VisionOutcome<TemplateMatchOutput>(VisionExecutionStatus.NoMatch, output);
        Assert.NotNull(outcome.Value);
        Assert.Empty(outcome.Value.Matches);
        Assert.Null(outcome.Issue);
        Assert.Throws<ArgumentNullException>(() => new VisionOutcome<TemplateMatchOutput>(VisionExecutionStatus.Success, null));
    }

    [Fact]
    public void NewApiSignatureClosure_DoesNotExposeOldToolProviderOrUiInterfaces()
    {
        var assembly = typeof(IVisionEngine).Assembly;
        var visited = new HashSet<Type>();
        var pending = new Queue<Type>(new[] { typeof(IVisionEngine), typeof(TemplateMatchOperation), typeof(AiClassificationOperation), typeof(TemplateMatchCandidate) });
        while (pending.TryDequeue(out var type))
        {
            if (type.IsByRef || type.IsArray) { pending.Enqueue(type.GetElementType()!); continue; }
            if (!visited.Add(type) || type.IsGenericParameter) continue;
            if (type.IsGenericType) foreach (var argument in type.GetGenericArguments()) pending.Enqueue(argument);
            if (type.Assembly != assembly) continue;
            if (type.BaseType != null) pending.Enqueue(type.BaseType);
            foreach (var implemented in type.GetInterfaces()) pending.Enqueue(implemented);
            Assert.False(type.IsInterface && type != typeof(IVisionEngine), $"Additional domain interface exposed: {type}");
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                pending.Enqueue(method.ReturnType);
                foreach (var parameter in method.GetParameters()) pending.Enqueue(parameter.ParameterType);
            }
            foreach (var constructor in type.GetConstructors())
                foreach (var parameter in constructor.GetParameters()) pending.Enqueue(parameter.ParameterType);
        }
        Assert.True(visited.Count(type => type.Assembly == assembly) > 20);
    }

    [Fact]
    public void ImageOnlyOperations_DoNotRequireAFakeModel_AndStillValidateEngineOwnership()
    {
        var context = new VisionContext(Guid.NewGuid(), Guid.NewGuid());
        var operation = new ImageOnlyProbe(Frame());
        var descriptor = new VisionEngineDescriptor(context.EngineInstanceId, new("opencv"),
            [new(operation.OperationId, VisionExecutionDomain.Vision, VisionOperationFeatures.None)]);
        Assert.Null(operation.Model);
        Assert.Empty(operation.Validate(descriptor, context));
        Assert.Contains(operation.Validate(descriptor, new(Guid.NewGuid(), Guid.NewGuid())), issue => issue.Code == "ContextMismatch");
    }

    private sealed class ImageOnlyProbe(VisionFrame image) : VisionImageOperation<VisionFrame>(image)
    {
        public override string OperationId => "test.image-only";
        public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
        protected override void ValidateParameters(List<VisionIssue> issues) { }
    }

    private static VisionFrame Frame() => new(Guid.NewGuid(), Guid.NewGuid(), 4, 4, PixelFormat.Mono8, new byte[16]);
    private static VisionModelReference TemplateModel(VisionContext context) => new(context, Guid.NewGuid(), "opencv",
        new TemplateModelDefinition(Frame(), new(1, 1), new(-180, 360, 0.5, 2), new VisionMask(4, 4, Enumerable.Repeat((byte)1, 16).ToArray())), 1);

    [Theory]
    [InlineData("Vision.Core.Operations.TemplateMatchOperation")]
    [InlineData("Vision.Core.Operations.AiClassificationOperation")]
    public void TraditionalAndAiOperations_UseTheSameStronglyTypedContract(string typeName)
    {
        var operation = typeof(IVisionEngine).Assembly.GetType(typeName);
        Assert.NotNull(operation);
        Assert.Equal("Vision.Core.Engine.VisionImageOperation`1", operation.BaseType!.GetGenericTypeDefinition().FullName);
        Assert.DoesNotContain(operation.GetProperties(), property => property.Name is "BackendId" or "Provider" or "Options");
    }
}
