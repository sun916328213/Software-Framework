// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ModbusReadBatchPlanner、PlannedPoint。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Interfaces。导入不代表所有类型都会在运行时加载。
using Communication.Core.Interfaces;

namespace Communication.Runtime.Protocols.Modbus;

/// <summary>
/// 将可读 Modbus 点位规划为尽可能少的连续读取请求。
/// </summary>
public static class ModbusReadBatchPlanner
{
    /// <summary>Modbus 读保持/输入寄存器单次请求的协议上限。</summary>
    public const ushort ProtocolMaximumRegisters = 125;

    /// <summary>Modbus 读线圈/离散输入单次请求的协议上限。</summary>
    public const ushort ProtocolMaximumBits = 2000;

    /// <summary>
    /// 按区域和地址排序点位，再合并相邻或重叠的元素范围。
    /// </summary>
    /// <param name="points">待规划的数据点。不可读点位会被忽略。</param>
    /// <param name="maximumRegistersPerRequest">单次请求寄存器上限，不得超过 125。</param>
    /// <param name="maximumBitsPerRequest">单次请求位元素上限，不得超过 2000。</param>
    public static IReadOnlyList<ModbusReadBatch> Plan(
        IEnumerable<IDataPoint> points,
        ushort maximumRegistersPerRequest = ProtocolMaximumRegisters,
        ushort maximumBitsPerRequest = ProtocolMaximumBits)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (maximumRegistersPerRequest is 0 or > ProtocolMaximumRegisters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumRegistersPerRequest),
                "Modbus 单次寄存器读取上限必须在 1 到 125 之间。");
        }
        if (maximumBitsPerRequest is 0 or > ProtocolMaximumBits)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumBitsPerRequest),
                "Modbus 单次位读取上限必须在 1 到 2000 之间。");
        }

        var plannedPoints = points
            .Where(static point => point.Readable)
            .Select(CreatePlannedPoint)
            .OrderBy(static point => point.Region)
            .ThenBy(static point => point.StartAddress)
            .ToArray();
        var batches = new List<ModbusReadBatch>();
        var batchPoints = new List<PlannedPoint>();

        foreach (var point in plannedPoints)
        {
            var maximumElements = IsBitRegion(point.Region)
                ? maximumBitsPerRequest
                : maximumRegistersPerRequest;
            if (point.EndAddressExclusive - point.StartAddress > maximumElements)
            {
                throw new ArgumentException(
                    $"数据点 {point.Point.Name} 单独占用 " +
                    $"{point.EndAddressExclusive - point.StartAddress} 个元素，" +
                    $"超过当前单次请求上限 {maximumElements}。",
                    nameof(points));
            }

            if (batchPoints.Count == 0)
            {
                batchPoints.Add(point);
                continue;
            }

            var first = batchPoints[0];
            var currentEnd = batchPoints.Max(static item => item.EndAddressExclusive);
            var combinedEnd = Math.Max(currentEnd, point.EndAddressExclusive);
            if (point.Region == first.Region &&
                point.StartAddress <= currentEnd &&
                combinedEnd - first.StartAddress <= maximumElements)
            {
                batchPoints.Add(point);
                continue;
            }

            batches.Add(CreateBatch(batchPoints));
            batchPoints.Clear();
            batchPoints.Add(point);
        }

        if (batchPoints.Count > 0)
        {
            batches.Add(CreateBatch(batchPoints));
        }

        return batches;
    }

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreatePlannedPoint。
    /// 正文中的关键协作调用包括 ThrowIfNull、nameof、Parse、IsBitRegion；具体条件和先后次序见方法体。
    /// </summary>
    private static PlannedPoint CreatePlannedPoint(IDataPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        if (point.Length <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(point),
                $"数据点 {point.Name} 的字节长度必须大于零。");
        }

        var address = ModbusAddressValue.Parse(point.Address);
        if (IsBitRegion(address.Region))
        {
            if (point.DataType != DataPointType.Bool || point.Length != 1)
            {
                throw new ArgumentException(
                    $"位区域数据点 {point.Name} 必须是长度为 1 的 Bool。",
                    nameof(point));
            }

            return new PlannedPoint(
                point,
                address.Region,
                address.StartAddress,
                address.StartAddress + 1);
        }

        var registerCount = checked((point.Length + 1) / 2);
        var endAddressExclusive = address.StartAddress + registerCount;
        if (endAddressExclusive > ushort.MaxValue + 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(point),
                $"数据点 {point.Name} 的寄存器范围超出 0..65535。");
        }

        return new PlannedPoint(
            point,
            address.Region,
            address.StartAddress,
            endAddressExclusive);
    }

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateBatch。
    /// 正文中的关键协作调用包括 Max、ToArray、Select、IsBitRegion；具体条件和先后次序见方法体。
    /// </summary>
    private static ModbusReadBatch CreateBatch(IReadOnlyList<PlannedPoint> points)
    {
        var first = points[0];
        var endAddressExclusive = points.Max(static point => point.EndAddressExclusive);
        var slices = points
            .Select(point => IsBitRegion(first.Region)
                ? new ModbusReadSlice(
                    point.Point,
                    byteOffset: 0,
                    byteLength: 1,
                    bitOffset: point.StartAddress - first.StartAddress)
                : new ModbusReadSlice(
                    point.Point,
                    checked((point.StartAddress - first.StartAddress) * 2),
                    point.Point.Length))
            .ToArray();
        return new ModbusReadBatch(
            first.Region,
            first.StartAddress,
            checked((ushort)(endAddressExclusive - first.StartAddress)),
            slices);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 IsBitRegion。
    /// </summary>
    private static bool IsBitRegion(ModbusRegion region) =>
        region is ModbusRegion.Coil or ModbusRegion.DiscreteInput;

    /// <summary>
    /// PlannedPoint 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
    /// </summary>
    private sealed record PlannedPoint(
        IDataPoint Point,
        ModbusRegion Region,
        ushort StartAddress,
        int EndAddressExclusive);
}
