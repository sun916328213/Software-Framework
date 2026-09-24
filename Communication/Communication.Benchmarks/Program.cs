using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using CommunicationNew.Core.Interfaces;
using CommunicationNew.Core.Models;
using CommunicationNew.Protocols.Modbus;

namespace CommunicationNew.Benchmarks;

internal static class Program
{
    private const int PointCount = 100;
    private const int DefaultIterations = 10_000;
    private const int WarmupIterations = 200;

    private static int Main(string[] args)
    {
        if (!TryGetIterations(args, out var iterations))
        {
            Console.Error.WriteLine("迭代次数必须是大于 0 的整数。");
            return 1;
        }

        var points = CreateAdjacentRegisterPoints();
        var response = CreateRegisterResponse();
        var initialBatches = ModbusReadBatchPlanner.Plan(points);

        // 先让 JIT 和静态初始化退出计时区，降低首次运行噪声。
        RunPipeline(points, response, WarmupIterations);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        var extractedValues = RunPipeline(points, response, iterations);
        stopwatch.Stop();
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        var elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        var result = new BenchmarkResult(
            TimestampUtc: DateTimeOffset.UtcNow,
            Framework: RuntimeInformation.FrameworkDescription,
            OperatingSystem: RuntimeInformation.OSDescription,
            ProcessorCount: Environment.ProcessorCount,
            Points: PointCount,
            Iterations: iterations,
            RequestsWithoutBatching: PointCount,
            RequestsWithBatching: initialBatches.Count,
            RequestReductionPercent: 100d * (PointCount - initialBatches.Count) / PointCount,
            TotalElapsedMilliseconds: elapsedMilliseconds,
            MeanMicrosecondsPerIteration: elapsedMilliseconds * 1_000d / iterations,
            IterationsPerSecond: iterations / stopwatch.Elapsed.TotalSeconds,
            AllocatedBytesPerIteration: (double)allocatedBytes / iterations,
            ExtractedValues: extractedValues,
            Scope: "批量规划和响应切片提取；不包含网络、PLC 或串口 IO 延迟");

        Console.WriteLine(JsonSerializer.Serialize(
            result,
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static long RunPipeline(
        IReadOnlyList<IDataPoint> points,
        byte[] response,
        int iterations)
    {
        long extractedValues = 0;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var batches = ModbusReadBatchPlanner.Plan(points);
            foreach (var batch in batches)
            {
                extractedValues += batch.ExtractResponse(response).Count;
            }
        }

        return extractedValues;
    }

    private static IDataPoint[] CreateAdjacentRegisterPoints() =>
        Enumerable.Range(1, PointCount)
            .Select(number => (IDataPoint)new DataPoint(
                $"Register{number}",
                DataPointType.Int16,
                $"HR{number}"))
            .ToArray();

    private static byte[] CreateRegisterResponse()
    {
        var response = new byte[PointCount * 2];
        for (var index = 0; index < PointCount; index++)
        {
            response[index * 2] = (byte)(index >> 8);
            response[index * 2 + 1] = (byte)index;
        }

        return response;
    }

    private static bool TryGetIterations(string[] args, out int iterations)
    {
        iterations = DefaultIterations;
        return args.Length switch
        {
            0 => true,
            1 => int.TryParse(args[0], out iterations) && iterations > 0,
            _ => false
        };
    }

    private sealed record BenchmarkResult(
        DateTimeOffset TimestampUtc,
        string Framework,
        string OperatingSystem,
        int ProcessorCount,
        int Points,
        int Iterations,
        int RequestsWithoutBatching,
        int RequestsWithBatching,
        double RequestReductionPercent,
        double TotalElapsedMilliseconds,
        double MeanMicrosecondsPerIteration,
        double IterationsPerSecond,
        double AllocatedBytesPerIteration,
        long ExtractedValues,
        string Scope);
}
