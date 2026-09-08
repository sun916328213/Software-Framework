using System.Buffers.Binary;
using CommunicationNew.Protocols.Mitsubishi;

namespace CommunicationNew.MxComponentSmokeTest;

internal static class Program
{
    private const string PasswordEnvironmentVariable = "COMMUNICATIONNEW_MX_PASSWORD";

    private static async Task<int> Main(string[] args)
    {
        try
        {
            var options = SmokeOptions.Parse(args);
            if (options.ShowHelp)
            {
                PrintHelp();
                return 0;
            }

            var parameter = new McParam
            {
                Backend = McClientBackend.MxComponent,
                MxLogicalStationNumber = options.LogicalStationNumber,
                MxPassword = Environment.GetEnvironmentVariable(PasswordEnvironmentVariable) ?? string.Empty,
                MxBridgeExecutablePath = options.BridgeExecutablePath,
                TimeoutMs = options.TimeoutMilliseconds,
                RetryCount = 1,
                AutoReconnect = false,
            };
            await using var device = new McClientDevice(parameter);
            Exception? connectionError = null;
            device.ErrorOccurred += (_, eventArgs) => connectionError = eventArgs.Exception;
            if (!await device.ConnectAsync().ConfigureAwait(false))
            {
                throw connectionError ?? new IOException("MX Component 连接失败。");
            }

            Console.WriteLine(
                $"已连接 MX Component 逻辑站 {options.LogicalStationNumber}，" +
                $"模式：{(string.IsNullOrWhiteSpace(options.BridgeExecutablePath) ? "直接" : "桥接")}。");
            if (options.HasWrite)
            {
                await device.WriteAsync(options.Address, options.CreateWritePayload()).ConfigureAwait(false);
                Console.WriteLine($"写入完成：{options.Address}，点数 {options.Quantity}；开始回读。");
            }

            var data = await device.ReadAsync(options.Address, options.Quantity).ConfigureAwait(false);
            PrintReadResult(options.AddressValue, options.Quantity, data);
            await device.DisconnectAsync().ConfigureAwait(false);
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"参数错误：{ex.Message}");
            Console.Error.WriteLine("使用 --help 查看用法。");
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"MX 验收失败：{ex.Message}");
            return 1;
        }
    }

    private static void PrintReadResult(McAddressValue address, int quantity, byte[] data)
    {
        if (address.IsBit)
        {
            for (var index = 0; index < quantity; index++)
            {
                var packed = data[index / 2];
                var value = index % 2 == 0 ? (packed >> 4) & 1 : packed & 1;
                Console.WriteLine($"{address.Offset(index)} = {value}");
            }
            return;
        }

        if (data.Length != quantity * 2)
        {
            throw new InvalidDataException(
                $"字设备回读应为 {quantity * 2} 字节，实际为 {data.Length} 字节。");
        }
        for (var index = 0; index < quantity; index++)
        {
            var value = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(index * 2, 2));
            Console.WriteLine($"{address.Offset(index)} = 0x{value:X4} ({value})");
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("CommunicationNew MX Component 实机验收工具");
        Console.WriteLine();
        Console.WriteLine("只读（默认读取 D100 一个字）：");
        Console.WriteLine("  --station 0 --address D100 --count 1 [--bridge <绝对路径>] [--timeout 3000]");
        Console.WriteLine();
        Console.WriteLine("写字并回读：");
        Console.WriteLine("  --station 0 --address D100 --write-words 0x1234,5678 --confirm-write");
        Console.WriteLine();
        Console.WriteLine("写位并回读：");
        Console.WriteLine("  --station 0 --address M100 --write-bits 1,0,1 --confirm-write");
        Console.WriteLine();
        Console.WriteLine($"密码从环境变量 {PasswordEnvironmentVariable} 读取，不接受命令行密码。");
    }
}
