// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：PortManagementViewModelTests、PortModel、PortTarget、DialogStub。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System.Collections、System.ComponentModel、WPFCustomControls.PropertyGrid.Core.Models、WPFCustomControls.PropertyGrid.Core.ViewModels。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System.Collections;
using System.ComponentModel;
using WPFCustomControls.PropertyGrid.Core.Models;
using WPFCustomControls.PropertyGrid.Core.ViewModels;

namespace WPFCustomControls.Tests.PropertyGrid;

/// <summary>
/// PortManagementViewModelTests 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class PortManagementViewModelTests
{
    /// <summary>
    /// 验证 Constructor_ProjectsPortsAndExistingBinding 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Add、Single、True、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 AddInputPortCommand_AddsCustomPortFromDialogResult 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Execute、Equal、True；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 AddOutputPortCommand_PreservesSelectedProperty 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Execute、Equal、Single、False；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 ModifyBinding_ReplacesExistingBinding 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 BoundTarget、Execute、Equal、Single；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 DeleteBoundPort_RemovesBindingBeforePort 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 BoundTarget、Execute、Equal、Empty；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 UnlockingInputRegion_UpdatesExistingRows 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Add、Execute、False；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 ReloadAndDispose_DoNotAccumulateModelSubscriptions 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Add、Equal、RaiseBindingsChanged、Dispose；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 BoundTarget 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// PortModel 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：INotifyPropertyChanged。具体约束按这些契约及各成员实现解释。
    /// </summary>
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

    /// <summary>
    /// PortTarget 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IPortManageable、IDataBindingProvider、IPortConfigurable。具体约束按这些契约及各成员实现解释。
    /// </summary>
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

        /// <summary>
        /// 验证 AddCustomInputPort 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public void AddCustomInputPort(string name) => AddedInputs.Add(name);
        /// <summary>
        /// 验证 AddCustomOutputPort 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public void AddCustomOutputPort(string name, string? boundProperty = null) =>
            AddedOutputs.Add((name, boundProperty));
        /// <summary>
        /// 验证 RemoveCustomPort 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public bool RemoveCustomPort(string portId)
        {
            RemovedPorts.Add(portId);
            return true;
        }
        /// <summary>
        /// 验证 InvalidatePorts 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public void InvalidatePorts() { }
        /// <summary>
        /// 验证 GetUpstreamFlowNodes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public IReadOnlyList<UpstreamNodeInfo> GetUpstreamFlowNodes() => UpstreamNodes;
        /// <summary>
        /// 验证 GetUpstreamOutputPorts 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public IReadOnlyList<string> GetUpstreamOutputPorts(string upstreamNodeId) => ["Output", "OtherOutput"];
        /// <summary>
        /// 验证 CreateDataBinding 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public void CreateDataBinding(string upstreamNodeId, string upstreamPortName, string targetPortName) =>
            CreatedBindings.Add((upstreamNodeId, upstreamPortName, targetPortName));
        /// <summary>
        /// 验证 GetDataBindings 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public IReadOnlyList<DataBindingInfo> GetDataBindings() => Bindings;
        /// <summary>
        /// 验证 RemoveDataBinding 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public void RemoveDataBinding(string bindingId) => RemovedBindings.Add(bindingId);
        /// <summary>
        /// 验证 GetPortConfig 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public (bool IsInput, bool IsOutput) GetPortConfig(string propertyName) => default;
        /// <summary>
        /// 验证 SetPortConfig 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ConfigChanged；具体条件和先后次序见方法体。
        /// </summary>
        public void SetPortConfig(string propertyName, bool isInput, bool isOutput) => ConfigChanged();
        /// <summary>
        /// 验证 RaiseBindingsChanged 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public void RaiseBindingsChanged() => DataBindingsChanged?.Invoke();
    }

    /// <summary>
    /// DialogStub 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IPortDialogService。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class DialogStub : IPortDialogService
    {
        public PortDialogResult? AddResult { get; init; }
        public PortDialogResult? ModifyResult { get; init; }
        public PortDialogRequest? LastAddRequest { get; private set; }

        /// <summary>
        /// 验证 ShowAddPortDialog 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public PortDialogResult? ShowAddPortDialog(PortDialogRequest request)
        {
            LastAddRequest = request;
            return AddResult;
        }

        /// <summary>
        /// 验证 ShowModifyBindingDialog 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public PortDialogResult? ShowModifyBindingDialog(PortDialogRequest request) => ModifyResult;
    }
}
