// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：S6PresentationBoundaryTests、Item、Synchronization。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections、System.Collections.Specialized、System.Reflection、System.Runtime.Versioning、Vision.Core、Vision.Core.Geometry。导入不代表所有类型都会在运行时加载。
using System.Collections;
using System.Collections.Specialized;
using System.Reflection;
using System.Runtime.Versioning;
using Vision.Core;
using Vision.Core.Geometry;
using Vision.Core.Presentation;
using Vision.Core.Vision;
using Xunit;

namespace Vision.Tests.Architecture;

/// <summary>
/// S6PresentationBoundaryTests 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class S6PresentationBoundaryTests
{
    /// <summary>
    /// 测试相关入口 CoreAssemblyHasNoDesktopDependencies；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、GetCustomAttribute、DoesNotContain、GetReferencedAssemblies、Null；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CoreAssemblyHasNoDesktopDependencies()
    {
        var assembly = typeof(Point2D).Assembly;
        Assert.Equal(".NETCoreApp,Version=v9.0", assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a =>
            a.Name is "WindowsBase" or "PresentationFramework" or "PresentationCore" or "System.Xaml");
        Assert.Null(assembly.GetType("Vision.Core.DoubleArray2DToStringConverter"));
    }

    /// <summary>
    /// 测试相关入口 LegacyGeometryContractsUseCoreCoordinates；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ToArray、SelectMany、GetProperties、Equal、Count；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LegacyGeometryContractsUseCoreCoordinates()
    {
        Type[] types = [typeof(I_ROI_RectangleData), typeof(I_ROI_Rectangle2Data),
            typeof(I_ROI_PolylineData), typeof(I_XLD_LineData), typeof(I_XLD_SectorCircleData)];
        var properties = types.SelectMany(t => t.GetProperties()).ToArray();
        Assert.Equal(8, properties.Count(p => p.PropertyType == typeof(Point2D)));
        Assert.Single(properties.Where(p => p.PropertyType == typeof(List<Point2D>)));
        Assert.DoesNotContain(properties, p => p.PropertyType.Assembly.GetName().Name == "WindowsBase");
    }

    /// <summary>
    /// 测试相关入口 CollectionRetainsBatchNotificationsAndItemDisposal；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Add、Equal、RemoveAt、Single、Dispose；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CollectionRetainsBatchNotificationsAndItemDisposal()
    {
        using var items = new BulkObservableCollection<Item>();
        var actions = new List<NotifyCollectionChangedAction>();
        items.CollectionChanged += (_, e) => actions.Add(e.Action);
        var first = new Item();
        var second = new Item();
        items.Add(first);
        items[0] = second;
        Assert.Equal(1, first.Disposals);
        items.IsSuspend = true;
        items.Add(new Item());
        items.IsSuspend = false;
        items.RemoveAt(0);
        Assert.Equal(1, second.Disposals);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Replace,
            NotifyCollectionChangedAction.Reset, NotifyCollectionChangedAction.Remove }, actions);
        var last = Assert.Single(items);
        items.Dispose();
        Assert.Equal(1, last.Disposals);
        Assert.Empty(items);
        Assert.Equal(4, actions.Count);
    }

    /// <summary>
    /// 测试相关入口 SynchronizationAdapterUsesTheMutationLockAndCanBeReplaced；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 UseSynchronization、Equal、Same、True、IsEntered；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void SynchronizationAdapterUsesTheMutationLockAndCanBeReplaced()
    {
        var collection = new BulkObservableCollection<int>();
        var first = new Synchronization();
        var second = new Synchronization();
        collection.UseSynchronization(first);
        collection.UseSynchronization(first);
        Assert.Equal(1, first.Enables);
        Assert.Same(collection, first.Collection);
        collection.CollectionChanged += (_, _) => Assert.True(Monitor.IsEntered(second.Gate!));
        collection.UseSynchronization(second);
        Assert.Equal(1, first.Disables);
        Assert.Same(first.Gate, second.Gate);
        collection.Add(3);
        collection.Dispose();
        Assert.Equal(1, second.Disables);
    }

    /// <summary>
    /// Item 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IDisposable。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class Item : IDisposable
    {
        public int Disposals;
        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void Dispose() => Disposals++;
    }

    /// <summary>
    /// Synchronization 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ICollectionSynchronization。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class Synchronization : ICollectionSynchronization
    {
        public int Enables, Disables;
        public IEnumerable? Collection;
        public object? Gate;
        /// <summary>
        /// 测试相关入口 Enable；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void Enable(IEnumerable collection, object synchronizationLock)
        { Enables++; Collection = collection; Gate = synchronizationLock; }
        /// <summary>
        /// 测试相关入口 Disable；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void Disable(IEnumerable collection) => Disables++;
    }
}
