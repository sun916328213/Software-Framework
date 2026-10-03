// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ModbusTcpCodecTests、BufferSegment。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Buffers、Communication.Runtime.Protocols.Modbus、Xunit。导入不代表所有类型都会在运行时加载。
using System.Buffers;
using Communication.Runtime.Protocols.Modbus;
using Xunit;

namespace Communication.Tests.Protocols.Modbus;

/// <summary>
/// ModbusTcpCodecTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ModbusTcpCodecTests
{
    /// <summary>
    /// 验证 Read_holding_register_request_matches_the_modbus_tcp_wire_format 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Parse、Read、Encode、Equal、ToArray；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Read_holding_register_request_matches_the_modbus_tcp_wire_format()
    {
        var codec = new ModbusTcpCodec();
        var address = ModbusAddressValue.Parse("HR1");
        var request = ModbusTcpRequest.Read(
            unitId: 1,
            address,
            quantity: 2);

        var frame = codec.Encode(request, correlationId: 0x1234);

        Assert.Equal(address, request.Address);
        Assert.Equal(
            new byte[] { 0x12, 0x34, 0, 0, 0, 6, 1, 3, 0, 0, 0, 2 },
            frame.ToArray());
    }

    /// <summary>
    /// 验证 Write_request_preserves_the_typed_modbus_address 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Parse、Write、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Write_request_preserves_the_typed_modbus_address()
    {
        var address = ModbusAddressValue.Parse("HR10");

        var request = ModbusTcpRequest.Write(
            unitId: 1,
            address,
            quantity: 1,
            payload: new byte[] { 0, 42 });

        Assert.Equal(address, request.Address);
    }

    /// <summary>
    /// 验证 Split_response_waits_for_the_complete_frame_and_returns_register_bytes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 AsMemory、False、TryDecode、True、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Split_response_waits_for_the_complete_frame_and_returns_register_bytes()
    {
        var codec = new ModbusTcpCodec();
        var bytes = new byte[]
        {
            0x12, 0x34, 0, 0, 0, 7, 1,
            3, 4, 0, 0x2A, 0, 0x2B,
        };

        var partial = new ReadOnlySequence<byte>(bytes.AsMemory(0, 8));
        Assert.False(codec.TryDecode(partial, 0x1234, out _, out _));

        var complete = new ReadOnlySequence<byte>(bytes);
        Assert.True(codec.TryDecode(complete, 0x1234, out var response, out var consumed));
        Assert.Equal(complete.End, consumed);
        Assert.Equal((byte)1, response.UnitId);
        Assert.Equal((byte)3, response.FunctionCode);
        Assert.Equal(new byte[] { 0, 0x2A, 0, 0x2B }, response.Data.ToArray());
    }

    /// <summary>
    /// 验证 Every_segment_boundary_preserves_two_coalesced_responses 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateSegmentedSequence、True、TryDecode、Slice、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Every_segment_boundary_preserves_two_coalesced_responses()
    {
        var codec = new ModbusTcpCodec();
        var bytes = new byte[]
        {
            0x12, 0x34, 0, 0, 0, 7, 1, 3, 4, 0, 0x2A, 0, 0x2B,
            0x12, 0x35, 0, 0, 0, 5, 1, 3, 2, 0, 0x2C,
        };

        // 穷举每个切分位置，覆盖 MBAP、PDU 和两帧交界处的分段读取。
        for (var split = 1; split < bytes.Length; split++)
        {
            var sequence = CreateSegmentedSequence(bytes, split);

            Assert.True(codec.TryDecode(
                sequence,
                0x1234,
                out var first,
                out var firstConsumed));
            var remaining = sequence.Slice(firstConsumed);
            Assert.Equal(new byte[] { 0, 0x2A, 0, 0x2B }, first.Data.ToArray());

            Assert.True(codec.TryDecode(
                remaining,
                0x1235,
                out var second,
                out var secondConsumed));
            Assert.Equal(new byte[] { 0, 0x2C }, second.Data.ToArray());
            Assert.Equal(remaining.End, secondConsumed);
        }
    }

    /// <summary>
    /// 验证 CreateSegmentedSequence 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 AsMemory、Append；具体条件和先后次序见方法体。
    /// </summary>
    private static ReadOnlySequence<byte> CreateSegmentedSequence(
        byte[] bytes,
        int split)
    {
        var first = new BufferSegment(bytes.AsMemory(0, split));
        var last = first.Append(bytes.AsMemory(split));
        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    /// <summary>
    /// BufferSegment 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ReadOnlySequenceSegment&lt;byte&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class BufferSegment : ReadOnlySequenceSegment<byte>
    {
        /// <summary>
        /// 初始化 BufferSegment 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public BufferSegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        /// <summary>
        /// 验证 Append 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public BufferSegment Append(ReadOnlyMemory<byte> memory)
        {
            var segment = new BufferSegment(memory)
            {
                RunningIndex = RunningIndex + Memory.Length,
            };
            Next = segment;
            return segment;
        }
    }
}
