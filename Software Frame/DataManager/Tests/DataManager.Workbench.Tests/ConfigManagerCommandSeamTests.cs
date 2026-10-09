// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ConfigManagerCommandSeamTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：CommunityToolkit.Mvvm.Input、DataManager.Controls.Configuration、DataManager.Controls.ViewModels、DataManager.Controls.Views、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Workbench.Models、WPFCustomControls.Controls。导入不代表所有类型都会在运行时加载。
using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using DataManager.Controls.Configuration;
using DataManager.Controls.Services;
using DataManager.Controls.ViewModels;
using DataManager.Controls.Views;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Workbench.Models;
using WPFCustomControls.Controls;

namespace DataManager.Workbench.Tests;

/// <summary>
/// T10：<see cref="ConfigManagerView"/> 的「外部 Command 兼容 seam」。
///
/// <para>
/// 背景：控件 5 个按钮（选择 / 解锁 / 增加 / 删除 / 保存）原先绑在**只读**的
/// <c>RelayCommand</c> 属性上，外部只能通过 <c>*Callback</c> 这类 <see cref="Action"/>
/// **事后**挂钩，既无法替换命令体，也无法用 <c>CanExecute</c> 控制可用性。
/// 全仓 15 个宿主因此把回调命名成 <c>ReportSaveCommand</c> / <c>SaveCallbackCommand</c>
/// 之类「像命令其实是 Action」的东西。
/// </para>
///
/// <para>
/// 改造：5 个命令改为 <see cref="ICommand"/> **依赖属性**（与本仓库既有约定一致，
/// 见 <c>QueryView</c> / <c>AddUserView</c> / <c>DeletUserView</c> / <c>LoginView</c>），
/// 未设置时 CLR getter 回退到内置 <c>RelayCommand</c> ⇒ 15 个宿主**零改动**。
/// </para>
///
/// <para>
/// 本套件锁定三件事：① 15 个既有依赖属性的名称/类型/默认值**逐项不变**（兼容面）；
/// ② 未设置 ⇒ 内置行为照旧（含「按钮真的被绑上命令」这一条，它是构造函数顺序的回归守卫）；
/// ③ 已设置 ⇒ 外部命令接管，内置行为**不再发生**。
/// </para>
/// </summary>
[Collection("WorkbenchAppBoot")]
public class ConfigManagerCommandSeamTests(WorkbenchAppFixture host)
{
    private readonly WorkbenchAppFixture _host = host;

    // ------------------------------- ① 兼容面锁定 -------------------------------

    /// <summary>
    /// 改造前就存在的 15 个依赖属性：名称、类型、默认值逐项锁定。
    /// 这是 T03 §3「兼容面」在控件层的落点 —— 任何一项漂移都必须是有意为之并配迁移。
    /// </summary>
    [Fact]
    public void ExistingDependencyProperties_KeepNameTypeAndDefault()
    {
        var expected = new (string Name, Type Type, object? Default)[]
        {
            ("IsReadOnly", typeof(bool), true),
            ("ItemsSource", typeof(IEnumerable), null),
            ("SelectItem", typeof(object), null),
            ("FilePath", typeof(string), null),
            ("FileName", typeof(string), null),
            ("Title", typeof(string), null),
            ("SelectVisibility", typeof(Visibility), Visibility.Visible),
            ("AddVisibility", typeof(Visibility), Visibility.Visible),
            ("DeleteVisibility", typeof(Visibility), Visibility.Visible),
            ("SelectedCallback", typeof(Action), null),
            ("AddCallback", typeof(Action), null),
            ("DeletedCallback", typeof(Action), null),
            ("SaveCallback", typeof(Action), null),
            ("RowHeight", typeof(double), double.NaN),
            ("DataGridColumns", typeof(ObservableCollection<DataGridColumn>), null),
        };

        var actual = DeclaredDependencyProperties();

        foreach (var (name, type, expectedDefault) in expected)
        {
            Assert.True(actual.TryGetValue(name, out var dp), $"依赖属性 {name} 不存在（兼容面被破坏）");
            Assert.Equal(type, dp!.PropertyType);
            var metadata = dp.GetMetadata(typeof(ConfigManagerView));
            Assert.Equal(expectedDefault, metadata.DefaultValue);
        }
    }

    /// <summary>
    /// 新增的 5 个命令依赖属性：类型 <see cref="ICommand"/>、默认 <c>null</c>。
    /// 默认必须是 <c>null</c> —— 那正是「宿主没设置 ⇒ 走内置」的判据。
    /// </summary>
    [Fact]
    public void CommandDependencyProperties_AreICommandWithNullDefault()
    {
        var actual = DeclaredDependencyProperties();

        foreach (var name in new[] { "SelectCommand", "LockCommand", "SaveCommand", "AddCommand", "DeleteCommand" })
        {
            Assert.True(actual.TryGetValue(name, out var dp), $"命令依赖属性 {name} 不存在");
            Assert.Equal(typeof(ICommand), dp!.PropertyType);
            Assert.Null(dp.GetMetadata(typeof(ConfigManagerView)).DefaultValue);
        }
    }

    // ------------------------------- ② 未设置 ⇒ 内置 -------------------------------

    /// <summary>
    /// 宿主不设置任何命令时：5 个按钮必须**真的绑上了命令**，且保存按钮走的是内置实现。
    ///
    /// <para>
    /// 这一条同时是**构造函数顺序的回归守卫**：默认命令若晚于 <c>InitializeComponent()</c> 建立，
    /// 按钮加载时读到的 getter 就是 null，此后也不会重新求值 —— 按钮会静默变成禁用。
    /// </para>
    /// </summary>
    [Fact]
    public void UnsetCommands_WireButtonsToBuiltInImplementations()
    {
        var configRoot = ConfigRoot.CreateTemporary();
        Directory.CreateDirectory(configRoot.Directory);
        var fileName = "T10-BuiltIn.json";
        var expectedFile = Path.Combine(configRoot.Directory, fileName);
        try
        {
            _host.Invoke(() =>
            {
                var view = new ConfigManagerView
                {
                    FilePath = configRoot.Directory,
                    FileName = fileName,
                    ItemsSource = new ObservableCollection<SeamRow> { new() { Name = "行1" } },
                };
                var window = ShowInWindow(view);
                try
                {
                    var buttons = ButtonsOf(view);
                    Assert.Equal(5, buttons.Count);

                    // 5 个按钮都拿到了命令（顺序守卫）。
                    foreach (var (name, button) in buttons)
                    {
                        Assert.True(button.Command is not null, $"按钮「{name}」没有被绑上命令");
                    }

                    // 未设置 ⇒ getter 回退到内置 RelayCommand。
                    Assert.IsType<RelayCommand>(view.SaveCommand);
                    Assert.IsType<RelayCommand>(view.AddCommand);
                    Assert.IsType<RelayCommand>(view.DeleteCommand);
                    Assert.IsType<RelayCommand>(view.SelectCommand);
                    Assert.IsType<RelayCommand>(view.LockCommand);

                    // 按钮绑的就是 view 上的那一个实例（不是别的命令）。
                    Assert.Same(view.SaveCommand, buttons["保存"].Command);

                    // 内置行为真的还在：执行后文件落盘。
                    Assert.False(File.Exists(expectedFile));
                    view.SaveCommand.Execute(null);
                    Assert.True(File.Exists(expectedFile), "内置保存没有落盘");
                }
                finally
                {
                    window.Close();
                }
            });
        }
        finally
        {
            DeleteQuietly(configRoot.Directory);
        }
    }

    // ------------------------------- ③ 已设置 ⇒ 外部接管 -------------------------------

    /// <summary>
    /// 宿主注入自己的命令后：按钮改绑外部命令、外部命令被执行，
    /// 而**内置行为不再发生**（这里用「文件没有被写出来」验证）。
    /// </summary>
    [Fact]
    public void InjectedCommand_TakesOverAndSuppressesBuiltInBehaviour()
    {
        var configRoot = ConfigRoot.CreateTemporary();
        Directory.CreateDirectory(configRoot.Directory);
        var fileName = "T10-Injected.json";
        var expectedFile = Path.Combine(configRoot.Directory, fileName);
        try
        {
            _host.Invoke(() =>
            {
                var view = new ConfigManagerView
                {
                    FilePath = configRoot.Directory,
                    FileName = fileName,
                    ItemsSource = new ObservableCollection<SeamRow> { new() { Name = "行1" } },
                };
                var window = ShowInWindow(view);
                try
                {
                    var external = new RecordingCommand();

                    // 绑定是 OneWay（DP → 按钮），设置后按钮应当改绑。
                    view.SaveCommand = external;
                    Assert.Same(external, view.SaveCommand);
                    Assert.Same(external, ButtonsOf(view)["保存"].Command);

                    view.SaveCommand.Execute(null);
                    Assert.Equal(1, external.Executions);
                    Assert.False(File.Exists(expectedFile), "外部命令接管后，内置落盘不应再发生");

                    // 覆盖只影响被覆盖的那一个，其余仍走内置。
                    Assert.IsType<RelayCommand>(view.AddCommand);

                    // 依赖属性被清空 ⇒ 没有命令（按钮禁用）。
                    // 这是**有意**的：null 只有一种含义，显式设 null 与 ClearValue 等价；
                    // 若哪天有人加了「null 折回内置」的 coerce，这两条路就会不一致。
                    view.ClearValue(ConfigManagerView.SaveCommandProperty);
                    Assert.Null(view.SaveCommand);
                    Assert.Null(ButtonsOf(view)["保存"].Command);
                }
                finally
                {
                    window.Close();
                }
            });
        }
        finally
        {
            DeleteQuietly(configRoot.Directory);
        }
    }

    // --------------------------- ④ 真实宿主零改动（兼容回归） ---------------------------

    /// <summary>
    /// 真实宿主 <see cref="SqlView"/> 里的 <see cref="ConfigManagerView"/> 没有被改造波及：
    /// 它没设置任何命令 ⇒ 5 个按钮全部走内置实现。
    /// （15 个宿主用的都是这一套接线，此处取一个代表性宿主做回归。）
    /// </summary>
    [Fact]
    public void RealHost_WithoutSettingCommands_UsesBuiltIns()
    {
        var configRoot = ConfigRoot.CreateTemporary();
        Directory.CreateDirectory(configRoot.Directory);
        try
        {
            _host.Invoke(() =>
            {
                var vm = new SqlViewModel<TestEntity>(new SqlWorkspace<TestEntity>(new SQLManager(), configRoot));
                var page = new SqlView { DataContext = vm };
                var window = new Window
                {
                    Content = page,
                    Width = 1400,
                    Height = 900,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);

                    var manager = Descendants(page).OfType<ConfigManagerView>().Single();
                    Assert.IsType<RelayCommand>(manager.SaveCommand);
                    Assert.IsType<RelayCommand>(manager.AddCommand);
                    Assert.IsType<RelayCommand>(manager.DeleteCommand);
                    Assert.IsType<RelayCommand>(manager.SelectCommand);
                    Assert.IsType<RelayCommand>(manager.LockCommand);

                    // 宿主绑的 *Callback 仍在（改造没有动回调面）。
                    Assert.NotNull(manager.SaveCallback);
                    Assert.NotNull(manager.AddCallback);
                    Assert.NotNull(manager.DeletedCallback);
                }
                finally
                {
                    window.Close();
                }
            });
        }
        finally
        {
            DeleteQuietly(configRoot.Directory);
        }
    }

    // ------------------------------- ⑤ 模型契约面 -------------------------------

    /// <summary>
    /// <see cref="ConfigManagerModel{T}"/> 是宿主 VM 与控件之间的契约面
    /// （T03 §6 把「<c>ConfigManagerView</c> 现有 DP 清单」列为 T10 的关键触点，
    /// 而这个模型就是那些 DP 绑定的另一端）。
    ///
    /// <para>
    /// 本次**没有改它的代码**；这里把可观察语义锁住，免得后续任务
    /// （T12「配置持久化与 Apply 归还拥有者」）顺手改动时无人察觉。
    /// </para>
    /// </summary>
    [Fact]
    public void ConfigManagerModel_KeepsObservableContract()
    {
        var model = new ConfigManagerModel<SeamRow>();
        var changed = new List<string>();
        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");

        // 默认：集合非 null 且为空；三个字符串为 null。
        Assert.NotNull(model.DataCollection);
        Assert.Empty(model.DataCollection);
        Assert.Null(model.FilePath);
        Assert.Null(model.FileName);
        Assert.Null(model.Name);

        // FullPath：任一段缺失 ⇒ null（调用方判空，不拼半截路径）。
        Assert.Null(model.FullPath);
        model.FilePath = @"C:\cfg";
        Assert.Null(model.FullPath);
        model.FileName = "a.json";
        Assert.Equal(Path.Combine(@"C:\cfg", "a.json"), model.FullPath);

        // 三个字符串属性都是 INPC（控件靠它们联动）。
        model.Name = "档案";
        Assert.Contains(nameof(ConfigManagerModel<SeamRow>.FilePath), changed);
        Assert.Contains(nameof(ConfigManagerModel<SeamRow>.FileName), changed);
        Assert.Contains(nameof(ConfigManagerModel<SeamRow>.Name), changed);

        // 记录现状（不是断言）：FullPath 是计算属性、不发通知 ——
        // 宿主目前都分别绑 FilePath / FileName，所以还没被咬到。
        Assert.DoesNotContain(nameof(ConfigManagerModel<SeamRow>.FullPath), changed);
    }

    // ------------------------------- 工具 -------------------------------

    /// <summary>本类型自己声明的依赖属性（不含 UserControl 基类的）。</summary>
    private static Dictionary<string, DependencyProperty> DeclaredDependencyProperties()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
        return typeof(ConfigManagerView).GetFields(flags)
            .Where(field => field.FieldType == typeof(DependencyProperty))
            .Select(field => (DependencyProperty)field.GetValue(null)!)
            .ToDictionary(dp => dp.Name, dp => dp, StringComparer.Ordinal);
    }

    /// <summary>把控件放进窗口并完成一次布局（按钮的 Command 绑定需要可视树成型才可靠）。</summary>
    private static Window ShowInWindow(FrameworkElement view)
    {
        var window = new Window
        {
            Content = view,
            Width = 900,
            Height = 600,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        return window;
    }

    /// <summary>按 <c>AutomationProperties.Name</c> 收集 5 个按钮。</summary>
    private static Dictionary<string, Button> ButtonsOf(DependencyObject root) =>
        Descendants(root).OfType<Button>()
            .Where(button => !string.IsNullOrEmpty(AutomationProperties.GetName(button)))
            .ToDictionary(button => AutomationProperties.GetName(button), button => button, StringComparer.Ordinal);

    /// <summary>深度优先遍历可视树（含模板展开的子孙）。</summary>
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static void DeleteQuietly(string directory)
    {
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>最小可序列化行（测试用）。</summary>
    public sealed class SeamRow
    {
        public string? Name { get; set; }
    }

    /// <summary>只数执行次数的桩命令，用来证明「外部命令接管了」。</summary>
    private sealed class RecordingCommand : ICommand
    {
        public int Executions { get; private set; }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Executions++;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }
    }
}
