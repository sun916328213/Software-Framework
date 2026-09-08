using WPFCustomControls.PropertyGrid.Models;

namespace WPFCustomControls.PropertyGrid.Internal;

/// <summary>Centralizes binding lookup and replace/remove invariants.</summary>
internal sealed class PortBindingCoordinator
{
    private readonly IDataBindingProvider _provider;

    public PortBindingCoordinator(IDataBindingProvider provider) => _provider = provider;

    public IReadOnlyList<DataBindingInfo> Bindings => _provider.GetDataBindings();
    public IReadOnlyList<UpstreamNodeInfo> UpstreamNodes => _provider.GetUpstreamFlowNodes();
    public IReadOnlyList<string> GetUpstreamPorts(string nodeId) => _provider.GetUpstreamOutputPorts(nodeId);

    public DataBindingInfo? Find(string portId, string propertyName) =>
        Find(Bindings, portId, propertyName);

    public static DataBindingInfo? Find(
        IEnumerable<DataBindingInfo> bindings,
        string portId,
        string propertyName) =>
        bindings.FirstOrDefault(binding => binding.TargetPortId == portId)
        ?? bindings.FirstOrDefault(binding => binding.PropertyName == propertyName);

    public void Create(PortDialogResult result, string targetPortName)
    {
        if (result is not
            {
                IsUpstreamData: true,
                UpstreamNodeId: not null,
                UpstreamPortName: not null
            })
            return;
        _provider.CreateDataBinding(result.UpstreamNodeId, result.UpstreamPortName, targetPortName);
    }

    public void Replace(DataBindingInfo? existing, PortDialogResult result, string targetPortName)
    {
        if (result is not
            {
                IsUpstreamData: true,
                UpstreamNodeId: not null,
                UpstreamPortName: not null
            })
            return;
        if (existing != null)
            _provider.RemoveDataBinding(existing.BindingId);
        _provider.CreateDataBinding(result.UpstreamNodeId, result.UpstreamPortName, targetPortName);
    }

    public bool Remove(string portId, string propertyName)
    {
        var binding = Find(portId, propertyName);
        if (binding == null)
            return false;
        _provider.RemoveDataBinding(binding.BindingId);
        return true;
    }
}
