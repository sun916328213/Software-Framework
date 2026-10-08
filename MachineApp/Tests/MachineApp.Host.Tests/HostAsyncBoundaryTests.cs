using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DataManager.Core.Log.Definition;
using MachineApp.Business.Flow;
using MachineApp.Business.Ports;
using MachineApp.Data;
using MachineApp.UI.Model;
using MachineApp.UI.ViewModel;
using MachineApp.UI.WindowUI;
using Prism.Ioc;
using SecsGem.Core;
using WPFControls.Controls.Diagnostics;
using Xunit;

namespace MachineApp.Host.Tests;

[Collection("ProductionComposition")]
public sealed class HostAsyncBoundaryTests(ProductionFixture fixture)
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, false)]
    [InlineData(9, 0, false)]
    [InlineData(10, 0, false)]
    [InlineData(59, 0, false)]
    [InlineData(0, 1, false)]
    public async Task MaintenanceRunsOnlyAtHourBoundaryAndReportsFault(int minute, int second, bool expected)
    {
        var log = new Log();
        var process = CreateProcess(log, Proxy<IProductionPlc>(name => name switch
        {
            "get_IsOnline" => false,
            "ReadData" => "0",
            _ => throw new NotSupportedException(name)
        }));
        var oldPath = GlobalData.Path_Image;
        try
        {
            // Invalid input fails before enumeration/deletion and proves the async fault reaches the log.
            GlobalData.Path_Image = "";
            await RefreshAsync(process, new DateTime(2026, 10, 4, 12, minute, second));
            Assert.Equal(expected ? 1 : 0, log.Messages.Count);
            if (expected) Assert.Contains("directoryPath", Assert.Single(log.Messages));
        }
        finally { GlobalData.Path_Image = oldPath; }
    }

    [Fact]
    public async Task RefreshSkipsReentryUntilPendingOperationCompletes()
    {
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        var plc = Proxy<IProductionPlc>(name => name switch
        {
            "get_IsOnline" => true,
            "ReadData" => Read(),
            _ => throw new NotSupportedException(name)
        });
        string Read() { reads++; return "OnlineRemote"; }
        var secs = Proxy<ISecsGemRuntime>(name => name switch
        {
            "GoOnlineRemoteAsync" => release.Task,
            "GetState" => new SecsGemState(false, SecsGemMode.Offline, SecsGemEquipmentStatus.Idle),
            _ => throw new NotSupportedException(name)
        });
        var process = CreateProcess(new Log(), plc, secs);
        var now = new DateTime(2026, 10, 4, 12, 1, 1);
        var pending = RefreshAsync(process, now);
        try
        {
            Assert.False(pending.IsCompleted);
            Assert.Equal(2, reads);
            await RefreshAsync(process, now);
            Assert.Equal(2, reads);
        }
        finally { release.TrySetResult(true); await pending; }
        await RefreshAsync(process, now);
        Assert.Equal(4, reads);
    }

    [Fact]
    public async Task MaintenanceFaultReleasesRefreshGate()
    {
        var log = new Log();
        var process = CreateProcess(log, Proxy<IProductionPlc>(name => name == "ReadData" ? "0" : false));
        var oldPath = GlobalData.Path_Image;
        try
        {
            GlobalData.Path_Image = "";
            var now = new DateTime(2026, 10, 4, 12, 0, 0);
            await RefreshAsync(process, now);
            await RefreshAsync(process, now);
            Assert.Equal(2, log.Messages.Count);
        }
        finally { GlobalData.Path_Image = oldPath; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CloseReportsAsyncFailureAndContinuesRemainingCleanup(bool deviceFailure)
    {
        var fault = new InvalidOperationException("shutdown-fault");
        var errors = new List<Exception>();
        var order = new List<string>();
        var printer = Proxy<IProductionPrinter>(_ => { order.Add("printer"); return null; });
        var plc = Proxy<IProductionPlc>(_ => { order.Add("plc"); return Task.CompletedTask; });
        var secs = Proxy<ISecsGemRuntime>(_ =>
        {
            order.Add("disconnect");
            return deviceFailure ? Task.CompletedTask : Task.FromException(fault);
        });
        var devices = Proxy<IProductionDevices>(_ =>
        {
            order.Add("devices");
            return new ValueTask(deviceFailure ? Task.FromException(fault) : Task.CompletedTask);
        });
        var close = new Close(() => printer, () => plc, () => devices, secs, new TryCatchHelper(errors.Add));
        var file = Path.GetFullPath("Config/GlobalData.json");
        var original = File.Exists(file) ? File.ReadAllBytes(file) : null;
        try
        {
            Task? task = null;
            fixture.Invoke(_ => task = close.ClosedAsync());
            await task!;
            Assert.Same(fault, Assert.Single(errors));
            Assert.Equal(new[] { "printer", "plc", "disconnect", "devices" }, order);
        }
        finally
        {
            if (original != null) File.WriteAllBytes(file, original);
            else if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public async Task StopRefreshDrainsPendingTickAndPreventsFurtherReads()
    {
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        var plc = Proxy<IProductionPlc>(name => name == "ReadData" ? Read() : true);
        string Read() { reads++; return "OnlineRemote"; }
        var secs = Proxy<ISecsGemRuntime>(name => name == "GoOnlineRemoteAsync" ?
            release.Task.WaitAsync(TimeSpan.FromSeconds(10)) :
            new SecsGemState(false, SecsGemMode.Offline, SecsGemEquipmentStatus.Idle));
        var process = CreateProcess(new Log(), plc, secs);
        Task? tick = null;
        Task? stop = null;
        var now = new DateTime(2026, 10, 4, 12, 1, 1);
        try
        {
            fixture.Invoke(_ =>
            {
                var tickMethod = typeof(AutoProcess).GetMethod("TickAsync", BindingFlags.NonPublic | BindingFlags.Instance);
                var stopMethod = typeof(AutoProcess).GetMethod("StopAsync");
                Assert.NotNull(tickMethod);
                Assert.NotNull(stopMethod);
                tick = (Task)tickMethod.Invoke(process, [now])!;
                Assert.False(tick.IsCompleted);
                stop = (Task)stopMethod.Invoke(process, null)!;
                Assert.False(stop.IsCompleted);
                Assert.Equal(2, reads);
            });
            release.SetResult(true);
            await stop!.WaitAsync(TimeSpan.FromSeconds(10));
            await tick!;
            await RefreshAsync(process, now);
            Assert.Equal(2, reads);
        }
        finally { release.TrySetResult(true); if (tick != null) await tick.WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    [Fact]
    public async Task CloseKeepsDispatcherResponsiveAndWaitsForDisconnectThenDevices()
    {
        var order = new List<string>();
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var printer = Proxy<IProductionPrinter>(_ => { order.Add("printer"); return null; });
        var plc = Proxy<IProductionPlc>(_ => { order.Add("plc"); return Task.CompletedTask; });
        var secs = Proxy<ISecsGemRuntime>(_ => { order.Add("disconnect"); started.SetResult(); return disconnected.Task.WaitAsync(TimeSpan.FromSeconds(10)); });
        var devices = Proxy<IProductionDevices>(_ => { order.Add("devices"); disposing.SetResult(); return new ValueTask(disposed.Task); });
        var errors = new List<Exception>();
        var close = new Close(() => printer, () => plc, () => devices, secs, new TryCatchHelper(errors.Add));
        var file = Path.GetFullPath("Config/GlobalData.json");
        var original = File.Exists(file) ? File.ReadAllBytes(file) : null;
        var method = typeof(Close).GetMethod("ClosedAsync") ?? typeof(Close).GetMethod("Closed")!;
        var operation = Task.Run(async () =>
        {
            Task? pending = null;
            fixture.Invoke(_ => pending = method.Invoke(close, null) as Task);
            if (pending != null) await pending;
        });
        var responsive = false;
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var heartbeat = Task.Run(() => fixture.Invoke(_ => { }));
            responsive = await Task.WhenAny(heartbeat, Task.Delay(500)) == heartbeat;
            Assert.False(operation.IsCompleted);
            Assert.Equal(new[] { "printer", "plc", "disconnect" }, order);
            disconnected.SetResult();
            await disposing.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(operation.IsCompleted);
            disposed.SetResult();
            await operation.WaitAsync(TimeSpan.FromSeconds(10));
            await heartbeat;
            Assert.True(responsive, "UI dispatcher blocked while waiting for disconnect");
            Assert.Equal(new[] { "printer", "plc", "disconnect", "devices" }, order);
            Assert.Empty(errors);
        }
        finally
        {
            disconnected.TrySetResult();
            disposed.TrySetResult();
            await operation.WaitAsync(TimeSpan.FromSeconds(10));
            if (original != null) File.WriteAllBytes(file, original);
            else if (File.Exists(file)) File.Delete(file);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RecipeDialogCommitsOnlyOnConfirmation(bool modify, bool confirm)
    {
        string[] paths = ["Config/ProcessInfo/LabelPosition.json", "Config/ProcessInfo/Recipe",
            "Config/ProcessInfo/FOSBName.json", "Config/ProcessInfo/BagType.json"];
        var files = paths.ToDictionary(p => p, p => File.Exists(p) ? File.ReadAllBytes(p) : null);
        Directory.CreateDirectory("Config/ProcessInfo");
        foreach (var path in paths) File.WriteAllText(path, "[]");
        try
        {
        fixture.Invoke(container =>
        {
            var viewModel = new RecipeViewModel(container);
            var model = viewModel.Model;
            var original = model.Recipes;
            var oldRecipe = model.CurRecipe;
            var existing = new CRecipe { ID = "1" };
            existing.LabelInfoCollection.LabelInfo_First.Add(new CLabelInfo_Single { ID = 7 });
            model.Recipes = [existing];
            viewModel.CurRecipe = existing;
            viewModel.SelectIndex = 0;
            var view = new MachineApp.UI.View.Recipe { DataContext = viewModel };
            var grid = (DataGrid)((Border)((DockPanel)view.Content).Children[1]).Child;
            view.Measure(new Size(800, 600));
            view.Arrange(new Rect(0, 0, 800, 600));
            var seen = false;
            Exception? editError = null;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            timer.Tick += (_, _) =>
            {
                var dialog = Application.Current.Windows.OfType<RecipeSetting>().FirstOrDefault();
                if (dialog == null) return;
                seen = true;
                try
                {
                    viewModel.CurRecipe.Num_Desiccant = 99;
                    if (modify) viewModel.CurRecipe.LabelInfoCollection.LabelInfo_First[0].ID = 42;
                    else viewModel.CurRecipe.LabelInfoCollection.LabelInfo_First.Add(new CLabelInfo_Single { ID = 42 });
                    if (modify) viewModel.CurRecipe.ID = "modified";
                }
                catch (Exception error) { editError = error; }
                dialog.DialogResult = confirm;
                timer.Stop();
            };
            timer.Start();
            try
            {
                if (modify) viewModel.ModifyCommand.Execute(null);
                else viewModel.AddCommand.Execute(null);
                Assert.True(seen);
                Assert.Null(editError);
                Assert.Equal(modify || !confirm ? 1 : 2, model.Recipes.Count);
                Assert.Equal(modify && confirm ? "modified" : "1", model.Recipes[0].ID);
                if (!confirm)
                {
                    Assert.Same(existing, viewModel.CurRecipe);
                    Assert.Same(existing, model.CurRecipe);
                    Assert.Same(existing, grid.SelectedItem);
                    Assert.Equal(4, existing.Num_Desiccant);
                    Assert.Equal(7, Assert.Single(existing.LabelInfoCollection.LabelInfo_First).ID);
                }
                else
                {
                    var committed = model.Recipes[modify ? 0 : 1];
                    Assert.Same(committed, viewModel.CurRecipe);
                    Assert.Same(committed, model.CurRecipe);
                    Assert.Same(committed, grid.SelectedItem);
                    Assert.Equal(99, committed.Num_Desiccant);
                    Assert.Equal(42, Assert.Single(committed.LabelInfoCollection.LabelInfo_First).ID);
                    if (modify) Assert.NotSame(existing, committed);
                }
                Assert.Equal("[]", File.ReadAllText(paths[1]));
            }
            finally { timer.Stop(); view.DataContext = null; model.Recipes = original; model.CurRecipe = oldRecipe; }
        });
        }
        finally
        {
            foreach (var (path, bytes) in files)
                if (bytes != null) File.WriteAllBytes(path, bytes);
                else if (File.Exists(path)) File.Delete(path);
        }
    }

    private AutoProcess CreateProcess(Log log, IProductionPlc plc, ISecsGemRuntime? secs = null)
    {
        AutoProcess? result = null;
        fixture.Invoke(container =>
        {
            result = (AutoProcess)typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(container.Resolve<AutoProcess>(), null)!;
            Set("_logInfoService", (Func<IProductionLog>)(() => log));
            Set("_PLCMethod", plc);
            if (secs != null) Set("_secsRuntime", secs);
            void Set(string name, object value) => typeof(AutoProcess).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(result, value);
        });
        return result!;
    }

    private Task RefreshAsync(AutoProcess process, DateTime now)
    {
        Task? result = null;
        fixture.Invoke(_ =>
        {
            var method = typeof(AutoProcess).GetMethod("UpdateAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            result = Assert.IsAssignableFrom<Task>(method.Invoke(process, [now]));
        });
        return result!;
    }

    private static T Proxy<T>(Func<string, object?> invoke) where T : class
    {
        var result = DispatchProxy.Create<T, PortProxy>();
        ((PortProxy)(object)result).Handler = invoke;
        return result;
    }

    public class PortProxy : DispatchProxy
    {
        public Func<string, object?> Handler { get; set; } = _ => throw new NotSupportedException();
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!.Name);
    }

    private sealed class Log : IProductionLog
    {
        public List<string> Messages { get; } = [];
        public void AddRunInfo(string info, ELogLevel level = ELogLevel.Information, int retainNum = 1000) => Messages.Add(info);
    }
}
