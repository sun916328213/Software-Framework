// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ViewModelSubscriptionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls.Configuration、DataManager.Controls.Services、DataManager.Controls.ViewModels、DataManager.Core.Excel.Definition、DataManager.Core.SQL.Definition、DataManager.Runtime.Excel、DataManager.Runtime.SQL、DataManager.Runtime.SQL.Definition、DataManager.Workbench.Models、System.Reflection。导入不代表所有类型都会在运行时加载。
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using DataManager.Controls.Configuration;
using DataManager.Controls.Services;
using DataManager.Controls.ViewModels;
using DataManager.Core.Excel.Definition;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.Excel;
using DataManager.Runtime.SQL;
using DataManager.Runtime.SQL.Definition;
using DataManager.Workbench.Models;

namespace DataManager.Workbench.Tests;

/// <summary>
/// T09：SQL / Excel 页面 VM 的订阅寿命。
///
/// <para>
/// 背景：<c>SqlWorkspace&lt;T&gt;</c> / <c>ExcelWorkspace&lt;T&gt;</c> 在模块里注册为**单例**，
/// 因此页面 Model 比页面 VM 活得久；而两个 VM 原来都用**匿名 lambda** 订阅 Model 的
/// <c>PropertyChanged</c>（Excel 还多两处：档案集合 <c>CollectionChanged</c>、当前行 <c>PropertyChanged</c>），
/// 且**没有任何退订入口**。页面退场时 <c>WorkbenchLifetime.CloseRegionAsync</c> 会执行
/// <c>(viewModel as IDisposable)?.Dispose()</c> —— 那时它是个静默空操作。
/// </para>
///
/// <para>
/// 本套件锁定三件事：① 订阅确实挂在共享 Model 上；② <c>Dispose()</c> 确实摘干净、幂等、
/// 且不会误摘别人的订阅；③ **退订 ≠ 释放共享 Workspace**（T03 §2「共享实例不释放」）。
/// </para>
/// </summary>
public class ViewModelSubscriptionTests
{
    /// <summary>字段式事件的后备字段与事件同名。</summary>
    private const string PropertyChangedEvent = "PropertyChanged";
    private const string CollectionChangedEvent = "CollectionChanged";

    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic
        | BindingFlags.Public | BindingFlags.DeclaredOnly;

    /// <summary>
    /// 数某对象上某事件的订阅者个数。走编译器生成的**后备字段**，沿继承链逐层找；
    /// 名字对不上时按委托类型兜底（<c>ObservableCollection&lt;T&gt;</c> 的事件声明方式随版本有差异）。
    /// </summary>
    private static int SubscriberCount(object source, string eventName, Type? handlerType = null)
    {
        for (var type = source.GetType(); type is not null; type = type.BaseType)
        {
            var field = type.GetField(eventName, Fields)
                ?? (handlerType is null ? null : type.GetFields(Fields).FirstOrDefault(f => f.FieldType == handlerType));
            if (field is not null)
                return (field.GetValue(source) as Delegate)?.GetInvocationList().Length ?? 0;
        }
        throw new InvalidOperationException($"未找到事件 {eventName} 的后备字段（{source.GetType().Name}）");
    }

    private static SqlWorkspace<TestEntity> NewSqlWorkspace() =>
        new(new SQLManager(), ConfigRoot.CreateTemporary());

    private static ExcelWorkspace<ExcelPerson> NewExcelWorkspace() =>
        new(new ExcelManager(), ConfigRoot.CreateTemporary());

    // ------------------------------- SQL 页面 VM -------------------------------

    /// <summary>订阅挂在共享 Model 上；<c>Dispose()</c> 之后订阅数回到基线。</summary>
    [Fact]
    public void SqlViewModel_SubscribesSharedModel_AndDisposeUnsubscribes()
    {
        var workspace = NewSqlWorkspace();
        var model = workspace.Model;
        var baseline = SubscriberCount(model, PropertyChangedEvent);

        var vm = new SqlViewModel<TestEntity>(workspace);
        Assert.Equal(baseline + 1, SubscriberCount(model, PropertyChangedEvent));

        vm.Dispose();
        Assert.Equal(baseline, SubscriberCount(model, PropertyChangedEvent));

        workspace.Dispose();
    }

    /// <summary>幂等：重复 <c>Dispose()</c> 不得把**别人的**订阅一起摘掉。</summary>
    [Fact]
    public void SqlViewModel_DisposeTwice_KeepsOtherSubscribers()
    {
        var workspace = NewSqlWorkspace();
        var model = workspace.Model;
        var raised = 0;
        PropertyChangedEventHandler probe = (_, _) => raised++;
        model.PropertyChanged += probe;
        var baseline = SubscriberCount(model, PropertyChangedEvent);

        var vm = new SqlViewModel<TestEntity>(workspace);
        vm.Dispose();
        vm.Dispose();

        Assert.Equal(baseline, SubscriberCount(model, PropertyChangedEvent));
        model.SelectedEntity = new TestEntity();
        Assert.Equal(1, raised);

        workspace.Dispose();
    }

    /// <summary>行为面：退订后 Model 的变化不再打到已退场的 VM（命令不再被刷新）。</summary>
    [Fact]
    public void SqlViewModel_Dispose_StopsRefreshingCommands()
    {
        var workspace = NewSqlWorkspace();
        var vm = new SqlViewModel<TestEntity>(workspace);
        var raised = 0;
        vm.UpdateSelectedCommand.CanExecuteChanged += (_, _) => raised++;

        workspace.Model.SelectedEntity = new TestEntity();
        Assert.Equal(1, raised);

        vm.Dispose();
        workspace.Model.SelectedEntity = new TestEntity();
        Assert.Equal(1, raised);

        workspace.Dispose();
    }

    /// <summary>
    /// T03 §2「共享实例不释放」：VM 退场只退自己的订阅，
    /// 不得释放/停用宿主共享的 Workspace（页面重建时那样会误伤新页面）。
    /// </summary>
    [Fact]
    public void SqlViewModel_Dispose_DoesNotReleaseSharedWorkspace()
    {
        var workspace = NewSqlWorkspace();
        var vm = new SqlViewModel<TestEntity>(workspace);
        vm.Dispose();

        // 未释放的第一道判据：签发的 Token 仍是「活的」。
        Assert.False(workspace.BeginOperation().IsCancellationRequested);

        // 第二道：仍能按指纹建实例（已释放的 Workspace 会抛 ObjectDisposedException）。
        var profile = new SQLParam
        {
            Name = "after-vm-dispose",
            DBType = ESQLType.SQLite,
            ToolType = ESQLToolType.EFCore,
            ConnectionString = "Data Source=:memory:;",
        };
        workspace.Model.DataCollection.Clear();
        workspace.Model.DataCollection.Add(profile);
        Assert.NotNull(workspace.EnsureDb(profile));

        workspace.Dispose();
    }

    // ------------------------------ Excel 页面 VM ------------------------------

    /// <summary>三处订阅（Model 属性 / 档案集合 / 当前行）全部挂上，<c>Dispose()</c> 后全部摘除。</summary>
    [Fact]
    public void ExcelViewModel_SubscribesModelAndRow_AndDisposeUnsubscribesAll()
    {
        var workspace = NewExcelWorkspace();
        var model = workspace.Model;
        var row = model.SelectedExcelParam!;
        var modelBaseline = SubscriberCount(model, PropertyChangedEvent);
        var profilesBaseline = SubscriberCount(model.DataCollection, CollectionChangedEvent,
            typeof(NotifyCollectionChangedEventHandler));
        var rowBaseline = SubscriberCount(row, PropertyChangedEvent);

        var vm = new ExcelViewModel<ExcelPerson>(workspace);
        Assert.Equal(modelBaseline + 1, SubscriberCount(model, PropertyChangedEvent));
        Assert.Equal(profilesBaseline + 1, SubscriberCount(model.DataCollection, CollectionChangedEvent,
            typeof(NotifyCollectionChangedEventHandler)));
        Assert.Equal(rowBaseline + 1, SubscriberCount(row, PropertyChangedEvent));

        vm.Dispose();
        Assert.Equal(modelBaseline, SubscriberCount(model, PropertyChangedEvent));
        Assert.Equal(profilesBaseline, SubscriberCount(model.DataCollection, CollectionChangedEvent,
            typeof(NotifyCollectionChangedEventHandler)));
        Assert.Equal(rowBaseline, SubscriberCount(row, PropertyChangedEvent));
    }

    /// <summary>行订阅退订：切换档案时旧行的订阅必须摘掉、只保留新行一处。</summary>
    [Fact]
    public void ExcelViewModel_RowSwitch_MovesRowSubscriptionToNewRow()
    {
        var workspace = NewExcelWorkspace();
        var model = workspace.Model;
        var first = model.SelectedExcelParam!;
        var second = new ExcelParam { Name = "second", FilePath = @"C:\excel2", FileName = "b.xlsx" };
        var firstBaseline = SubscriberCount(first, PropertyChangedEvent);
        var secondBaseline = SubscriberCount(second, PropertyChangedEvent);

        var vm = new ExcelViewModel<ExcelPerson>(workspace);
        Assert.Equal(firstBaseline + 1, SubscriberCount(first, PropertyChangedEvent));
        Assert.Equal(secondBaseline, SubscriberCount(second, PropertyChangedEvent));

        model.SelectedExcelParam = second;

        Assert.Equal(firstBaseline, SubscriberCount(first, PropertyChangedEvent));
        Assert.Equal(secondBaseline + 1, SubscriberCount(second, PropertyChangedEvent));

        vm.Dispose();
        Assert.Equal(secondBaseline, SubscriberCount(second, PropertyChangedEvent));
    }

    /// <summary>Excel 的退订同样是幂等的。</summary>
    [Fact]
    public void ExcelViewModel_DisposeTwice_KeepsOtherSubscribers()
    {
        var workspace = NewExcelWorkspace();
        var model = workspace.Model;
        var row = model.SelectedExcelParam!;
        var raised = 0;
        PropertyChangedEventHandler probe = (_, _) => raised++;
        row.PropertyChanged += probe;
        var rowBaseline = SubscriberCount(row, PropertyChangedEvent);

        var vm = new ExcelViewModel<ExcelPerson>(workspace);
        vm.Dispose();
        vm.Dispose();

        Assert.Equal(rowBaseline, SubscriberCount(row, PropertyChangedEvent));
        row.ToolType = ExcelType.EPPlus;
        Assert.Equal(1, raised);
    }
}
