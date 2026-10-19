using Secs4Net;
using Secs4Net.Sml;
using SecsGem.Runtime.Order.S7;
using SecsGem.Tests.Fakes;
using SecsGem.Tests.Infrastructure;
using Xunit;

namespace SecsGem.Tests.Runtime;

public sealed class ProcessProgramFacadeTests
{
    [Fact]
    public void Facade_Provides_Metadata_And_ReadOnly_Wire_Previews_Without_Sending()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var bytes = new byte[] { 0, 255 };
        runtime.StoreUnformattedProcessProgram("RAW", bytes);
        bytes[0] = 99;
        runtime.StoreFormattedProcessProgram(new ProcessProgramData
        {
            PPID = "FMT", MDLN = "MODEL", SOFTREV = "1.0",
            PPBODY = new() { new List<object> { (ushort)12, new List<object> { 1.25f } } }
        });

        var catalog = runtime.GetProcessPrograms();
        Assert.Equal(new[] { "FMT", "RAW" }, catalog.Select(x => x.PPID));
        var raw = catalog.Single(x => x.PPID == "RAW");
        Assert.True(raw.HasBinary);
        Assert.False(raw.HasFormatted);
        Assert.Equal(2, raw.BinaryLength);
        var formatted = catalog.Single(x => x.PPID == "FMT");
        Assert.Equal("MODEL", formatted.MDLN);
        Assert.Equal("1.0", formatted.SOFTREV);
        Assert.Equal(1, formatted.CommandCount);

        using var binaryPreview = runtime.GetProcessProgramPreview("RAW").ToSecsMessage();
        Assert.Equal((byte)6, binaryPreview.F);
        Assert.Equal(new byte[] { 0, 255 }, binaryPreview.SecsItem!.Items[1].GetMemory<byte>().ToArray());
        using var formattedPreview = runtime.GetProcessProgramPreview("FMT").ToSecsMessage();
        Assert.Equal((byte)26, formattedPreview.F);
        Assert.Equal(4, formattedPreview.SecsItem!.Count);
        Assert.False(formattedPreview.ReplyExpected);
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public void Binary_Read_Facade_Returns_Isolated_Bytes_And_Does_Not_Send()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        Assert.Null(runtime.GetUnformattedProcessProgram("MISSING"));
        runtime.StoreUnformattedProcessProgram("001001", new byte[] { 0, 128, 255 });
        var read = runtime.GetUnformattedProcessProgram("001001")!;
        read[0] = 99;
        Assert.Equal(new byte[] { 0, 128, 255 }, runtime.GetUnformattedProcessProgram("001001"));
        Assert.Null(runtime.GetUnformattedProcessProgram("1001"));
        Assert.Throws<ArgumentException>(() => runtime.GetUnformattedProcessProgram(""));
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public void Formatted_Read_Facade_Returns_Independent_Command_Items_Without_Sending()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        Assert.Null(runtime.GetFormattedProcessProgram("MISSING"));
        runtime.StoreFormattedProcessProgram(new ProcessProgramData
        {
            PPID = "FMT", MDLN = "MODEL", SOFTREV = "1.0",
            PPBODY = new() { new List<object> { (ushort)1, new List<object> { (uint)1, (uint)5000, 1.5, 100d } } }
        });
        var read = runtime.GetFormattedProcessProgram("FMT")!;
        ((Item)read.PPBODY[0]).Items[1].Items[1] = Item.U4(9999);
        read.PPBODY.Clear();
        var original = runtime.GetFormattedProcessProgram("FMT")!;
        Assert.Equal((uint)5000, ((Item)original.PPBODY[0]).Items[1].Items[1].GetMemory<uint>().Span[0]);
        Assert.Throws<ArgumentException>(() => runtime.GetFormattedProcessProgram(""));
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public void Catalog_Is_A_Snapshot_And_Public_Facade_Rejects_Disposed_Runtime()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var original = runtime.GetProcessPrograms();
        runtime.StoreUnformattedProcessProgram("NEW", Array.Empty<byte>());
        Assert.Empty(original);
        Assert.Single(runtime.GetProcessPrograms());
        runtime.Dispose();
        Assert.Throws<ObjectDisposedException>(() => runtime.GetProcessPrograms());
        Assert.Throws<ObjectDisposedException>(() => runtime.GetProcessProgramPreview("NEW"));
        Assert.Throws<ObjectDisposedException>(() => runtime.GetUnformattedProcessProgram("NEW"));
        Assert.Throws<ObjectDisposedException>(() => runtime.GetFormattedProcessProgram("NEW"));
        Assert.Throws<ObjectDisposedException>(() => runtime.StoreUnformattedProcessProgram("NEW", Array.Empty<byte>()));
    }
}
