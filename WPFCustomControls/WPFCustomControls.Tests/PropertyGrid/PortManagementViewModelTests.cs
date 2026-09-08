using System.Collections;
using System.ComponentModel;
using WPFCustomControls.PropertyGrid.Models;
using WPFCustomControls.PropertyGrid.ViewModels;

namespace WPFCustomControls.Tests.PropertyGrid;

public sealed class PortManagementViewModelTests
{
    [Fact]
    public void Constructor_ProjectsPortsAndExistingBinding()
    {
        var target = new PortTarget();
        target.Inputs.Add(new PortModel { Id = "in-1", Name = "Input" });
        target.Outputs.Add(new PortModel { Id = "out-1", Name = "Output", PropertyName = "Result" });
        target.Bindings.Add(new DataBindingInfo
        {
            BindingId = "binding-1",
            TargetPortId = "in-1",
            UpstreamNodeId = "node-1",
            UpstreamNodeName = "Source",
            PropertyName = "Value",
            SourcePortName = "Output"
        });

        using var viewModel = new PortManagementViewModel(target, ["Result"], new DialogStub());

        Assert.Single(viewModel.InputPorts);
        Assert.True(viewModel.InputPorts[0].IsBound);
        Assert.Equal("已绑定: Source.Value", viewModel.InputPorts[0].BindingStatusText);
        Assert.Single(viewModel.OutputPorts);
        Assert.Equal("Result", viewModel.OutputPorts[0].BoundPropertyName);
    }

    [Fact]
    public void AddInputPortCommand_AddsCustomPortFromDialogResult()
    {
        var target = new PortTarget();
        var dialog = new DialogStub
        {
            AddResult = new PortDialogResult { PortName = "NewInput" }
        };
        using var viewModel = new PortManagementViewModel(target, [], dialog);

        viewModel.AddInputPortCommand.Execute(null);

        Assert.Equal(["NewInput"], target.AddedInputs);
        Assert.True(dialog.LastAddRequest!.IsInputPort);
    }

    [Fact]
    public void AddOutputPortCommand_PreservesSelectedProperty()
    {
        var target = new PortTarget();
        var dialog = new DialogStub
        {
            AddResult = new PortDialogResult
            {
                PortName = "NewOutput",
                IsToolProperty = true,
                BoundPropertyName = "Result"
            }
        };
        using var viewModel = new PortManagementViewModel(target, ["Result"], dialog);

        viewModel.AddOutputPortCommand.Execute(null);

        Assert.Equal(("NewOutput", "Result"), Assert.Single(target.AddedOutputs));
        Assert.False(dialog.LastAddRequest!.IsInputPort);
    }

    [Fact]
    public void ModifyBinding_ReplacesExistingBinding()
    {
        var target = BoundTarget();
        var dialog = new DialogStub
        {
            ModifyResult = new PortDialogResult
            {
                IsUpstreamData = true,
                UpstreamNodeId = "node-2",
                UpstreamPortName = "OtherOutput"
            }
        };
        using var viewModel = new PortManagementViewModel(target, [], dialog);

        viewModel.InputPorts[0].ModifyBindingCommand.Execute(null);

        Assert.Equal(["binding-1"], target.RemovedBindings);
        Assert.Equal(("node-2", "OtherOutput", "Input"), Assert.Single(target.CreatedBindings));
    }

    [Fact]
    public void DeleteBoundPort_RemovesBindingBeforePort()
    {
        var target = BoundTarget();
        using var viewModel = new PortManagementViewModel(target, [], new DialogStub());

        viewModel.InputPorts[0].DeleteCommand.Execute(null);

        Assert.Equal(["binding-1"], target.RemovedBindings);
        Assert.Equal(["in-1"], target.RemovedPorts);
        Assert.Empty(viewModel.InputPorts);
    }

    [Fact]
    public void UnlockingInputRegion_UpdatesExistingRows()
    {
        var target = new PortTarget();
        target.Inputs.Add(new PortModel { Id = "in-1", Name = "Input" });
        using var viewModel = new PortManagementViewModel(target, [], new DialogStub());

        viewModel.ToggleInputLockCommand.Execute(null);

        Assert.False(viewModel.IsInputLocked);
        Assert.False(viewModel.InputPorts[0].IsReadOnly);
    }

    [Fact]
    public void ReloadAndDispose_DoNotAccumulateModelSubscriptions()
    {
        var target = new PortTarget();
        var port = new PortModel { Id = "in-1", Name = "Input" };
        target.Inputs.Add(port);
        var viewModel = new PortManagementViewModel(target, [], new DialogStub());
        Assert.Equal(1, port.SubscriberCount);

        target.RaiseBindingsChanged();
        target.RaiseBindingsChanged();

        Assert.Equal(1, port.SubscriberCount);
        viewModel.Dispose();
        Assert.Equal(0, port.SubscriberCount);
    }

    private static PortTarget BoundTarget()
    {
        var target = new PortTarget();
        target.Inputs.Add(new PortModel { Id = "in-1", Name = "Input" });
        target.UpstreamNodes.Add(new UpstreamNodeInfo { NodeId = "node-1", NodeName = "Source" });
        target.Bindings.Add(new DataBindingInfo
        {
            BindingId = "binding-1",
            TargetPortId = "in-1",
            UpstreamNodeId = "node-1",
            UpstreamNodeName = "Source",
            PropertyName = "Value",
            SourcePortName = "Output"
        });
        return target;
    }

    private sealed class PortModel : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _propertyChanged;

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                _propertyChanged += value;
                SubscriberCount++;
            }
            remove
            {
                _propertyChanged -= value;
                SubscriberCount--;
            }
        }

        public int SubscriberCount { get; private set; }
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? PropertyName { get; set; }
        public object? Value { get; set; }
    }

    private sealed class PortTarget : IPortManageable, IDataBindingProvider, IPortConfigurable
    {
        public List<PortModel> Inputs { get; } = [];
        public List<PortModel> Outputs { get; } = [];
        public List<DataBindingInfo> Bindings { get; } = [];
        public List<UpstreamNodeInfo> UpstreamNodes { get; } = [];
        public List<string> AddedInputs { get; } = [];
        public List<(string Name, string? Property)> AddedOutputs { get; } = [];
        public List<(string Node, string Source, string Target)> CreatedBindings { get; } = [];
        public List<string> RemovedBindings { get; } = [];
        public List<string> RemovedPorts { get; } = [];

        public IEnumerable CustomInputPorts => Inputs;
        public IEnumerable CustomOutputPorts => Outputs;
        public event Action? DataBindingsChanged;
        public event Action ConfigChanged = delegate { };

        public void AddCustomInputPort(string name) => AddedInputs.Add(name);
        public void AddCustomOutputPort(string name, string? boundProperty = null) =>
            AddedOutputs.Add((name, boundProperty));
        public bool RemoveCustomPort(string portId)
        {
            RemovedPorts.Add(portId);
            return true;
        }
        public void InvalidatePorts() { }
        public IReadOnlyList<UpstreamNodeInfo> GetUpstreamFlowNodes() => UpstreamNodes;
        public IReadOnlyList<string> GetUpstreamOutputPorts(string upstreamNodeId) => ["Output", "OtherOutput"];
        public void CreateDataBinding(string upstreamNodeId, string upstreamPortName, string targetPortName) =>
            CreatedBindings.Add((upstreamNodeId, upstreamPortName, targetPortName));
        public IReadOnlyList<DataBindingInfo> GetDataBindings() => Bindings;
        public void RemoveDataBinding(string bindingId) => RemovedBindings.Add(bindingId);
        public (bool IsInput, bool IsOutput) GetPortConfig(string propertyName) => default;
        public void SetPortConfig(string propertyName, bool isInput, bool isOutput) => ConfigChanged();
        public void RaiseBindingsChanged() => DataBindingsChanged?.Invoke();
    }

    private sealed class DialogStub : IPortDialogService
    {
        public PortDialogResult? AddResult { get; init; }
        public PortDialogResult? ModifyResult { get; init; }
        public PortDialogRequest? LastAddRequest { get; private set; }

        public PortDialogResult? ShowAddPortDialog(PortDialogRequest request)
        {
            LastAddRequest = request;
            return AddResult;
        }

        public PortDialogResult? ShowModifyBindingDialog(PortDialogRequest request) => ModifyResult;
    }
}
