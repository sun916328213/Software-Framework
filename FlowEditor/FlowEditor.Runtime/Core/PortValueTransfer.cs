namespace FlowEditor.Core;

/// <summary>集中执行端口查找、类型验证、诊断生成和值写入。</summary>
public static class PortValueTransfer
{
    /// <summary>
    /// 按端口 ID 把源节点的当前值传入目标节点；任一端口不存在时返回 <see langword="false"/>。
    /// 类型不兼容时抛出 <see cref="FlowChartTypeMismatchException"/>，目标端口保持原值。
    /// </summary>
    /// <param name="sourceNode">提供输出值的源节点。</param>
    /// <param name="sourcePortId">源端口 ID。</param>
    /// <param name="targetNode">接收输入值的目标节点。</param>
    /// <param name="targetPortId">目标端口 ID。</param>
    /// <param name="transferredValue">成功传递的值，可为 <see langword="null"/>。</param>
    /// <returns>两个端口均存在且完成传递时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
    public static bool TryTransfer(
        INodeExecutable sourceNode,
        string sourcePortId,
        INodeExecutable targetNode,
        string targetPortId,
        out object? transferredValue)
    {
        transferredValue = null;
        var sourcePort = sourceNode.FindPortById(sourcePortId);
        var targetPort = targetNode.FindPortById(targetPortId);
        if (sourcePort == null || targetPort == null)
            return false;

        if (!targetPort.IsTypeCompatibleWith(sourcePort.DataType))
        {
            throw new FlowChartTypeMismatchException(
                $"源端口 [{sourceNode.Name}].{sourcePort.Name}({sourcePort.DataType}) " +
                $"与目标端口 [{targetNode.Name}].{targetPort.Name}({targetPort.DataType}) 声明类型不兼容");
        }

        if (!targetPort.IsCompatible(sourcePort.Value))
        {
            var sourceValueType = sourcePort.Value?.GetType().Name ?? "null";
            throw new FlowChartTypeMismatchException(
                $"源端口 [{sourceNode.Name}].{sourcePort.Name} 的值 " +
                $"'{sourcePort.Value}'（运行时类型: {sourceValueType}）" +
                $"无法传入目标端口 [{targetNode.Name}].{targetPort.Name}({targetPort.DataType})");
        }

        transferredValue = sourcePort.Value;
        targetPort.Value = transferredValue;
        return true;
    }
}
