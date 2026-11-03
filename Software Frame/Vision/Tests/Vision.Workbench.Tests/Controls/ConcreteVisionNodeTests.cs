using System.IO;
using System.Text.Json.Nodes;
using Vision.Controls.Services;
using Vision.Core.Imaging;
using Vision.FlowNode;
using WPFCustomControls.PropertyGrid.Core.ViewModels;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class ConcreteVisionNodeTests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData("OpenCV/Blob", "Vision.FlowNode.OpenCv.BlobNode", "MinGray", 87)]
    [InlineData("OpenCV/FindLine", "Vision.FlowNode.OpenCv.FindLineNode", "Threshold", 91)]
    [InlineData("OpenCV/FindCircle", "Vision.FlowNode.OpenCv.FindCircleNode", "RadiusMin", 19)]
    [InlineData("OpenCV/TemplateMatch", "Vision.FlowNode.OpenCv.TemplateMatchNode", "MaxMatches", 3)]
    [InlineData("OpenCV/Threshold", "Vision.FlowNode.OpenCv.ThresholdNode", "MinGray", 87)]
    [InlineData("OpenCV/GrayStatistics", "Vision.FlowNode.OpenCv.GrayStatisticsNode", "HistogramBinWidth", 2)]
    [InlineData("Common/Geometry", "Vision.FlowNode.Common.GeometryNode", "Operation", 1)]
    [InlineData("Common/Tolerance", "Vision.FlowNode.Common.ToleranceNode", "Maximum", 17)]
    public async Task CatalogCreatesConcreteNode_WithOriginalDtoPortsAndBorrowedEditor(
        string tool, string typeName, string parameter, int value)
    {
        var creates = 0;
        var vm = host.Invoke(() => VisionFlowToolboxTests.Create(() => creates++));
        try
        {
            host.Invoke(() =>
            {
                var node = vm.Add(vm.ToolEntries.Single(e => e.Name == tool));
                Assert.Equal(typeName, node.GetType().FullName);
                Assert.Equal("Vision.FlowNode.VisionToolNodeBase", node.GetType().BaseType!.FullName);
                Assert.True(node.GetType().BaseType.IsAbstract);
                var workspace = Assert.IsType<VisionToolInstanceWorkspace>(node.Session);
                Assert.Same(workspace.Parameters, workspace.Editor.CurrentParameters);
                var definition = Assert.IsType<VisionNodeDefinition>(node.GetType().GetProperty("Definition")!.GetValue(node));
                Assert.Same(VisionNodeDefinitions.Get(tool), definition);
                Assert.Contains(parameter, definition.Parameters);
                vm.Select(node);
                var row = Assert.Single(vm.PropertyGridVM.Groups.SelectMany(g => g.Items), p => p.PropertyName == parameter);
                var property = workspace.Parameters.GetType().GetProperty(parameter)!;
                vm.Chart.Model.IsModified = false;
                row.Value = property.PropertyType.IsEnum ? Enum.ToObject(property.PropertyType, value) : value;
                Assert.Equal(value, Convert.ToInt32(property.GetValue(workspace.Parameters)));
                Assert.True(vm.Chart.Model.IsModified);
                Assert.Same(workspace.Editor, vm.SelectedEditor);
                Assert.Equal(1, creates);
                Assert.Null(node.Result);

                // 与原通用节点比较端口契约，不把“类名不同”当成工具接入正确的证据。
                var legacyType = typeof(VisionNodeDefinitions).Assembly.GetType("Vision.FlowNode.VisionToolNode")!;
                var entry = vm.ToolEntries.Single(e => e.Name == tool);
                var catalog = new VisionBrowserCatalog([entry], [], _ => throw new NotSupportedException(), () => null);
                using var legacy = (IDisposable)Activator.CreateInstance(legacyType, new VisionToolInstanceWorkspace(catalog, entry))!;
                var old = (FlowEditor.Controls.Nodes.NodeModel)legacy;
                old.IsInEnabled = node.IsInEnabled;
                old.IsOutEnabled = node.IsOutEnabled;
                Assert.Equal(old.InputPorts.Select(p => (p.Name, p.PropertyName)), node.InputPorts.Select(p => (p.Name, p.PropertyName)));
                Assert.Equal(old.OutputPorts.Select(p => (p.Name, p.PropertyName)), node.OutputPorts.Select(p => (p.Name, p.PropertyName)));
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task ConcreteAndLegacyDocuments_RestoreTypesIdsConnectionsAndOriginalParameters()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".flow");
        var vm = host.Invoke(() => VisionFlowToolboxTests.Create());
        var loaded = host.Invoke(() => VisionFlowToolboxTests.Create());
        try
        {
            host.Invoke(() =>
            {
                var blob = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Blob"));
                var line = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/FindLine"));
                var fallback = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Morphology"));
                var circle = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/FindCircle"));
                var template = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/TemplateMatch"));
                foreach (var (identity, typeName) in new[]
                {
                    ("OpenCV/Threshold", "Vision.FlowNode.OpenCv.ThresholdNode"),
                    ("OpenCV/GrayStatistics", "Vision.FlowNode.OpenCv.GrayStatisticsNode"),
                    ("Common/Geometry", "Vision.FlowNode.Common.GeometryNode"),
                    ("Common/Tolerance", "Vision.FlowNode.Common.ToleranceNode")
                })
                    Assert.Equal(typeName, vm.Add(vm.ToolEntries.Single(e => e.Name == identity)).GetType().FullName);
                Assert.Equal("Vision.FlowNode.OpenCv.FindCircleNode", circle.GetType().FullName);
                Assert.Equal("Vision.FlowNode.OpenCv.TemplateMatchNode", template.GetType().FullName);
                circle.CircleOutputField = "Circle";
                circle.PointOutputField = "Center";
                var templateWorkspace = Assert.IsType<VisionToolInstanceWorkspace>(template.Session);
                templateWorkspace.Editor.TemplateEditor!.Capture(VisionImage.FromBuffer(4, 4,
                    Vision.Core.Imaging.PixelFormat.Mono8, Enumerable.Range(0, 16).Select(i => (byte)i).ToArray()));
                Assert.Equal("Vision.FlowNode.OpenCv.BlobNode", blob.GetType().FullName);
                Assert.Equal("Vision.FlowNode.OpenCv.FindLineNode", line.GetType().FullName);
                Assert.Equal("Vision.FlowNode.VisionToolNode", fallback.GetType().FullName);
                ((Vision.Tools.OpenCv.Inspection.BlobParameters)blob.Session.Parameters).MinGray = 89;
                vm.SetImage(VisionImage.FromBuffer(16, 16, Vision.Core.Imaging.PixelFormat.Mono8, new byte[256]));
                vm.Chart.Connections.Add(new() { SourceNodeId = blob.Id,
                    SourcePortId = blob.OutputPorts.Single(p => p.Name == "Next_Out").Id,
                    TargetNodeId = line.Id, TargetPortId = line.InputPorts.Single(p => p.Name == "Next_IN").Id });
                vm.Chart.Connections.Add(new() { SourceNodeId = fallback.Id,
                    SourcePortId = fallback.OutputPorts.Single(p => p.PropertyName == "OutputImage").Id,
                    TargetNodeId = blob.Id, TargetPortId = blob.InputPorts.Single(p => p.PropertyName == "Image").Id });
                Assert.True(vm.Chart.SaveDocument(path));
                Assert.True(loaded.Chart.LoadDocument(path));
                Assert.Equal(vm.Chart.Nodes.Select(n => n.Model.GetType()), loaded.Chart.Nodes.Select(n => n.Model.GetType()));
                Assert.Equal(vm.Chart.Nodes.Select(n => n.Id), loaded.Chart.Nodes.Select(n => n.Id));
                Assert.Equal(vm.Chart.Connections.Select(c => (c.Id, c.SourcePortId, c.TargetPortId)),
                    loaded.Chart.Connections.Select(c => (c.Id, c.SourcePortId, c.TargetPortId)));
                var loadedSession = (Vision.Core.Tools.IVisionToolSession)loaded.Chart.Nodes[0].Model.GetType().GetProperty("Session")!.GetValue(loaded.Chart.Nodes[0].Model)!;
                Assert.NotSame(blob.Session.Parameters, loadedSession.Parameters);
                Assert.Equal(89, Assert.IsType<Vision.Tools.OpenCv.Inspection.BlobParameters>(loadedSession.Parameters).MinGray);
                Assert.All(loaded.Chart.Nodes, n => Assert.Equal(0, n.Model.Count));
                var restoredCircle = Assert.IsAssignableFrom<VisionToolNodeBase>(loaded.Chart.Nodes[3].Model);
                Assert.Equal("Circle", restoredCircle.CircleOutputField);
                Assert.Equal("Center", restoredCircle.PointOutputField);
                var restoredTemplate = Assert.IsType<VisionToolInstanceWorkspace>(Assert.IsAssignableFrom<VisionToolNodeBase>(loaded.Chart.Nodes[4].Model).Session);
                Assert.Equal(Enumerable.Range(0, 16).Select(i => (byte)i), restoredTemplate.Editor.TemplateEditor!.TemplateImage!.Pixels);
                Assert.NotSame(templateWorkspace.Editor.TemplateEditor.TemplateImage!.Pixels, restoredTemplate.Editor.TemplateEditor.TemplateImage.Pixels);

                var document = JsonNode.Parse(File.ReadAllText(path))!;
                var legacyName = typeof(VisionNodeDefinitions).Assembly.GetType("Vision.FlowNode.VisionToolNode")!.AssemblyQualifiedName;
                foreach (var item in document["nodes"]!.AsArray())
                {
                    item!["nodeTypeId"] = "vision.tool.instance";
                    item["typeName"] = legacyName;
                }
                File.WriteAllText(path, document.ToJsonString());
                Assert.True(loaded.Chart.LoadDocument(path));
                Assert.All(loaded.Chart.Nodes, n => Assert.Equal("Vision.FlowNode.VisionToolNode", n.Model.GetType().FullName));
                var restored = (Vision.Core.Tools.IVisionToolSession)loaded.Chart.Nodes[0].Model.GetType().GetProperty("Session")!.GetValue(loaded.Chart.Nodes[0].Model)!;
                Assert.Equal(89, Assert.IsType<Vision.Tools.OpenCv.Inspection.BlobParameters>(restored.Parameters).MinGray);
                Assert.Equal(blob.Id, loaded.Chart.Nodes[0].Id);
                Assert.Equal(2, loaded.Chart.Connections.Count);
                var legacyCircle = Assert.IsAssignableFrom<VisionToolNodeBase>(loaded.Chart.Nodes[3].Model);
                Assert.Equal("Circle", legacyCircle.CircleOutputField);
                Assert.Equal("Center", legacyCircle.PointOutputField);
                var legacyTemplate = Assert.IsType<VisionToolInstanceWorkspace>(Assert.IsAssignableFrom<VisionToolNodeBase>(loaded.Chart.Nodes[4].Model).Session);
                Assert.Equal(Enumerable.Range(0, 16).Select(i => (byte)i), legacyTemplate.Editor.TemplateEditor!.TemplateImage!.Pixels);
                Assert.Null(restored.Result);
            });
        }
        finally
        {
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            await host.Invoke(() => loaded.DisposeAsync().AsTask());
            File.Delete(path);
        }
    }

    [Fact]
    public async Task UnmigratedTool_KeepsOriginalHostParameterProjection()
    {
        var vm = host.Invoke(() => VisionFlowToolboxTests.Create(null, c => c with
        {
            Tools = c.Tools.Select(e => e.Name == "OpenCV/Morphology"
                ? e with { NodeParameterNames = ["KernelSize"] } : e).ToArray()
        }));
        try
        {
            host.Invoke(() =>
            {
                var node = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Morphology"));
                Assert.Equal("Vision.FlowNode.VisionToolNode", node.GetType().FullName);
                vm.Select(node);
                Assert.Equal(new[] { "KernelSize" }, vm.PropertyGridVM.Groups.Single(g => g.GroupName == "工具关键参数").Items.Select(i => i.PropertyName));
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData("vision.opencv.find-line", "Vision.FlowNode.OpenCv.FindLineNode")]
    [InlineData("vision.opencv.find-circle", "Vision.FlowNode.OpenCv.FindCircleNode")]
    [InlineData("vision.opencv.template-match", "Vision.FlowNode.OpenCv.TemplateMatchNode")]
    [InlineData("vision.opencv.threshold", "Vision.FlowNode.OpenCv.ThresholdNode")]
    [InlineData("vision.opencv.gray-statistics", "Vision.FlowNode.OpenCv.GrayStatisticsNode")]
    [InlineData("vision.common.geometry", "Vision.FlowNode.Common.GeometryNode")]
    [InlineData("vision.common.tolerance", "Vision.FlowNode.Common.ToleranceNode")]
    public async Task ConcreteTypeWithWrongToolConfiguration_IsRejectedWithoutReplacingCurrentNodes(string typeId, string typeName)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".flow");
        var vm = host.Invoke(() => VisionFlowToolboxTests.Create());
        try
        {
            host.Invoke(() =>
            {
                var blob = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Blob"));
                Assert.Equal("Vision.FlowNode.OpenCv.BlobNode", blob.GetType().FullName);
                Assert.True(vm.Chart.SaveDocument(path));
                var document = JsonNode.Parse(File.ReadAllText(path))!;
                document["nodes"]![0]!["nodeTypeId"] = typeId;
                document["nodes"]![0]!["typeName"] = typeName + ", Vision.FlowNode";
                File.WriteAllText(path, document.ToJsonString());
                var error = Assert.Throws<InvalidOperationException>(() => vm.Chart.LoadDocument(path));
                Assert.Contains("节点类型与工具配置不匹配", error.Message);
                Assert.Same(blob, Assert.Single(vm.Chart.Nodes).Model);
                Assert.True(((VisionToolInstanceWorkspace)blob.Session).Editor.IsSessionActive);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); File.Delete(path); }
    }
}
