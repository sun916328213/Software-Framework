using System.ComponentModel.DataAnnotations;
using System.Windows.Threading;
using DataManager.Runtime.Log;
using Prism.DryIoc;
using Prism.Ioc;
using Secs4Net;
using Secs4Net.Sml;
using SecsGem.Controls;
using SecsGem.Controls.Services;
using SecsGem.Runtime;
using SecsGem.Runtime.Order.S7;
using SecsGem.Runtime.Variable;
using SecsGem.Tests.Fakes;

namespace SecsGem.Workbench.Tests;

/// <summary>完全不同于 WorkbenchRecipe 的业务类型；无 Version/曝光/增益/速度字段。</summary>
[Collection("WorkbenchAppBoot")]
public sealed class VariableRecipeSchemaTests(WorkbenchAppFixture host)
{
    public sealed record ProfileStep(string Name, int[] Offsets, double[] Times, bool Enabled, string[] Tags);
    public sealed record ProfileRecipe([property: Required] string BatchName, bool DryRun, byte[] Signature, ProfileStep[] Steps);

    private sealed class ProfileCodec : IFormattedRecipeCodec<ProfileRecipe>
    {
        public ProcessProgramData Encode(string ppid, ProfileRecipe recipe)
        {
            var commands = new List<object>
            {
                Item.L(Item.A("HEADER"), Item.L(Item.A(recipe.BatchName), Item.Boolean(recipe.DryRun), Item.B(recipe.Signature.ToArray())))
            };
            foreach (var step in recipe.Steps)
                commands.Add(Item.L(Item.A("STEP"), Item.L(Item.A(step.Name), Item.I4(step.Offsets.ToArray()),
                    Item.F8(step.Times.ToArray()), Item.Boolean(step.Enabled), Item.L(step.Tags.Select(Item.A)))));
            return new() { PPID = ppid, MDLN = "PROFILE-MODEL", SOFTREV = "3.0", PPBODY = commands };
        }

        public ProfileRecipe Decode(ProcessProgramData program)
        {
            if (program.PPBODY.Count == 0) throw new ArgumentException("缺少 HEADER。");
            var header = (Item)program.PPBODY[0];
            if (header.Items[0].GetString() != "HEADER" || header.Items[1].Count != 3)
                throw new ArgumentException("HEADER 要求三个参数。");
            var fields = header.Items[1].Items;
            var steps = program.PPBODY.Skip(1).Cast<Item>().Select(command =>
            {
                if (command.Items[0].GetString() != "STEP" || command.Items[1].Count != 5)
                    throw new ArgumentException("STEP 要求五个参数。");
                var p = command.Items[1].Items;
                return new ProfileStep(p[0].GetString(), p[1].GetMemory<int>().ToArray(), p[2].GetMemory<double>().ToArray(),
                    p[3].GetMemory<bool>().Span[0], p[4].Items.Select(item => item.GetString()).ToArray());
            }).ToArray();
            return new(fields[0].GetString(), fields[1].GetMemory<bool>().Span[0], fields[2].GetMemory<byte>().ToArray(), steps);
        }
    }

    [Theory]
    [InlineData(121)]
    [InlineData(1024)]
    public void Generic_Controls_Do_Not_Reintroduce_A_Fixed_Ppid_Length_Limit(int length)
    {
        var ppid = "00042-" + new string('R', length);
        ProcessProgramCommunication.ValidatePpid(ppid);
        using var request = ProcessProgramCommunication.CreateRequest(2, ppid);
        Assert.Equal(ppid, request.SecsItem!.GetString());
        Assert.Throws<ArgumentException>(() => ProcessProgramCommunication.ValidatePpid(""));
        Assert.Throws<ArgumentException>(() => ProcessProgramCommunication.ValidatePpid("中文"));
    }

    private static SecsGemRuntime NewRuntime(InMemorySecsTransport transport)
    {
        var container = new DryIocContainerExtension();
        container.RegisterSingleton<LogManager>();
        new SecsGemModule().RegisterTypes(container);
        return new SecsGemRuntime(container, transport);
    }

    private static VIDManager Catalog => ContainerLocator.Container.Resolve<VIDManager>();
    // 仅借用 Item 生成比较文本，所有权仍属于原请求/回复。
    private static string ReplySml(Item body) => new SecsMessage(7, 26) { SecsItem = body }.ToSml();
    private static void Receive(InMemorySecsTransport transport, byte function, Item body)
    {
        using (var request = new SecsMessage(7, function, true) { SecsItem = body })
            transport.EmitReceivedAsync(request).GetAwaiter().GetResult();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    [Fact]
    public void Independent_Host_Sml_With_Nested_Lists_And_Vectors_Updates_A_Different_Business_Model()
    {
        host.Invoke(() =>
        {
            var transport = new InMemorySecsTransport();
            using var runtime = NewRuntime(transport);
            using var controls = new SecsWorkspace(runtime, Catalog, Dispatcher.CurrentDispatcher);
            var recipes = new Dictionary<string, ProfileRecipe>();
            var errors = new List<string?>();
            controls.ConfigureRecipeSource(recipes, () => "RUNNING-OTHER", received: (_, error) => errors.Add(error), formattedCodec: new ProfileCodec());
            // 独立手写 Host 报文，不由被测业务编解码器生成输入。
            const string sml = """
                'S7F23' W
                <L [4]
                    <A [9] 'PROFILE-A'>
                    <A [13] 'PROFILE-MODEL'>
                    <A [3] '3.0'>
                    <L [3]
                        <L [2]
                            <A [6] 'HEADER'>
                            <L [3]
                                <A [5] 'LOT-7'>
                                <Boolean [1] True>
                                <B [3] 0 255 128>
                            >
                        >
                        <L [2]
                            <A [4] 'STEP'>
                            <L [5]
                                <A [4] 'RAMP'>
                                <I4 [3] -3 0 7>
                                <F8 [2] 0.25 1.5>
                                <Boolean [1] False>
                                <L [2]
                                    <A [4] 'edge'>
                                    <A [4] 'main'>
                                >
                            >
                        >
                        <L [2]
                            <A [4] 'STEP'>
                            <L [5]
                                <A [4] 'HOLD'>
                                <I4 [0]>
                                <F8 [1] 3>
                                <Boolean [1] True>
                                <L [0]
                                >
                            >
                        >
                    >
                >
                .
                """;
            string expected;
            using (var download = sml.ToSecsMessage())
            {
                expected = ReplySml(download.SecsItem!);
                transport.EmitReceivedAsync(download).GetAwaiter().GetResult();
            }
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Equal((byte)0, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
            Assert.Null(Assert.Single(errors));
            var recipe = recipes["PROFILE-A"];
            Assert.Equal("LOT-7", recipe.BatchName);
            Assert.True(recipe.DryRun);
            Assert.Equal(new byte[] { 0, 255, 128 }, recipe.Signature);
            Assert.Equal(2, recipe.Steps.Length);
            Assert.Equal(new[] { -3, 0, 7 }, recipe.Steps[0].Offsets);
            Assert.Equal(new[] { 0.25, 1.5 }, recipe.Steps[0].Times);
            Assert.False(recipe.Steps[0].Enabled);
            Assert.Equal(new[] { "edge", "main" }, recipe.Steps[0].Tags);
            Assert.Empty(recipe.Steps[1].Offsets);
            Assert.Empty(recipe.Steps[1].Tags);
            Receive(transport, 25, Item.A("PROFILE-A"));
            Assert.Equal(expected, ReplySml(transport.LastReplyItem!));
            Assert.Equal("RUNNING-OTHER", controls.Model.HostCurrentPpid);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    public void Local_Publication_And_Host_Replacement_Support_Variable_Step_And_Array_Counts(int stepCount)
    {
        host.Invoke(() =>
        {
            var transport = new InMemorySecsTransport();
            using var runtime = NewRuntime(transport);
            using var controls = new SecsWorkspace(runtime, Catalog, Dispatcher.CurrentDispatcher);
            var steps = Enumerable.Range(0, stepCount).Select(index => new ProfileStep($"STEP-{index}",
                Enumerable.Range(0, index % 4).Select(value => -value).ToArray(),
                Enumerable.Range(0, index % 3).Select(value => value + 0.25).ToArray(), index % 2 == 0,
                Enumerable.Range(0, index % 5).Select(value => $"TAG-{value}").ToArray())).ToArray();
            var current = new ProfileRecipe("CURRENT", true, new byte[] { 1, 2, 255 }, steps);
            var recipes = new Dictionary<string, ProfileRecipe> { ["000123"] = current };
            var codec = new ProfileCodec();
            controls.ConfigureRecipeSource(recipes, () => "000123", formattedCodec: codec);
            controls.ImportHostRecipes();
            Receive(transport, 25, Item.A("000123"));
            var reply = transport.LastReplyItem!;
            Assert.Equal("000123", reply.Items[0].GetString());
            Assert.Equal(1 + stepCount, reply.Items[3].Count);
            Assert.Equal(3, reply.Items[3].Items[0].Items[1].Count);
            for (var i = 0; i < stepCount; i++)
            {
                var parameters = reply.Items[3].Items[i + 1].Items[1];
                Assert.Equal(5, parameters.Count);
                Assert.Equal(current.Steps[i].Offsets, parameters.Items[1].GetMemory<int>().ToArray());
                Assert.Equal(current.Steps[i].Times, parameters.Items[2].GetMemory<double>().ToArray());
                Assert.Equal(current.Steps[i].Tags, parameters.Items[4].Items.Select(item => item.GetString()).ToArray());
            }
            reply.Items[3].Items[0].Items[1].Items[0] = Item.A("MUTATED-REPLY");
            Receive(transport, 25, Item.A("000123"));
            Assert.Equal("CURRENT", transport.LastReplyItem!.Items[3].Items[0].Items[1].Items[0].GetString());
            Receive(transport, 3, Item.L(Item.A("000123"), Item.B(ProcessProgramCommunication.Serialize(
                new ProfileRecipe("FROM-JSON", false, new byte[] { 128 }, steps)))));
            Assert.Equal("FROM-JSON", recipes["000123"].BatchName);
            Receive(transport, 25, Item.A("000123"));
            Assert.Equal("FROM-JSON", transport.LastReplyItem!.Items[3].Items[0].Items[1].Items[0].GetString());
            Assert.Equal(1 + stepCount, transport.LastReplyItem.Items[3].Count);
            var replacement = codec.Encode("000123", new ProfileRecipe("NEW-BATCH", false, Array.Empty<byte>(), steps.Reverse().ToArray()));
            Receive(transport, 23, Item.L(Item.A(replacement.PPID), Item.A(replacement.MDLN), Item.A(replacement.SOFTREV),
                Item.L(replacement.PPBODY.Cast<Item>())));
            Assert.Equal((byte)0, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
            Assert.Equal("NEW-BATCH", recipes["000123"].BatchName);
            Assert.False(recipes["000123"].DryRun);
            Assert.Empty(recipes["000123"].Signature);
            Assert.Equal(stepCount, recipes["000123"].Steps.Length);
            Assert.Equal("CURRENT", current.BatchName); // 更新集合不能替换当前快照。
            Assert.Equal(new byte[] { 1, 2, 255 }, current.Signature);
            Receive(transport, 25, Item.A("000123"));
            Assert.Equal("NEW-BATCH", transport.LastReplyItem!.Items[3].Items[0].Items[1].Items[0].GetString());
        });
    }
}
