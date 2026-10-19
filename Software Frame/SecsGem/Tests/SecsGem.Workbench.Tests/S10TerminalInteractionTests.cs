using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DataManager.Runtime.Log;
using Prism.DryIoc;
using Prism.Ioc;
using Secs4Net;
using SecsGem.Controls;
using SecsGem.Controls.Services;
using SecsGem.Controls.Terminal;
using SecsGem.Controls.ViewModels;
using SecsGem.Runtime;
using SecsGem.Runtime.Variable;
using SecsGem.Tests.Fakes;

namespace SecsGem.Workbench.Tests;

[Collection("WorkbenchAppBoot")]
public sealed class S10TerminalInteractionTests(WorkbenchAppFixture host)
{
    private static IEnumerable<DependencyObject> Children(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var nested in Children(child)) yield return nested;
        }
    }

    [Fact]
    public void Modeless_Queue_Confirms_Only_Explicit_Read_And_Shutdown_Drops_Unread_Window()
    {
        host.Invoke(() =>
        {
            var container = new DryIocContainerExtension();
            container.RegisterSingleton<LogManager>();
            new SecsGemModule().RegisterTypes(container);
            var transport = new InMemorySecsTransport();
            using var runtime = new SecsGemRuntime(container, transport);
            using var display = new WpfTerminalDisplay(runtime);
            runtime.TerminalDisplay = display;
            using var workspace = new SecsWorkspace(runtime, ContainerLocator.Container.Resolve<VIDManager>(), Dispatcher.CurrentDispatcher);
            using var vm = new SecsViewModel(workspace);
            void Receive(byte function, Item body)
            {
                using var request = new SecsMessage(10, function, true) { SecsItem = body };
                transport.EmitReceivedAsync(request).GetAwaiter().GetResult();
                Assert.Equal((byte)0, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
            }
            Receive(5, Item.L(Item.B(7), Item.L(Item.A("First"), Item.A(""), Item.A("Last"))));
            Receive(3, Item.L(Item.B(8), Item.A("Second")));
            // UI 尚未处理任何投递，协议已确认两条消息；不能同步 Dispatcher.Invoke/ShowDialog。
            runtime.DrainTerminalMessagesAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var first = Assert.Single(Application.Current.Windows.OfType<MessagePrompt>());
            Assert.Equal("First\r\n\r\nLast", first.Message);
            Assert.Contains("TID=7", first.Title);
            Assert.All(runtime.GetTerminalMessageHistory(), entry => Assert.False(entry.IsRead));
            first.UpdateLayout();
            Children(first).OfType<Button>().Single(button => Equals(button.Content, "OK"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var second = Assert.Single(Application.Current.Windows.OfType<MessagePrompt>());
            Assert.Equal("Second", second.Message);
            Assert.True(runtime.GetTerminalMessageHistory()[0].IsRead);
            Assert.False(runtime.GetTerminalMessageHistory()[1].IsRead);
            workspace.RefreshState();
            Assert.Equal(2, workspace.Model.TerminalMessages.Count);
            workspace.Model.SelectedTerminalMessage = workspace.Model.TerminalMessages[1];
            Assert.True(vm.AcknowledgeTerminalCommand.CanExecute(null));
            display.Dispose(); // 宿主关闭不是操作员确认。
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Empty(Application.Current.Windows.OfType<MessagePrompt>());
            Assert.Equal(TerminalDeliveryState.Failed, runtime.GetTerminalMessageHistory()[1].State);
            Assert.False(runtime.GetTerminalMessageHistory()[1].IsRead);
            workspace.RefreshState();
            Assert.False(vm.AcknowledgeTerminalCommand.CanExecute(null));
            using var late = new SecsMessage(10, 3, true) { SecsItem = Item.L(Item.B(0), Item.A("Late")) };
            transport.EmitReceivedAsync(late).GetAwaiter().GetResult();
            Assert.Equal((byte)1, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
            Assert.Equal(2, runtime.GetTerminalMessageHistory().Count);
        });
    }
}
