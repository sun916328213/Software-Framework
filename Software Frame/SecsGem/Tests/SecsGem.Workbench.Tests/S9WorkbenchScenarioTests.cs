using System.Text;
using System.Windows.Threading;
using DataManager.Runtime.Log;
using Prism.DryIoc;
using Prism.Ioc;
using Secs4Net;
using SecsGem.Controls;
using SecsGem.Controls.Services;
using SecsGem.Runtime;
using SecsGem.Runtime.Variable;
using SecsGem.Tests.Fakes;
using SecsGem.Tests.Infrastructure;
using SecsGem.Workbench.Models;
using SecsGem.Workbench.Services;

namespace SecsGem.Workbench.Tests;

[Collection("WorkbenchAppBoot")]
public sealed class S9WorkbenchScenarioTests(WorkbenchAppFixture host)
{
    [Fact]
    public void Download_Timeout_And_Oversize_Protect_Business_List_And_Applied_Snapshot()
    {
        host.Invoke(() =>
        {
            var container = new DryIocContainerExtension();
            container.RegisterSingleton<LogManager>();
            new SecsGemModule().RegisterTypes(container);
            var transport = new InMemorySecsTransport();
            var clock = new ManualScenarioTimeProvider();
            using var runtime = new SecsGemRuntime(container, transport, clock);
            runtime.ConfigureProtocolScenarios(new() { MaxMessageBodyBytes = 128, ProcessProgramTransferTimeout = TimeSpan.FromSeconds(30) });
            var recipes = new RecipeTestWorkspace();
            recipes.SwitchRecipe();
            var applied = recipes.Model.CurrentRecipe;
            using var controls = new SecsWorkspace(runtime, ContainerLocator.Container.Resolve<VIDManager>(), Dispatcher.CurrentDispatcher);
            controls.ConfigureRecipeSource(recipes.Model.Recipes, () => recipes.Model.CurrentPpid, recipes.Model.NotifyRecipesChanged,
                recipes.Model.NotifyRecipeReceived, recipes.Model.NotifyRecipesDeleted, recipes.Model.NotifyFormattedRecipeReceived,
                formattedCodec: new ParameterBlockRecipeCodec<WorkbenchRecipe>(WorkbenchRecipe.FormattedParameterNames));
            controls.ImportHostRecipes();
            transport.ConnectAsync(true, "127.0.0.1", 1, 0).GetAwaiter().GetResult();
            void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            void Send(byte function, Item body)
            {
                using var message = new SecsMessage(7, function, true) { SecsItem = body };
                transport.EmitReceivedAsync(message).GetAwaiter().GetResult();
                Flush();
            }
            void Drain() => runtime.DrainProtocolScenariosAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            var replacement = Encoding.UTF8.GetBytes("{\"version\":1,\"exposureUs\":8000,\"gain\":2,\"speedMmPerSec\":120}");

            // 正常完成：更新可用业务参数，但保留已应用的 DEMO-FAST 快照。
            Send(1, Item.L(Item.A("DEMO-FAST"), Item.U4((uint)replacement.Length)));
            Send(3, Item.L(Item.A("DEMO-FAST"), Item.B(replacement)));
            Drain();
            Assert.Equal(8000, recipes.Model.Recipes["DEMO-FAST"].ExposureUs);
            Assert.Equal(applied, recipes.Model.CurrentRecipe);
            Assert.Equal("DEMO-FAST", recipes.Model.CurrentPpid);

            // 等待超时：通知 Host，但不创建虚假业务行或改变应用快照。
            Send(1, Item.L(Item.A("WAIT-A"), Item.U4((uint)replacement.Length)));
            clock.Advance(TimeSpan.FromSeconds(30));
            Drain();
            Flush();
            Assert.Contains(transport.Sent, frame => frame.Order == "S9F13" && frame.Item.Items[1].GetString() == "WAIT-A");
            Assert.DoesNotContain("WAIT-A", recipes.Model.RecipeNames);
            Assert.Equal(applied, recipes.Model.CurrentRecipe);

            // 超大替换：协议拒绝后不污染有效业务值，也不改当前应用。
            Send(3, Item.L(Item.A("DEMO-FAST"), Item.B(new byte[256])));
            Assert.Equal("S9F11", transport.Sent.Last().Order);
            Assert.Equal(replacement, runtime.GetUnformattedProcessProgram("DEMO-FAST"));
            Assert.Equal(8000, recipes.Model.Recipes["DEMO-FAST"].ExposureUs);
            Assert.Equal(applied, recipes.Model.CurrentRecipe);

            // 迟到下载仍可成为待手动应用的配方，不恢复旧等待，不自动选中。
            Send(3, Item.L(Item.A("WAIT-A"), Item.B(replacement.ToArray())));
            Assert.Equal(8000, recipes.Model.Recipes["WAIT-A"].ExposureUs);
            Assert.Equal("DEMO-FAST", recipes.Model.CurrentPpid);
            Assert.Equal(applied, recipes.Model.CurrentRecipe);
            Assert.Contains(controls.Messages, entry => entry.Text.Contains("S9F13"));
            Assert.Contains(controls.Messages, entry => entry.Text.Contains("S9F11"));
        });
    }
}
