using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DataManager.Runtime.Log;
using Prism.DryIoc;
using Prism.Ioc;
using Secs4Net;
using Secs4Net.Sml;
using SecsGem.Controls;
using SecsGem.Controls.Services;
using SecsGem.Controls.ViewModels;
using SecsGem.Controls.Views;
using SecsGem.Runtime;
using SecsGem.Runtime.Protocol.Registry;
using SecsGem.Runtime.Variable;
using SecsGem.Tests.Fakes;
using SecsGem.Tests.Infrastructure;
using SecsGem.Workbench.Models;
using SecsGem.Workbench.Services;
using SecsGem.Workbench.ViewModels;
using SecsGem.Workbench.Views;

namespace SecsGem.Workbench.Tests;

public sealed class RecipeCommunicationTests
{
    private static WorkbenchRecipe Recipe => new(1, 5000, 1.5, 100);

    [Fact]
    public void Controls_Converts_A_Host_Defined_Type_Using_An_Independent_Json_Golden()
    {
        var expected = Encoding.UTF8.GetBytes("{\"version\":1,\"exposureUs\":5000,\"gain\":1.5,\"speedMmPerSec\":100}");
        var bytes = ProcessProgramCommunication.Serialize(Recipe);
        Assert.Equal(expected, bytes);
        Assert.Equal(Recipe, ProcessProgramCommunication.Deserialize<WorkbenchRecipe>(bytes));
    }

    [Fact]
    public void Struct_Recipe_Deserialization_Validates_The_Same_Boxed_Instance()
    {
        // 独立正文直接反序列化，避免序列化阶段先失败而掩盖下载路径的问题。
        var body = Encoding.UTF8.GetBytes("{\"version\":1,\"exposureUs\":5000,\"gain\":1.5,\"speedMmPerSec\":100}");
        Assert.Equal(new WorkbenchRecipe(1, 5000, 1.5, 100),
            ProcessProgramCommunication.Deserialize<WorkbenchRecipe>(body));
    }

    [Fact]
    public void Struct_Recipe_Serialization_Still_Enforces_Host_Validation_Rules()
    {
        Assert.Throws<ValidationException>(() =>
            ProcessProgramCommunication.Serialize(new WorkbenchRecipe(1, -1, 1.5, 100)));
    }

    [Theory]
    [InlineData("{}", typeof(JsonException))]
    [InlineData("{\"version\":\"1\",\"exposureUs\":5000,\"gain\":1.5,\"speedMmPerSec\":100}", typeof(JsonException))]
    [InlineData("{\"version\":0,\"exposureUs\":5000,\"gain\":1.5,\"speedMmPerSec\":100}", typeof(ValidationException))]
    [InlineData("{\"version\":-1,\"exposureUs\":5000,\"gain\":1.5,\"speedMmPerSec\":100}", typeof(ValidationException))]
    [InlineData("{\"version\":1,\"exposureUs\":-1,\"gain\":1.5,\"speedMmPerSec\":100}", typeof(ValidationException))]
    [InlineData("{\"version\":1,\"exposureUs\":5000,\"gain\":1.5,\"speedMmPerSec\":100,\"unknown\":1}", typeof(JsonException))]
    public void Controls_Uses_The_Host_Type_Required_Fields_And_Validation_Rules(string json, Type error)
    {
        Assert.Throws(error, () => ProcessProgramCommunication.Deserialize<WorkbenchRecipe>(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void Positive_Versions_Are_Accepted_By_Json_And_Formatted_Recipe_Paths(int version)
    {
        var expected = new WorkbenchRecipe(version, 5000, 1.5, 100);
        var json = Encoding.UTF8.GetBytes($"{{\"version\":{version},\"exposureUs\":5000,\"gain\":1.5,\"speedMmPerSec\":100}}");
        Assert.Equal(expected, ProcessProgramCommunication.Deserialize<WorkbenchRecipe>(json));
        Assert.Equal(json, ProcessProgramCommunication.Serialize(expected));
        foreach (var value in new[] { Item.U4((uint)version), Item.A(version.ToString(System.Globalization.CultureInfo.InvariantCulture)) })
        {
            using var command = Item.L(Item.U2(1), Item.L(value, Item.U4(5000), Item.F8(1.5), Item.F8(100)));
            var program = new SecsGem.Runtime.Order.S7.ProcessProgramData { PPID = "DEMO-Test", PPBODY = new() { command } };
            Assert.Equal(expected, ProcessProgramCommunication.DeserializeFormatted<WorkbenchRecipe>(program, WorkbenchRecipe.FormattedParameterNames));
        }
    }

    [Fact]
    public void S7F1_Length_And_S7F3_Body_Use_Actual_Bytes_And_Preserve_Ascii_Ids()
    {
        var bytes = ProcessProgramCommunication.Serialize(Recipe);
        using var inquiry = ProcessProgramCommunication.CreateRequest(0, "001001", bytes);
        using var download = ProcessProgramCommunication.CreateRequest(1, "001001", bytes);
        Assert.Equal((uint)bytes.Length, inquiry.SecsItem!.Items[1].GetMemory<uint>().Span[0]);
        Assert.Equal(SecsFormat.ASCII, download.SecsItem!.Items[0].Format);
        Assert.Equal("001001", download.SecsItem.Items[0].GetString());
        Assert.Equal(bytes, download.SecsItem.Items[1].GetMemory<byte>().ToArray());
        bytes[0] = 0;
        Assert.Equal((byte)'{', download.SecsItem.Items[1].GetMemory<byte>().Span[0]);
    }

    [Fact]
    public void Formatted_Parameter_Block_Uses_Explicit_Host_Order_And_Required_Range_Validation()
    {
        using var command = Item.L(Item.U2(1), Item.L(Item.U4(1), Item.U4(5000), Item.F8(1.5), Item.F8(100)));
        var program = new SecsGem.Runtime.Order.S7.ProcessProgramData { PPID = "DEMO-Test", PPBODY = new() { command } };
        Assert.Equal(Recipe, ProcessProgramCommunication.DeserializeFormatted<WorkbenchRecipe>(program, WorkbenchRecipe.FormattedParameterNames));
        command.Items[1].Items[1] = Item.U4(uint.MaxValue);
        Assert.Throws<JsonException>(() => ProcessProgramCommunication.DeserializeFormatted<WorkbenchRecipe>(program, WorkbenchRecipe.FormattedParameterNames));
        command.Items[1].Items[1] = Item.U4(5000);
        command.Items[1].Items[0] = Item.U4(2);
        Assert.Equal(2, ProcessProgramCommunication.DeserializeFormatted<WorkbenchRecipe>(program, WorkbenchRecipe.FormattedParameterNames).Version);
        command.Items[1].Items[0] = Item.U4(0);
        Assert.Throws<ValidationException>(() => ProcessProgramCommunication.DeserializeFormatted<WorkbenchRecipe>(program, WorkbenchRecipe.FormattedParameterNames));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Formatted_Business_Parameters_Accept_Ascii_Numbers_And_Other_Numeric_Formats(bool ascii)
    {
        using var command = ascii
            ? Item.L(Item.A("1"), Item.L(Item.A("1"), Item.A("5000"), Item.A("1.5"), Item.A("100")))
            : Item.L(Item.U4(1), Item.L(Item.U1(1), Item.I4(5000), Item.F4(1.5f), Item.U2(100)));
        var program = new SecsGem.Runtime.Order.S7.ProcessProgramData { PPID = "DEMO-Test", PPBODY = new() { command } };
        Assert.Equal(Recipe, ProcessProgramCommunication.DeserializeFormatted<WorkbenchRecipe>(program, WorkbenchRecipe.FormattedParameterNames));
        // 转换不改原始 CCODE/PPARM 类型，协议读取仍能返回原始数据。
        Assert.Equal(ascii ? SecsFormat.ASCII : SecsFormat.U1, command.Items[1].Items[0].Format);
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    public void Formatted_Numeric_Text_Must_Not_Default_Truncate_Or_Overflow(string version)
    {
        using var command = Item.L(Item.A("1"), Item.L(Item.A(version), Item.A("5000"), Item.A("1.5"), Item.A("100")));
        var program = new SecsGem.Runtime.Order.S7.ProcessProgramData { PPID = "DEMO-Test", PPBODY = new() { command } };
        Assert.Throws<JsonException>(() => ProcessProgramCommunication.DeserializeFormatted<WorkbenchRecipe>(program, WorkbenchRecipe.FormattedParameterNames));
    }

    [Fact]
    public void Formatted_Requests_Require_Explicit_Protocol_Data_Not_A_Guessed_Recipe_Mapping()
    {
        Assert.Throws<ArgumentException>(() => ProcessProgramCommunication.CreateRequest(5, "FMT"));
        using var message = ProcessProgramCommunication.CreateRequest(5, "FMT", formattedSml:
            """
            'S7F23' W
            <L [4]
                <A [3] 'FMT'>
                <A [5] 'MODEL'>
                <A [3] '1.0'>
                <L [1]
                    <L [2]
                        <U2 [1] 12>
                        <L [1]
                            <F8 [1] 2.5>
                        >
                    >
                >
            >
            .
            """);
        Assert.Equal(2.5, message.SecsItem!.Items[3].Items[0].Items[1].Items[0].GetMemory<double>().Span[0]);
        Assert.Throws<ArgumentException>(() => ProcessProgramCommunication.CreateRequest(5, "OTHER", formattedSml: message.ToSml()));
    }
}

/// <summary>离线 STA 验证：Workbench 只切换，Controls 负责转换/模板/读写；不连接真实 Host/设备。</summary>
[Collection("WorkbenchAppBoot")]
public sealed class S7RecipeInteractionTests(WorkbenchAppFixture host)
{
    private static SecsGemRuntime NewRuntime(out SecsMessageRegistry registry)
    {
        var container = new DryIocContainerExtension();
        container.RegisterSingleton<LogManager>();
        new SecsGemModule().RegisterTypes(container);
        var runtime = container.Resolve<SecsGemRuntime>();
        registry = container.Resolve<SecsMessageRegistry>();
        return runtime;
    }
    private static SecsGemRuntime NewRuntime() => NewRuntime(out _);
    private static SecsGemRuntime NewRuntime(InMemorySecsTransport transport)
    {
        var container = new DryIocContainerExtension();
        container.RegisterSingleton<LogManager>();
        new SecsGemModule().RegisterTypes(container);
        return new SecsGemRuntime(container, transport);
    }
    private static VIDManager Catalog => ContainerLocator.Container.Resolve<VIDManager>();
    private static void Layout(FrameworkElement page)
    {
        page.Measure(new Size(1100, 850));
        page.Arrange(new Rect(0, 0, 1100, 850));
        page.UpdateLayout();
        page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static void Configure(SecsWorkspace controls, RecipeTestWorkspace recipes, bool mapFormatted = false) =>
        controls.ConfigureRecipeSource(recipes.Model.Recipes, () => recipes.Model.CurrentPpid, recipes.Model.NotifyRecipesChanged,
            recipes.Model.NotifyRecipeReceived, recipes.Model.NotifyRecipesDeleted, recipes.Model.NotifyFormattedRecipeReceived,
            formattedCodec: mapFormatted ? new ParameterBlockRecipeCodec<WorkbenchRecipe>(
                WorkbenchRecipe.FormattedParameterNames, model: "WORKBENCH", softwareRevision: "1.0") : null);

    [Fact]
    public void Workbench_Real_Button_Manually_Switches_And_Contains_No_Communication_Editor()
    {
        host.Invoke(() =>
        {
            var recipes = new RecipeTestWorkspace();
            using var vm = new RecipeTestViewModel(recipes);
            using var page = RecipeTestView.Create(vm);
            Layout(page);
            Assert.Empty(recipes.Model.CurrentPpid);
            ((DataGrid)page.FindName("RecipeListGrid")).SetCurrentValue(DataGrid.SelectedValueProperty, "DEMO-PRECISE");
            Assert.Empty(recipes.Model.CurrentPpid); // 下拉选中不是切换提交。
            var button = Descendants(page).OfType<Button>().Single(x => Equals(x.Content, "切换当前配方"));
            Assert.True(button.IsEnabled);
            button.Command.Execute(null);
            Assert.Equal("DEMO-PRECISE", recipes.Model.CurrentPpid);
            Assert.Equal(10000, recipes.Model.CurrentRecipe!.Value.ExposureUs);
            Assert.Null(page.FindName("RequestBox"));
            Assert.Null(page.FindName("JsonPreviewBox"));
            vm.Dispose();
            Assert.False(vm.SwitchRecipeCommand.CanExecute(null));
        });
    }

    [Fact]
    public void Controls_Removes_Recipe_Test_Tab_But_Keeps_Communication_Services()
    {
        host.Invoke(() =>
        {
            using var runtime = NewRuntime();
            using var controls = new SecsWorkspace(runtime, Catalog, Dispatcher.CurrentDispatcher);
            using var vm = new SecsViewModel(controls);
            using var page = SecsView.Create(vm);
            Layout(page);
            Assert.Null(page.FindName("RecipeTab"));
            Assert.Null(page.FindName("RecipeGrid"));
            Assert.Null(page.FindName("RecipeRequestBox"));
            Assert.DoesNotContain(Descendants(page).OfType<Button>(), button => Equals(button.Content, "读取 JSON 到宿主数据"));
            Assert.False(vm.ImportHostRecipesCommand.CanExecute(null));
            Assert.True(vm.BuildRecipeRequestCommand.CanExecute(null));
            controls.Model.RecipeRequestIndex = 4;
            vm.BuildRecipeRequestCommand.Execute(null);
            using var directory = controls.Model.RecipeRequestSml.ToSecsMessage();
            Assert.Equal((byte)19, directory.F);
            Assert.Null(directory.SecsItem);
            Assert.Empty(runtime.GetProcessPrograms());
        });
    }

    [Fact]
    public void Seed_Query_Read_And_Delete_Use_The_Same_Protocol_Library_And_Refresh_All_Rows()
    {
        host.Invoke(() =>
        {
            var transport = new InMemorySecsTransport();
            using var runtime = NewRuntime(transport);
            var recipes = new RecipeTestWorkspace();
            using var controls = new SecsWorkspace(runtime, Catalog, Dispatcher.CurrentDispatcher);
            Configure(controls, recipes, mapFormatted: true);
            controls.ImportHostRecipes(); // 与独立 Workbench 启动的同一个初始化入口。
            using var page = RecipeTestView.Create(new RecipeTestViewModel(recipes));
            Layout(page);
            var grid = (DataGrid)page.FindName("RecipeListGrid");
            void Receive(byte function, Item? body = null)
            {
                using var message = new SecsMessage(7, function, true) { SecsItem = body };
                transport.EmitReceivedAsync(message).GetAwaiter().GetResult();
                Layout(page);
            }
            Receive(19);
            Assert.Equal(new[] { "DEMO-FAST", "DEMO-PRECISE" }, transport.LastReplyItem!.Items.Select(item => item.GetString()).OrderBy(id => id, StringComparer.Ordinal));
            Receive(5, Item.A("DEMO-FAST"));
            Assert.Equal(2, transport.LastReplyItem!.Count);
            Assert.Equal("DEMO-FAST", transport.LastReplyItem.Items[0].GetString());
            Assert.Equal(Encoding.UTF8.GetBytes("{\"version\":1,\"exposureUs\":5000,\"gain\":1.5,\"speedMmPerSec\":100}"),
                transport.LastReplyItem.Items[1].GetMemory<byte>().ToArray());
            Receive(25, Item.A("DEMO-FAST"));
            Assert.Equal(4, transport.LastReplyItem!.Count);
            Assert.Equal("DEMO-FAST", transport.LastReplyItem.Items[0].GetString());
            Assert.Equal("WORKBENCH", transport.LastReplyItem.Items[1].GetString());
            Assert.Equal("1.0", transport.LastReplyItem.Items[2].GetString());
            var command = Assert.Single(transport.LastReplyItem.Items[3].Items);
            Assert.Equal((ushort)1, command.Items[0].GetMemory<ushort>().Span[0]);
            Assert.Equal((uint)5000, command.Items[1].Items[1].GetMemory<uint>().Span[0]);
            Assert.Equal(1.5, command.Items[1].Items[2].GetMemory<double>().Span[0]);
            Assert.Equal(100, command.Items[1].Items[3].GetMemory<double>().Span[0]);
            recipes.SwitchRecipe();
            var current = recipes.Model.CurrentRecipe;
            Receive(17, Item.L(Item.A("DEMO-FAST"), Item.A("MISSING")));
            Assert.Equal((byte)4, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
            Assert.Equal(2, grid.Items.Count); // 拒绝的整批删除不能误删 UI。
            Receive(17, Item.L(Item.A("DEMO-FAST")));
            Assert.Equal((byte)0, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
            Assert.Equal("DEMO-PRECISE", Assert.Single(grid.Items.Cast<RecipeListRow>()).Key);
            Assert.DoesNotContain("DEMO-FAST", recipes.Model.Recipes.Keys);
            Assert.Null(runtime.GetUnformattedProcessProgram("DEMO-FAST"));
            Assert.Equal(current, recipes.Model.CurrentRecipe);
            Receive(19);
            Assert.Equal("DEMO-PRECISE", Assert.Single(transport.LastReplyItem!.Items).GetString());
            Receive(5, Item.A("DEMO-FAST"));
            Assert.Equal(0, transport.LastReplyItem!.Count); // 只有不存在时才为空。
            Receive(25, Item.A("DEMO-FAST"));
            Assert.Equal(0, transport.LastReplyItem!.Count); // 删除后二进制和格式化正文均不可读。
            Receive(3, Item.L(Item.A("RAW-ONLY"), Item.B(new byte[] { 0 })));
            Assert.Contains(grid.Items.Cast<RecipeListRow>(), row => row.Key == "RAW-ONLY" && !row.CanSwitch);
            Receive(17, Item.L());
            Assert.Equal((byte)0, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
            Assert.Empty(grid.Items);
            Assert.Empty(recipes.Model.RecipeNames); // 解析失败项也必须清理。
            Assert.Empty(recipes.Model.Recipes);
            Assert.Empty(runtime.GetProcessPrograms());
            Assert.Equal(current, recipes.Model.CurrentRecipe);
            Receive(19);
            Assert.Equal(0, transport.LastReplyItem!.Count);
        });
    }

    public sealed record AlternateRecipe([property: Required] string Program, [property: Range(1, 100)] int Cycles);

    private sealed class AlternateCodec : IFormattedRecipeCodec<AlternateRecipe>
    {
        public SecsGem.Runtime.Order.S7.ProcessProgramData Encode(string ppid, AlternateRecipe recipe) => new()
        {
            PPID = ppid, MDLN = "ALT-MODEL", SOFTREV = "2.0",
            PPBODY = new()
            {
                Item.L(Item.U2(7), Item.L(Item.A(recipe.Program))),
                Item.L(Item.U2(9), Item.L(Item.U4(checked((uint)recipe.Cycles))))
            }
        };
        public AlternateRecipe Decode(SecsGem.Runtime.Order.S7.ProcessProgramData program)
        {
            if (program.PPBODY.Count != 2) throw new ArgumentException("要求两条工艺命令。");
            var name = (Item)program.PPBODY[0];
            var cycles = (Item)program.PPBODY[1];
            if (name.Items[0].GetMemory<ushort>().Span[0] != 7 || cycles.Items[0].GetMemory<ushort>().Span[0] != 9)
                throw new ArgumentException("未知工艺命令。");
            return new(name.Items[1].Items[0].GetString(), checked((int)cycles.Items[1].Items[0].GetMemory<uint>().Span[0]));
        }
    }

    [Fact]
    public void Replaceable_Codec_Publishes_And_Reads_A_Different_Model_With_Multiple_Commands()
    {
        host.Invoke(() =>
        {
            var transport = new InMemorySecsTransport();
            using var runtime = NewRuntime(transport);
            using var controls = new SecsWorkspace(runtime, Catalog, Dispatcher.CurrentDispatcher);
            var recipes = new Dictionary<string, AlternateRecipe> { ["00042"] = new("ETCH", 4) };
            var codec = new AlternateCodec();
            controls.ConfigureRecipeSource(recipes, () => null, formattedCodec: codec);
            controls.ImportHostRecipes();
            void Receive(byte function, Item body)
            {
                using var request = new SecsMessage(7, function, true) { SecsItem = body };
                transport.EmitReceivedAsync(request).GetAwaiter().GetResult();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }
            Receive(25, Item.A("00042"));
            Assert.Equal("00042", transport.LastReplyItem!.Items[0].GetString());
            Assert.Equal("ALT-MODEL", transport.LastReplyItem.Items[1].GetString());
            Assert.Equal(2, transport.LastReplyItem.Items[3].Count);
            Assert.Equal("ETCH", transport.LastReplyItem.Items[3].Items[0].Items[1].Items[0].GetString());
            Receive(3, Item.L(Item.A("00042"), Item.B(Encoding.UTF8.GetBytes("{\"program\":\"ETCH-B\",\"cycles\":7}"))));
            Receive(25, Item.A("00042"));
            Assert.Equal((uint)7, transport.LastReplyItem!.Items[3].Items[1].Items[1].Items[0].GetMemory<uint>().Span[0]);
            var hostProgram = codec.Encode("00042", new AlternateRecipe("ETCH-C", 9));
            Receive(23, Item.L(Item.A(hostProgram.PPID), Item.A(hostProgram.MDLN), Item.A(hostProgram.SOFTREV),
                Item.L(hostProgram.PPBODY.Cast<Item>())));
            Assert.Equal(new AlternateRecipe("ETCH-C", 9), recipes["00042"]);
            Receive(25, Item.A("00042"));
            Assert.Equal((uint)9, transport.LastReplyItem!.Items[3].Items[1].Items[1].Items[0].GetMemory<uint>().Span[0]);
            Receive(25, Item.A("42"));
            Assert.Equal(0, transport.LastReplyItem!.Count); // 不能合并不同 ASCII PPID。
        });
    }

    [Fact]
    public void S7F23_Maps_Real_Recipe_And_Rejects_Invalid_Updates_Without_Replacing_Current_Snapshot()
    {
        host.Invoke(() =>
        {
            var transport = new InMemorySecsTransport();
            using var runtime = NewRuntime(transport);
            using var controls = new SecsWorkspace(runtime, Catalog, Dispatcher.CurrentDispatcher);
            var recipes = new RecipeTestWorkspace();
            Configure(controls, recipes, mapFormatted: true);
            using var vm = new RecipeTestViewModel(recipes);
            using var page = RecipeTestView.Create(vm);
            void Send(ushort code, params Item[] parameters)
            {
                using (var message = new SecsMessage(7, 23, true)
                {
                    SecsItem = Item.L(Item.A("DEMO-Test"), Item.A("WORKBENCH"), Item.A("1.0"),
                        Item.L(Item.L(Item.U2(code), Item.L(parameters))))
                }) transport.EmitReceivedAsync(message).GetAwaiter().GetResult();
                Layout(page);
                Assert.Equal((byte)0, transport.LastReplyItem!.GetMemory<byte>().Span[0]); // 原始格式已接收，不等于业务校验成功。
            }
            Send(1, Item.A("1"), Item.A("5000"), Item.A("1.5"), Item.A("100"));
            Assert.Equal(new WorkbenchRecipe(1, 5000, 1.5, 100), recipes.Model.Recipes["DEMO-Test"]);
            using (var request = new SecsMessage(7, 25, true) { SecsItem = Item.A("DEMO-Test") })
                transport.EmitReceivedAsync(request).GetAwaiter().GetResult();
            Assert.Equal(SecsFormat.ASCII, transport.LastReplyItem!.Items[3].Items[0].Items[1].Items[0].Format);
            Assert.Empty(recipes.Model.CurrentPpid);
            recipes.Model.SelectedPpid = "DEMO-Test";
            Assert.True(vm.SwitchRecipeCommand.CanExecute(null));
            vm.SwitchRecipeCommand.Execute(null);
            var current = recipes.Model.CurrentRecipe;
            Send(1, Item.U4(1), Item.U4(8000), Item.F8(2), Item.F8(120));
            var valid = recipes.Model.Recipes["DEMO-Test"];
            Assert.Equal(8000, valid.ExposureUs);
            Assert.Equal(current, recipes.Model.CurrentRecipe);
            foreach (var parameters in new[]
            {
                new[] { Item.U4(1), Item.U4(0), Item.F8(2), Item.F8(120) },
                new[] { Item.U4(1), Item.U4(8000), Item.A("invalid-gain"), Item.F8(120) },
                new[] { Item.U4(1), Item.U4(8000), Item.F8(double.NaN), Item.F8(120) },
                new[] { Item.U4(1), Item.U4(8000), Item.F8(2) }
            })
            {
                Send(1, parameters);
                Assert.Equal(valid, recipes.Model.Recipes["DEMO-Test"]);
                Assert.False(vm.SwitchRecipeCommand.CanExecute(null));
                Assert.NotNull(recipes.Model.RecipeRows.Single(row => row.Key == "DEMO-Test").Error);
            }
            Send(12, Item.A("DEMO"));
            Assert.False(vm.SwitchRecipeCommand.CanExecute(null));
            Send(1, Item.U4(1), Item.U4(9000), Item.F8(3), Item.F8(150));
            Assert.True(vm.SwitchRecipeCommand.CanExecute(null));
            Assert.Null(recipes.Model.RecipeRows.Single(row => row.Key == "DEMO-Test").Error);
            Assert.Equal(current, recipes.Model.CurrentRecipe);
            vm.SwitchRecipeCommand.Execute(null);
            Assert.Equal(9000, recipes.Model.CurrentRecipe!.Value.ExposureUs);
        });
    }

    [Fact]
    public void Accepted_S7F23_Refreshes_List_Without_Json_Parsing_Or_Switching()
    {
        host.Invoke(() =>
        {
            var transport = new InMemorySecsTransport();
            using var runtime = NewRuntime(transport);
            var recipes = new RecipeTestWorkspace();
            recipes.SwitchRecipe();
            var current = recipes.Model.CurrentRecipe;
            using var controls = new SecsWorkspace(runtime, Catalog, Dispatcher.CurrentDispatcher);
            Configure(controls, recipes);
            using var vm = new RecipeTestViewModel(recipes);
            using var page = RecipeTestView.Create(vm);
            Layout(page);
            var grid = (DataGrid)page.FindName("RecipeListGrid");
            void Receive(byte function, Item body)
            {
                using var message = new SecsMessage(7, function, true) { SecsItem = body };
                transport.EmitReceivedAsync(message).GetAwaiter().GetResult();
                Layout(page);
            }
            Item Formatted(string id, ushort code) => Item.L(Item.A(id), Item.A("WORKBENCH"), Item.A("1.0"),
                Item.L(Item.L(Item.U2(code), Item.L(Item.A("DEMO")))));
            Receive(23, Formatted("FMT-TEST", 12));
            Assert.Equal((byte)0, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
            var row = Assert.Single(grid.Items.Cast<RecipeListRow>(), row => row.Key == "FMT-TEST");
            Assert.True(row.HasFormatted);
            Assert.Null(row.Error);
            Assert.Null(row.Value);
            Assert.Contains("未映射", row.Status);
            Assert.False(row.CanSwitch);
            recipes.Model.SelectedPpid = "FMT-TEST";
            Assert.False(vm.SwitchRecipeCommand.CanExecute(null));
            Receive(25, Item.A("FMT-TEST"));
            Assert.Equal(4, transport.LastReplyItem!.Count);
            Receive(23, Formatted("FMT-TEST", 13));
            Assert.Equal(3, grid.Items.Count);
            Receive(23, Formatted("DEMO-FAST", 14));
            Assert.True(grid.Items.Cast<RecipeListRow>().Single(row => row.Key == "DEMO-FAST").HasFormatted);
            Assert.Equal(current, recipes.Model.CurrentRecipe);
            Assert.Equal("DEMO-FAST", recipes.Model.CurrentPpid);
            Receive(23, Item.L(Item.A("REJECTED"), Item.A("M"), Item.A("V"),
                Item.L(Item.L(Item.A("12"), Item.A("not-a-list")))));
            Assert.DoesNotContain("REJECTED", recipes.Model.RecipeNames);
            Receive(17, Item.L(Item.A("FMT-TEST")));
            Assert.DoesNotContain("FMT-TEST", recipes.Model.RecipeNames);
            Receive(17, Item.L());
            Assert.Empty(grid.Items);
            Assert.Equal(current, recipes.Model.CurrentRecipe);
        });
    }

    [Fact]
    public void Binding_Does_Not_Overwrite_Previously_Received_Formatted_Body_With_Old_Binary_Data()
    {
        host.Invoke(() =>
        {
            using var runtime = NewRuntime(new InMemorySecsTransport());
            var codec = new ParameterBlockRecipeCodec<WorkbenchRecipe>(WorkbenchRecipe.FormattedParameterNames);
            runtime.StoreUnformattedProcessProgram("LATE", ProcessProgramCommunication.Serialize(new WorkbenchRecipe(1, 5000, 1.5, 100)));
            runtime.StoreFormattedProcessProgram(codec.Encode("LATE", new WorkbenchRecipe(1, 9000, 3, 150)));
            using var controls = new SecsWorkspace(runtime, Catalog, Dispatcher.CurrentDispatcher);
            var recipes = new RecipeTestWorkspace();
            Configure(controls, recipes, mapFormatted: true);
            Assert.Equal(9000, recipes.Model.Recipes["LATE"].ExposureUs);
            Assert.Equal(9000, codec.Decode(runtime.GetFormattedProcessProgram("LATE")!).ExposureUs);
            Assert.Empty(recipes.Model.CurrentPpid);
        });
    }

    [Fact]
    public void Formatted_Recipes_Backfill_On_Binding_And_Deleted_Notifications_Cannot_Resurrect_Them()
    {
        host.Invoke(() =>
        {
            var transport = new InMemorySecsTransport();
            using var runtime = NewRuntime(transport);
            var recipes = new RecipeTestWorkspace();
            using var controls = new SecsWorkspace(runtime, Catalog, Dispatcher.CurrentDispatcher);
            void Send(byte function, Item body)
            {
                using var message = new SecsMessage(7, function, true) { SecsItem = body };
                transport.EmitReceivedAsync(message).GetAwaiter().GetResult();
            }
            Item Formatted(string id) => Item.L(Item.A(id), Item.A("M"), Item.A("V"), Item.L());
            Send(23, Formatted("EARLY-FMT"));
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Configure(controls, recipes);
            Assert.Contains(recipes.Model.RecipeRows, row => row.Key == "EARLY-FMT" && row.HasFormatted);
            Send(23, Formatted("DELETED-FMT"));
            Send(17, Item.L(Item.A("DELETED-FMT")));
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.DoesNotContain("DELETED-FMT", recipes.Model.RecipeNames);
            controls.Dispose();
            Send(23, Formatted("AFTER-DISPOSE"));
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.DoesNotContain("AFTER-DISPOSE", recipes.Model.RecipeNames);
        });
    }

    [Fact]
    public void Received_S7F3_Automatically_Adds_And_Updates_Workbench_List_Without_Switching()
    {
        host.Invoke(() =>
        {
            var transport = new InMemorySecsTransport();
            using var runtime = NewRuntime(transport);
            var recipes = new RecipeTestWorkspace();
            recipes.SwitchRecipe();
            var current = recipes.Model.CurrentRecipe;
            using var controls = new SecsWorkspace(runtime, Catalog, Dispatcher.CurrentDispatcher);
            Configure(controls, recipes);
            using var vm = new RecipeTestViewModel(recipes);
            using var page = RecipeTestView.Create(vm);
            Layout(page);
            var grid = (DataGrid)page.FindName("RecipeListGrid");
            var payload = Encoding.UTF8.GetBytes("{\"version\":1,\"exposureUs\":8000,\"gain\":2,\"speedMmPerSec\":120}");
            using (var download = new SecsMessage(7, 3, true)
                { SecsItem = Item.L(Item.A("HOST-NEW"), Item.B(payload)) })
                transport.EmitReceivedAsync(download).GetAwaiter().GetResult();
            Layout(page); // 请求已释放后才处理 Dispatcher 通知。
            Assert.Equal((byte)0, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
            Assert.Contains(grid.Items.Cast<RecipeListRow>(), row => row.Key == "HOST-NEW" && row.Value?.ExposureUs == 8000);
            Assert.Equal(3, grid.Items.Count);
            Assert.Equal("DEMO-FAST", recipes.Model.CurrentPpid);
            Assert.Equal(current, recipes.Model.CurrentRecipe);
            Assert.Equal("DEMO-FAST", recipes.Model.SelectedPpid);
            using (var replacement = new SecsMessage(7, 3, true)
                { SecsItem = Item.L(Item.A("HOST-NEW"), Item.B(ProcessProgramCommunication.Serialize(new WorkbenchRecipe(1, 9000, 3, 150)))) })
                transport.EmitReceivedAsync(replacement).GetAwaiter().GetResult();
            Layout(page);
            Assert.Equal(3, grid.Items.Count);
            Assert.Equal(9000, recipes.Model.Recipes["HOST-NEW"].ExposureUs);
            Assert.Equal(current, recipes.Model.CurrentRecipe);
            grid.SetCurrentValue(DataGrid.SelectedValueProperty, "HOST-NEW");
            Assert.Equal("DEMO-FAST", recipes.Model.CurrentPpid);
            vm.SwitchRecipeCommand.Execute(null);
            Assert.Equal("HOST-NEW", recipes.Model.CurrentPpid);
            Assert.Equal(9000, recipes.Model.CurrentRecipe!.Value.ExposureUs);
        });
    }

    [Fact]
    public void Downloads_Received_Before_Source_Binding_Appear_When_Recipe_Management_Opens()
    {
        host.Invoke(() =>
        {
            var transport = new InMemorySecsTransport();
            using var runtime = NewRuntime(transport);
            using var controls = new SecsWorkspace(runtime, Catalog, Dispatcher.CurrentDispatcher);
            var recipes = new RecipeTestWorkspace();
            using (var download = new SecsMessage(7, 3, true)
                { SecsItem = Item.L(Item.A("EARLY"), Item.B(ProcessProgramCommunication.Serialize(new WorkbenchRecipe(1, 8000, 2, 120)))) })
                transport.EmitReceivedAsync(download).GetAwaiter().GetResult();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.DoesNotContain("EARLY", recipes.Model.RecipeNames);
            Configure(controls, recipes);
            using var page = RecipeTestView.Create(new RecipeTestViewModel(recipes));
            Layout(page);
            var grid = (DataGrid)page.FindName("RecipeListGrid");
            Assert.Contains(grid.Items.Cast<RecipeListRow>(), row => row.Key == "EARLY");
            Assert.Empty(recipes.Model.CurrentPpid);
            Assert.Contains(controls.Messages, message => message.Text.Contains("PPID=EARLY") && message.Kind == "配方");
        });
    }

    [Fact(Timeout = 30000)]
    public async Task Loopback_Host_S7F3_Refreshes_Recipe_Management_Without_Opening_Controls_View()
    {
        // 仅连接测试创建的 127.0.0.1 Host，覆盖真实传输 → Runtime → Controls → Workbench。
        using var simulator = HsmsHostSimulator.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var context = host.Invoke(() =>
        {
            var runtime = NewRuntime();
            var recipes = new RecipeTestWorkspace();
            var controls = new SecsWorkspace(runtime, Catalog, Dispatcher.CurrentDispatcher);
            Configure(controls, recipes, mapFormatted: true);
            controls.ImportHostRecipes();
            return (Runtime: runtime, Recipes: recipes, Controls: controls);
        });
        try
        {
            await context.Runtime.ConnectAsync(true, "127.0.0.1", simulator.Port, 0);
            await simulator.WaitForDeviceAsync(timeout.Token);
            await simulator.CompleteSelectAsync(timeout.Token);
            Assert.True(host.WaitUntil(() => context.Runtime.IsConnected, TimeSpan.FromSeconds(5)));
            async Task<HsmsHostSimulator.HsmsMessage> Exchange(byte function, byte[] bytes, uint systemBytes)
            {
                simulator.SendDataMessage(7, function, true, 0, systemBytes, bytes, timeout.Token);
                var message = await simulator.ReadMessageAsync(timeout.Token);
                Assert.Equal((byte)7, message.S);
                Assert.Equal((byte)(function + 1), message.F);
                Assert.Equal(systemBytes, message.SystemBytes);
                Assert.False(message.WBit);
                return message;
            }
            byte[] Ascii(string value) => [0x41, (byte)value.Length, .. Encoding.ASCII.GetBytes(value)];
            var fast = Ascii("DEMO-FAST");
            var precise = Ascii("DEMO-PRECISE");
            var directory = await Exchange(19, [], 1);
            byte[] firstOrder = [0x01, 0x02, .. fast, .. precise];
            byte[] secondOrder = [0x01, 0x02, .. precise, .. fast];
            Assert.True(directory.Body.SequenceEqual(firstOrder) || directory.Body.SequenceEqual(secondOrder));
            var read = await Exchange(5, fast, 2);
            var expectedJson = Encoding.UTF8.GetBytes("{\"version\":1,\"exposureUs\":5000,\"gain\":1.5,\"speedMmPerSec\":100}");
            Assert.Equal(new byte[] { 0x01, 0x02 }.Concat(fast).Concat(new byte[] { 0x21, (byte)expectedJson.Length }).Concat(expectedJson).ToArray(), read.Body);
            var formattedRead = await Exchange(25, fast, 0x25);
            byte[] expectedFormatted = [0x01, 4, .. fast, .. Ascii("WORKBENCH"), .. Ascii("1.0"),
                0x01, 1, 0x01, 2, 0xA9, 2, 0, 1, 0x01, 4,
                0xB1, 4, 0, 0, 0, 1, 0xB1, 4, 0, 0, 0x13, 0x88,
                0x81, 8, 0x3F, 0xF8, 0, 0, 0, 0, 0, 0,
                0x81, 8, 0x40, 0x59, 0, 0, 0, 0, 0, 0];
            Assert.Equal(expectedFormatted, formattedRead.Body); // 独立字节期望，验证线上不是空 L[4] 模板。
            var id = Encoding.ASCII.GetBytes("HOST-WIRE");
            var json = Encoding.UTF8.GetBytes("{\"version\":1,\"exposureUs\":8000,\"gain\":2,\"speedMmPerSec\":120}");
            // 独立编码 SECS-II L[2], A PPID, B PPBODY，不使用生产组包器生成请求。
            byte[] body = [0x01, 0x02, 0x41, (byte)id.Length, .. id, 0x21, (byte)json.Length, .. json];
            simulator.SendDataMessage(7, 3, true, 0, 0x12345678, body, timeout.Token);
            var reply = await simulator.ReadMessageAsync(timeout.Token);
            Assert.Equal((byte)7, reply.S);
            Assert.Equal((byte)4, reply.F);
            Assert.Equal(0x12345678u, reply.SystemBytes);
            Assert.Equal(new byte[] { 0x21, 0x01, 0x00 }, reply.Body);
            Assert.True(host.WaitUntil(() => context.Recipes.Model.Recipes.ContainsKey("HOST-WIRE"), TimeSpan.FromSeconds(5)),
                host.Invoke(() => string.Join(Environment.NewLine, context.Controls.Messages.Select(message => message.Text))));
            host.Invoke(() =>
            {
                using var page = RecipeTestView.Create(new RecipeTestViewModel(context.Recipes));
                Layout(page);
                var grid = (DataGrid)page.FindName("RecipeListGrid");
                Assert.Contains(grid.Items.Cast<RecipeListRow>(), row => row.Key == "HOST-WIRE" && row.Value?.ExposureUs == 8000);
                Assert.Empty(context.Recipes.Model.CurrentPpid);
            });
            var rejected = await Exchange(17, [0x01, 0x02, .. fast, .. Ascii("MISSING")], 3);
            Assert.Equal(new byte[] { 0x21, 0x01, 0x04 }, rejected.Body);
            Assert.True(host.Invoke(() => context.Recipes.Model.Recipes.ContainsKey("DEMO-FAST")));
            var deleted = await Exchange(17, [0x01, 0x01, .. Ascii("HOST-WIRE")], 4);
            Assert.Equal(new byte[] { 0x21, 0x01, 0x00 }, deleted.Body);
            Assert.True(host.WaitUntil(() => !context.Recipes.Model.RecipeNames.Contains("HOST-WIRE"), TimeSpan.FromSeconds(5)));
            var missing = await Exchange(5, Ascii("HOST-WIRE"), 5);
            Assert.Equal(new byte[] { 0x01, 0x00 }, missing.Body);
            var cleared = await Exchange(17, [0x01, 0x00], 6);
            Assert.Equal(new byte[] { 0x21, 0x01, 0x00 }, cleared.Body);
            Assert.True(host.WaitUntil(() => context.Recipes.Model.RecipeRows.Count == 0, TimeSpan.FromSeconds(5)));
            var empty = await Exchange(19, [], 7);
            Assert.Equal(new byte[] { 0x01, 0x00 }, empty.Body);
        }
        finally
        {
            host.Invoke(() => context.Controls.Dispose());
            try { await context.Runtime.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
            finally { context.Runtime.Dispose(); }
        }
    }

    [Fact]
    public void Zero_Prefixed_Body_Still_Adds_Ppid_But_Cannot_Switch_And_Valid_Replacement_Recovers()
    {
        host.Invoke(() =>
        {
            var transport = new InMemorySecsTransport();
            using var runtime = NewRuntime(transport);
            var recipes = new RecipeTestWorkspace();
            recipes.SwitchRecipe();
            var current = recipes.Model.CurrentRecipe;
            using var controls = new SecsWorkspace(runtime, Catalog, Dispatcher.CurrentDispatcher);
            Configure(controls, recipes);
            using var vm = new RecipeTestViewModel(recipes);
            using var page = RecipeTestView.Create(vm);
            Layout(page);
            using (var bad = new SecsMessage(7, 3, true)
                { SecsItem = Item.L(Item.A("DEMO-Test"), Item.B(new byte[] { 0, 1, 128, 255 })) })
                transport.EmitReceivedAsync(bad).GetAwaiter().GetResult();
            Layout(page);
            var grid = (DataGrid)page.FindName("RecipeListGrid");
            var row = Assert.Single(grid.Items.Cast<RecipeListRow>(), row => row.Key == "DEMO-Test");
            Assert.Contains("0x00", row.Error);
            Assert.False(row.CanSwitch);
            Assert.Equal(new byte[] { 0, 1, 128, 255 }, runtime.GetUnformattedProcessProgram("DEMO-Test"));
            Assert.Null(row.Value);
            Assert.DoesNotContain("DEMO-Test", recipes.Model.Recipes.Keys);
            grid.SetCurrentValue(DataGrid.SelectedValueProperty, "DEMO-Test");
            Assert.False(vm.SwitchRecipeCommand.CanExecute(null));
            recipes.SwitchRecipe(); // 直接流程调用也不能绕过解析守卫。
            Assert.Equal(current, recipes.Model.CurrentRecipe);
            Assert.Equal("DEMO-FAST", recipes.Model.CurrentPpid);
            using (var good = new SecsMessage(7, 3, true)
                { SecsItem = Item.L(Item.A("DEMO-Test"), Item.B(ProcessProgramCommunication.Serialize(new WorkbenchRecipe(1, 8000, 2, 120)))) })
                transport.EmitReceivedAsync(good).GetAwaiter().GetResult();
            Layout(page);
            row = Assert.Single(grid.Items.Cast<RecipeListRow>(), row => row.Key == "DEMO-Test");
            Assert.Null(row.Error);
            Assert.True(row.CanSwitch);
            Assert.Equal(3, grid.Items.Count); // 两个示例 + 一个下载项，修复正文不重复新增行。
            Assert.Equal(current, recipes.Model.CurrentRecipe);
            grid.SetCurrentValue(DataGrid.SelectedValueProperty, "DEMO-Test");
            vm.SwitchRecipeCommand.Execute(null);
            Assert.Equal("DEMO-Test", recipes.Model.CurrentPpid);
            Assert.Equal(8000, recipes.Model.CurrentRecipe!.Value.ExposureUs);
        });
    }

    [Fact]
    public void Invalid_And_Late_Downloads_Do_Not_Replace_The_Workbench_Library()
    {
        host.Invoke(() =>
        {
            var transport = new InMemorySecsTransport();
            using var runtime = NewRuntime(transport);
            var recipes = new RecipeTestWorkspace();
            using var controls = new SecsWorkspace(runtime, Catalog, Dispatcher.CurrentDispatcher);
            Configure(controls, recipes);
            using var page = RecipeTestView.Create(new RecipeTestViewModel(recipes));
            Layout(page);
            var original = recipes.Model.Recipes["DEMO-FAST"];
            using (var invalid = new SecsMessage(7, 3, true)
                { SecsItem = Item.L(Item.A("DEMO-FAST"), Item.B(Encoding.UTF8.GetBytes("not-json"))) })
                transport.EmitReceivedAsync(invalid).GetAwaiter().GetResult();
            Layout(page);
            Assert.Equal(original, recipes.Model.Recipes["DEMO-FAST"]);
            Assert.StartsWith("失败", controls.Model.RecipeStatus);
            Assert.Contains("S7F3 PPID=DEMO-FAST", controls.Model.RecipeStatus);
            Assert.Contains(controls.Messages, message => message.Kind == "错误" && message.Text.Contains("PPID=DEMO-FAST"));
            using (var late = new SecsMessage(7, 3, true)
                { SecsItem = Item.L(Item.A("LATE"), Item.B(ProcessProgramCommunication.Serialize(original))) })
                transport.EmitReceivedAsync(late).GetAwaiter().GetResult();
            controls.Dispose();
            Layout(page);
            Assert.DoesNotContain("LATE", recipes.Model.RecipeNames);
        });
    }

    [Fact]
    public void Import_Download_And_Read_Do_Not_Switch_The_Host_Current_Ppid()
    {
        host.Invoke(() =>
        {
            using var runtime = NewRuntime(out var registry);
            var recipes = new RecipeTestWorkspace();
            using var controls = new SecsWorkspace(runtime, Catalog, Dispatcher.CurrentDispatcher);
            Configure(controls, recipes);
            using var vm = new SecsViewModel(controls);
            Assert.Empty(runtime.GetProcessPrograms()); // 装配不导入、不切换。
            controls.ImportHostRecipes();
            Assert.Equal(2, runtime.GetProcessPrograms().Count);
            Assert.Empty(recipes.Model.CurrentPpid);
            recipes.SwitchRecipe();
            Assert.Equal("DEMO-FAST", recipes.Model.CurrentPpid);
            controls.UseCurrentRecipePpid();
            Assert.Equal("DEMO-FAST", controls.Model.RecipePpid);
            controls.Model.RecipePpid = "DEMO-PRECISE";
            var incoming = Encoding.UTF8.GetBytes("{\"version\":1,\"exposureUs\":9000,\"gain\":2.5,\"speedMmPerSec\":200}");
            using var request = new SecsMessage(7, 3, true)
                { SecsItem = Item.L(Item.A("DEMO-PRECISE"), Item.B(incoming)) };
            using var ack = registry.GetHandler("S7F3").GetReplyItem(request);
            Assert.Equal((byte)0, ack.GetMemory<byte>().Span[0]);
            Assert.Equal(10000, recipes.Model.Recipes["DEMO-PRECISE"].ExposureUs); // 接收字节不自动改宿主数据。
            vm.ReadRecipeToHostCommand.Execute(null);
            Assert.Equal(9000, recipes.Model.Recipes["DEMO-PRECISE"].ExposureUs);
            Assert.Equal("DEMO-FAST", recipes.Model.CurrentPpid);
            controls.Model.SelectedProcessProgram = controls.Model.ProcessPrograms.Single(x => x.PPID == "DEMO-PRECISE");
            Assert.Equal("DEMO-FAST", recipes.Model.CurrentPpid);
            recipes.Model.SelectedPpid = "DEMO-PRECISE";
            Assert.Equal("DEMO-FAST", recipes.Model.CurrentPpid);
            recipes.SwitchRecipe();
            Assert.Equal("DEMO-PRECISE", recipes.Model.CurrentPpid);
            Assert.Equal(200d, recipes.Model.CurrentRecipe!.Value.SpeedMmPerSec);
            runtime.StoreUnformattedProcessProgram("DEMO-PRECISE", Encoding.UTF8.GetBytes(
                "{\"version\":1,\"exposureUs\":9000,\"gain\":2.5,\"speedMmPerSec\":250}"));
            controls.ReadRecipeToHost();
            Assert.Equal(250d, recipes.Model.Recipes["DEMO-PRECISE"].SpeedMmPerSec);
            Assert.Equal(200d, recipes.Model.CurrentRecipe!.Value.SpeedMmPerSec); // 同名更新不覆盖当前快照。
            recipes.SwitchRecipe();
            Assert.Equal(250d, recipes.Model.CurrentRecipe!.Value.SpeedMmPerSec);
        });
    }

    [Fact]
    public void Invalid_Json_Does_Not_Replace_Host_Data_And_Offline_Import_Is_Guarded()
    {
        host.Invoke(() =>
        {
            using var runtime = NewRuntime();
            var recipes = new RecipeTestWorkspace();
            using var controls = new SecsWorkspace(runtime, Catalog, Dispatcher.CurrentDispatcher);
            Configure(controls, recipes);
            using var vm = new SecsViewModel(controls);
            recipes.Model.Recipes["INVALID"] = new(99, 100, 1, 1);
            controls.ImportHostRecipes();
            Assert.Empty(runtime.GetProcessPrograms()); // 先完整校验，不留下半批数据。
            recipes.Model.Recipes.Remove("INVALID");
            controls.Model.IsConnected = true;
            Assert.False(vm.ImportHostRecipesCommand.CanExecute(null));
            controls.ImportHostRecipes();
            Assert.Empty(runtime.GetProcessPrograms());
            Assert.Contains("先断开", controls.Model.RecipeStatus);
            controls.Model.IsConnected = false;
            controls.ImportHostRecipes();
            var original = recipes.Model.Recipes["DEMO-FAST"];
            runtime.StoreUnformattedProcessProgram("DEMO-FAST", Encoding.UTF8.GetBytes("not-json"));
            controls.Model.RecipePpid = "DEMO-FAST";
            controls.ReadRecipeToHost();
            Assert.Equal(original, recipes.Model.Recipes["DEMO-FAST"]);
            Assert.Empty(controls.Model.RecipeJsonPreview);
            Assert.StartsWith("失败", controls.Model.RecipeStatus);
            controls.Dispose();
            Assert.False(controls.Model.HasRecipeSource);
            Assert.False(vm.BuildRecipeRequestCommand.CanExecute(null));
            Assert.Equal(2, runtime.GetProcessPrograms().Count); // 借用者不释放 Runtime。
        });
    }

    [Fact]
    public void Controls_Templates_Do_Not_Send_Delete_Or_Switch_And_Inputs_Invalidate_Old_Text()
    {
        host.Invoke(() =>
        {
            using var runtime = NewRuntime();
            var recipes = new RecipeTestWorkspace();
            using var controls = new SecsWorkspace(runtime, Catalog, Dispatcher.CurrentDispatcher);
            Configure(controls, recipes);
            using var vm = new SecsViewModel(controls);
            controls.ImportHostRecipes();
            controls.Model.RecipePpid = "DEMO-FAST";
            controls.Model.RecipeFormattedSml = """
                'S7F23' W
                <L [4]
                    <A [9] 'DEMO-FAST'>
                    <A [5] 'MODEL'>
                    <A [3] '1.0'>
                    <L [0]
                    >
                >
                .
                """;
            int sends = 0;
            runtime.OnSend += (_, _) => { sends++; return Task.CompletedTask; };
            byte[] functions = [1, 3, 5, 17, 19, 23, 25, 17];
            for (int i = 0; i < functions.Length; i++)
            {
                controls.Model.RecipeRequestIndex = i;
                vm.BuildRecipeRequestCommand.Execute(null);
                using var message = controls.Model.RecipeRequestSml.ToSecsMessage();
                Assert.Equal(functions[i], message.F);
                Assert.True(message.ReplyExpected);
            }
            Assert.Equal(0, sends);
            Assert.Equal(2, runtime.GetProcessPrograms().Count);
            Assert.Empty(recipes.Model.CurrentPpid);
            Assert.True(vm.CopyRecipeRequestCommand.CanExecute(null));
            controls.Model.RecipePpid = "001001";
            Assert.Empty(controls.Model.RecipeRequestSml);
            Assert.False(vm.CopyRecipeRequestCommand.CanExecute(null));
        });
    }

    [Fact]
    public void App_Composes_Only_Host_Data_Into_Controls_And_Registers_The_Switch_Page()
    {
        host.Invoke(() =>
        {
            var container = ContainerLocator.Container;
            var recipes = container.Resolve<RecipeTestWorkspace>();
            var shell = Assert.IsType<MainWindowViewModel>(Application.Current.MainWindow.DataContext);
            Assert.Contains(shell.NavItems, item => item.Title == "配方管理");
            Assert.DoesNotContain(shell.NavItems, item => item.Title == "手动切换配方");
            Assert.True(container.Resolve<SecsWorkspace>().Model.HasRecipeSource);
            var protocolLibrary = container.Resolve<SecsGemRuntime>().GetProcessPrograms();
            Assert.Contains(protocolLibrary, recipe => recipe.PPID == "DEMO-FAST" && recipe.HasBinary);
            Assert.Contains(protocolLibrary, recipe => recipe.PPID == "DEMO-PRECISE" && recipe.HasBinary);
            using var page = Assert.IsType<RecipeTestView>(container.Resolve<object>(nameof(RecipeTestView)));
            Assert.Same(recipes.Model, Assert.IsType<RecipeTestViewModel>(page.DataContext).Model);
            Layout(page);
            Assert.Equal("配方管理", Assert.Single(Descendants(page).OfType<WPFCustomControls.Controls.PageHeaderView>()).Title);
        });
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
