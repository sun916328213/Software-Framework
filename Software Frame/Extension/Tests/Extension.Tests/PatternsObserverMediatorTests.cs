// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：PatternsObserverMediatorTests、CountingObserver、ThrowingObserver、ProbeObservable、TypedObserver、ProbeColleague、ProbeAdvancedObserver。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.IO、System.Linq、System.Runtime.CompilerServices、System.Threading。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Extension.Patterns.Coordination.Mediator;
using Extension.Patterns.Coordination.Mediator.Advanced;
using Extension.Patterns.Messaging.Observer;
using Extension.Patterns.Messaging.Observer.Advanced;
using Xunit;

namespace Extension.Tests;

/// <summary>
/// PatternsObserverMediatorTests 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class PatternsObserverMediatorTests
{
	/// <summary>
	/// CountingObserver 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
	/// 继承或实现的契约：IObserver。具体约束按这些契约及各成员实现解释。
	/// </summary>
	private sealed class CountingObserver : IObserver
	{
		public int UpdateCount { get; private set; }

		public IObservable LastSubject { get; private set; }

		public Action<IObservable> OnUpdate { get; set; }

		/// <summary>
		/// 验证 Update 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
		/// </summary>
		public void Update(IObservable subject)
		{
			UpdateCount++;
			LastSubject = subject;
			OnUpdate?.Invoke(subject);
		}
	}

	/// <summary>
	/// ThrowingObserver 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
	/// 继承或实现的契约：IObserver。具体约束按这些契约及各成员实现解释。
	/// </summary>
	private sealed class ThrowingObserver : IObserver
	{
		/// <summary>
		/// 验证 Update 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
		/// </summary>
		public void Update(IObservable subject)
		{
			throw new InvalidOperationException("观察者炸了");
		}
	}

	/// <summary>
	/// ProbeObservable 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
	/// 继承或实现的契约：ObservableBase。具体约束按这些契约及各成员实现解释。
	/// </summary>
	private sealed class ProbeObservable : ObservableBase
	{
	}

	/// <summary>
	/// TypedObserver 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
	/// 继承或实现的契约：Observer&lt;int&gt;。具体约束按这些契约及各成员实现解释。
	/// </summary>
	private sealed class TypedObserver : Observer<int>
	{
		public List<int> Received { get; } = new List<int>();

		public IObservable LastSubject { get; private set; }

		/// <summary>
		/// 验证 HandleUpdate 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
		/// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
		/// </summary>
		public override void HandleUpdate(IObservable subject, int data)
		{
			LastSubject = subject;
			Received.Add(data);
		}
	}

	/// <summary>
	/// ProbeColleague 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
	/// 继承或实现的契约：ColleagueBase。具体约束按这些契约及各成员实现解释。
	/// </summary>
	private sealed class ProbeColleague : ColleagueBase
	{
		public List<KeyValuePair<string, object>> Received { get; } = new List<KeyValuePair<string, object>>();

		public Action<string, object> OnReceive { get; set; }

		public int DisposeCount { get; private set; }

		/// <summary>
		/// 初始化 ProbeColleague 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
		/// </summary>
		public ProbeColleague(string id)
			: base(id)
		{
		}

		/// <summary>
		/// 验证 Receive 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
		/// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
		/// </summary>
		public override void Receive(string from, object message)
		{
			Received.Add(new KeyValuePair<string, object>(from, message));
			OnReceive?.Invoke(from, message);
		}

		/// <summary>
		/// 验证 Dispose 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
		/// 正文中的关键协作调用包括 Dispose；具体条件和先后次序见方法体。
		/// </summary>
		protected override void Dispose(bool disposing)
		{
			DisposeCount++;
			base.Dispose(disposing);
		}
	}

	/// <summary>
	/// ProbeAdvancedObserver 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
	/// 继承或实现的契约：AdvancedObserverBase&lt;int&gt;。具体约束按这些契约及各成员实现解释。
	/// </summary>
	private sealed class ProbeAdvancedObserver : AdvancedObserverBase<int>
	{
		public int HandleCount { get; private set; }

		public int LastData { get; private set; }

		/// <summary>
		/// 验证 HandleEvent 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
		/// </summary>
		public override void HandleEvent(object sender, EventArgs<int> e)
		{
			HandleCount++;
			LastData = e.Data;
		}
	}

	/// <summary>
	/// 验证 ObservableBase_Attach去重_Detach_ObserverCount 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Attach、Equal、True、ContainsObserver、Detach；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ObservableBase_Attach去重_Detach_ObserverCount()
	{
		ProbeObservable probeObservable = new ProbeObservable();
		CountingObserver observer = new CountingObserver();
		probeObservable.Attach(observer);
		probeObservable.Attach(observer);
		Assert.Equal(1, probeObservable.ObserverCount);
		Assert.True(probeObservable.ContainsObserver(observer));
		probeObservable.Detach(observer);
		Assert.Equal(0, probeObservable.ObserverCount);
		Assert.False(probeObservable.ContainsObserver(observer));
	}

	/// <summary>
	/// 验证 ObservableBase_Notify把自身作为subject传给观察者 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Attach、Notify、Equal、Same；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ObservableBase_Notify把自身作为subject传给观察者()
	{
		ProbeObservable probeObservable = new ProbeObservable();
		CountingObserver countingObserver = new CountingObserver();
		probeObservable.Attach(countingObserver);
		probeObservable.Notify();
		Assert.Equal(1, countingObserver.UpdateCount);
		Assert.Same(probeObservable, countingObserver.LastSubject);
	}

	/// <summary>
	/// 验证 ObservableBase_一个观察者抛异常不影响其他观察者 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Attach、Notify、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ObservableBase_一个观察者抛异常不影响其他观察者()
	{
		ProbeObservable probeObservable = new ProbeObservable();
		CountingObserver countingObserver = new CountingObserver();
		probeObservable.Attach(new ThrowingObserver());
		probeObservable.Attach(countingObserver);
		probeObservable.Notify();
		Assert.Equal(1, countingObserver.UpdateCount);
	}

	/// <summary>
	/// 验证 ObservableBase_没有观察者时Notify是空操作 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Notify、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ObservableBase_没有观察者时Notify是空操作()
	{
		ProbeObservable probeObservable = new ProbeObservable();
		probeObservable.Notify();
		Assert.Equal(0, probeObservable.ObserverCount);
	}

	/// <summary>
	/// 验证 ObservableBase_Dispose之后Attach抛但Notify静默返回 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Attach、Dispose、Throws、Detach、ClearObservers；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ObservableBase_Dispose之后Attach抛但Notify静默返回()
	{
		ProbeObservable observable = new ProbeObservable();
		CountingObserver observer = new CountingObserver();
		observable.Attach(observer);
		observable.Dispose();
		Assert.Throws<ObjectDisposedException>(delegate
		{
			observable.Attach(observer);
		});
		Assert.Throws<ObjectDisposedException>(delegate
		{
			observable.Detach(observer);
		});
		Assert.Throws<ObjectDisposedException>(delegate
		{
			observable.ClearObservers();
		});
		Assert.Throws<ObjectDisposedException>(() => observable.ContainsObserver(observer));
		observable.Notify();
		Assert.Equal(0, observer.UpdateCount);
	}

	/// <summary>
	/// 验证 ObservableBase_Dispose会清空观察者 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Attach、Dispose、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ObservableBase_Dispose会清空观察者()
	{
		ProbeObservable probeObservable = new ProbeObservable();
		probeObservable.Attach(new CountingObserver());
		probeObservable.Dispose();
		Assert.Equal(0, probeObservable.ObserverCount);
	}

	/// <summary>
	/// 验证 ObservableBase_批量Attach与Detach 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 AttachRange、Equal、DetachRange、True、ContainsObserver；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ObservableBase_批量Attach与Detach()
	{
		ProbeObservable probeObservable = new ProbeObservable();
		CountingObserver countingObserver = new CountingObserver();
		CountingObserver countingObserver2 = new CountingObserver();
		CountingObserver countingObserver3 = new CountingObserver();
		probeObservable.AttachRange(new IObserver[5] { countingObserver, countingObserver2, countingObserver3, null, countingObserver });
		Assert.Equal(3, probeObservable.ObserverCount);
		probeObservable.DetachRange(new IObserver[3] { countingObserver, countingObserver3, null });
		Assert.Equal(1, probeObservable.ObserverCount);
		Assert.True(probeObservable.ContainsObserver(countingObserver2));
	}

	/// <summary>
	/// 验证 ObservableBase_参数校验 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Throws、Attach、Detach、AttachRange、DetachRange；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ObservableBase_参数校验()
	{
		ProbeObservable observable = new ProbeObservable();
		Assert.Throws<ArgumentNullException>(delegate
		{
			observable.Attach(null);
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			observable.Detach(null);
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			observable.AttachRange(null);
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			observable.DetachRange(null);
		});
		Assert.False(observable.ContainsObserver(null));
	}

	/// <summary>
	/// 验证 ObservableBase_NotifyAsync会真的通知 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Attach、NotifyAsync、Equal；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task ObservableBase_NotifyAsync会真的通知()
	{
		ProbeObservable observable = new ProbeObservable();
		CountingObserver observer = new CountingObserver();
		observable.Attach(observer);
		await observable.NotifyAsync();
		Assert.Equal(1, observer.UpdateCount);
	}

	/// <summary>
	/// 验证 Observable_默认开启相等性检查_同值不通知 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Attach、True、UpdateData、False、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Observable_默认开启相等性检查_同值不通知()
	{
		Observable<int> observable = new Observable<int>();
		CountingObserver countingObserver = new CountingObserver();
		observable.Attach(countingObserver);
		Assert.True(observable.UpdateData(5));
		Assert.False(observable.UpdateData(5));
		Assert.Equal(1, countingObserver.UpdateCount);
	}

	/// <summary>
	/// 验证 Observable_关闭相等性检查_每次赋值都通知 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Attach、UpdateData、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Observable_关闭相等性检查_每次赋值都通知()
	{
		Observable<int> observable = new Observable<int>(enableEqualityCheck: false);
		CountingObserver countingObserver = new CountingObserver();
		observable.Attach(countingObserver);
		observable.UpdateData(5);
		observable.UpdateData(5);
		Assert.Equal(2, countingObserver.UpdateCount);
	}

	/// <summary>
	/// 验证 Observable_null与非null互换都会通知 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Attach、True、UpdateData、False、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Observable_null与非null互换都会通知()
	{
		Observable<string> observable = new Observable<string>();
		CountingObserver countingObserver = new CountingObserver();
		observable.Attach(countingObserver);
		Assert.True(observable.UpdateData("a"));
		Assert.True(observable.UpdateData(null));
		Assert.False(observable.UpdateData(null));
		Assert.Equal(2, countingObserver.UpdateCount);
	}

	/// <summary>
	/// 验证 Observable_Data属性读写与UpdateData等价 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Attach、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Observable_Data属性读写与UpdateData等价()
	{
		Observable<int> observable = new Observable<int>();
		CountingObserver countingObserver = new CountingObserver();
		observable.Attach(countingObserver);
		observable.Data = 9;
		Assert.Equal(9, observable.Data);
		Assert.Equal(1, countingObserver.UpdateCount);
	}

	/// <summary>
	/// 验证 Observable_UpdateDataAsync同值返回false 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Attach、True、UpdateDataAsync、False、Equal；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task Observable_UpdateDataAsync同值返回false()
	{
		Observable<int> observable = new Observable<int>();
		observable.Attach(new CountingObserver());
		Assert.True(await observable.UpdateDataAsync(3));
		Assert.False(await observable.UpdateDataAsync(3));
		Assert.Equal(3, observable.Data);
	}

	/// <summary>
	/// 验证 Observable_引用类型按Equals比较 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Attach、UpdateData、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Observable_引用类型按Equals比较()
	{
		Observable<string> observable = new Observable<string>();
		CountingObserver countingObserver = new CountingObserver();
		observable.Attach(countingObserver);
		observable.UpdateData(new string(new char[2] { 'a', 'b' }));
		observable.UpdateData(new string(new char[2] { 'a', 'b' }));
		Assert.Equal(1, countingObserver.UpdateCount);
	}

	/// <summary>
	/// 验证 Observer_Update用lock_observable_但Data用内部锁_锁的不是同一对象 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Run、Set、Wait、FromSeconds、True；具体条件和先后次序见方法体。
	/// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
	/// </summary>
	[Fact]
	public void Observer_Update用lock_observable_但Data用内部锁_锁的不是同一对象()
	{
		Observable<int> observable = new Observable<int>(enableEqualityCheck: false);
		ManualResetEventSlim entered = new ManualResetEventSlim(initialState: false);
		try
		{
			ManualResetEventSlim release = new ManualResetEventSlim(initialState: false);
			try
			{
				Task task = Task.Run(delegate
				{
					lock (observable)
					{
						entered.Set();
						release.Wait(TimeSpan.FromSeconds(10L));
					}
				});
				Assert.True(entered.Wait(TimeSpan.FromSeconds(10L)), "没能拿到 observable 的监视器锁");
				Task<int> task2 = Task.Run(() => observable.Data = 42);
				bool condition = task2.Wait(TimeSpan.FromSeconds(5L));
				release.Set();
				task.Wait(TimeSpan.FromSeconds(10L));
				Assert.True(condition, "lock(observable) 挡住了 Data 的 setter ⇒ 说明锁的其实是同一个对象");
				Assert.Equal(42, observable.Data);
			}
			finally
			{
				if (release != null)
				{
					((IDisposable)release).Dispose();
				}
			}
		}
		finally
		{
			if (entered != null)
			{
				((IDisposable)entered).Dispose();
			}
		}
	}

	/// <summary>
	/// 验证 Observer_Update拿的是实例锁_持锁期间挂了观察者的赋值会被卡住 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Attach、Run、Set、Wait、FromSeconds；具体条件和先后次序见方法体。
	/// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
	/// </summary>
	[Fact]
	public void Observer_Update拿的是实例锁_持锁期间挂了观察者的赋值会被卡住()
	{
		Observable<int> observable = new Observable<int>(enableEqualityCheck: false);
		observable.Attach(new TypedObserver());
		ManualResetEventSlim entered = new ManualResetEventSlim(initialState: false);
		try
		{
			ManualResetEventSlim release = new ManualResetEventSlim(initialState: false);
			try
			{
				Task task = Task.Run(delegate
				{
					lock (observable)
					{
						entered.Set();
						release.Wait(TimeSpan.FromSeconds(10L));
					}
				});
				Assert.True(entered.Wait(TimeSpan.FromSeconds(10L)));
				Task<int> task2 = Task.Run(() => observable.Data = 42);
				bool condition = task2.Wait(TimeSpan.FromMilliseconds(500L, 0L));
				release.Set();
				task.Wait(TimeSpan.FromSeconds(10L));
				task2.Wait(TimeSpan.FromSeconds(5L));
				Assert.False(condition, "持锁期间赋值完成了 ⇒ 说明 Observer<T>.Update 锁的不是这个实例");
			}
			finally
			{
				if (release != null)
				{
					((IDisposable)release).Dispose();
				}
			}
		}
		finally
		{
			if (entered != null)
			{
				((IDisposable)entered).Dispose();
			}
		}
	}

	/// <summary>
	/// 验证 Observer_Update会从被观察对象取到最新数据 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Attach、UpdateData、Equal、Same；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Observer_Update会从被观察对象取到最新数据()
	{
		Observable<int> observable = new Observable<int>();
		TypedObserver typedObserver = new TypedObserver();
		observable.Attach(typedObserver);
		observable.UpdateData(11);
		observable.UpdateData(22);
		Assert.Equal(new int[2] { 11, 22 }, typedObserver.Received);
		Assert.Same(observable, typedObserver.LastSubject);
	}

	/// <summary>
	/// 验证 Observer_Update对null的subject抛ArgumentNullException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Throws、Update；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Observer_Update对null的subject抛ArgumentNullException()
	{
		TypedObserver observer = new TypedObserver();
		Assert.Throws<ArgumentNullException>(delegate
		{
			observer.Update(null);
		});
	}

	/// <summary>
	/// 验证 Observer_Update对不匹配的subject类型只写控制台_不抛也不回调 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Update、Empty、Null；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Observer_Update对不匹配的subject类型只写控制台_不抛也不回调()
	{
		TypedObserver typedObserver = new TypedObserver();
		ProbeObservable subject = new ProbeObservable();
		typedObserver.Update(subject);
		Assert.Empty(typedObserver.Received);
		Assert.Null(typedObserver.LastSubject);
	}

	/// <summary>
	/// 验证 AdvancedObservable_Subscribe去重_SubscriberCount 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Subscribe、Equal、True、ContainsSubscriber、Unsubscribe；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AdvancedObservable_Subscribe去重_SubscriberCount()
	{
		AdvancedObservable<int> advancedObservable = new AdvancedObservable<int>();
		DelegateObserver<int> observer = new DelegateObserver<int>("a", delegate
		{
		});
		advancedObservable.Subscribe(observer);
		advancedObservable.Subscribe(observer);
		Assert.Equal(1, advancedObservable.SubscriberCount);
		Assert.True(advancedObservable.ContainsSubscriber(observer));
		advancedObservable.Unsubscribe(observer);
		Assert.Equal(0, advancedObservable.SubscriberCount);
	}

	/// <summary>
	/// 验证 AdvancedObservable_Publish通知所有订阅者并带上数据 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Subscribe、Add、Publish、Equal、ToArray；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AdvancedObservable_Publish通知所有订阅者并带上数据()
	{
		AdvancedObservable<int> advancedObservable = new AdvancedObservable<int>();
		List<int> seen = new List<int>();
		advancedObservable.Subscribe(new DelegateObserver<int>("a", delegate(object s, EventArgs<int> e)
		{
			seen.Add(e.Data);
		}));
		advancedObservable.Subscribe(new DelegateObserver<int>("b", delegate(object s, EventArgs<int> e)
		{
			seen.Add(e.Data * 10);
		}));
		advancedObservable.Publish(3);
		Assert.Equal(new int[2] { 3, 30 }, seen.OrderBy((int x) => x).ToArray());
	}

	/// <summary>
	/// 验证 AdvancedObservable_一个订阅者抛异常不影响其他订阅者 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Subscribe、Publish、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AdvancedObservable_一个订阅者抛异常不影响其他订阅者()
	{
		AdvancedObservable<int> advancedObservable = new AdvancedObservable<int>();
		int goodCount = 0;
		advancedObservable.Subscribe(new DelegateObserver<int>("bad", delegate
		{
			throw new InvalidOperationException("炸");
		}));
		advancedObservable.Subscribe(new DelegateObserver<int>("good", delegate
		{
			goodCount++;
		}));
		advancedObservable.Publish(1);
		Assert.Equal(1, goodCount);
	}

	/// <summary>
	/// 验证 AdvancedObservable_所有订阅者共享同一个EventArgs实例 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Subscribe、Publish、NotNull、Same、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AdvancedObservable_所有订阅者共享同一个EventArgs实例()
	{
		AdvancedObservable<int> advancedObservable = new AdvancedObservable<int>();
		EventArgs<int> first = null;
		EventArgs<int> second = null;
		advancedObservable.Subscribe(new DelegateObserver<int>("a", delegate(object s, EventArgs<int> e)
		{
			first = e;
		}));
		advancedObservable.Subscribe(new DelegateObserver<int>("b", delegate(object s, EventArgs<int> e)
		{
			second = e;
		}));
		advancedObservable.Publish(7);
		Assert.NotNull(first);
		Assert.Same(first, second);
		Assert.Equal(7, first.Data);
	}

	/// <summary>
	/// 验证 AdvancedObservable_Dispose之后Subscribe抛但Publish静默返回 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Subscribe、Dispose、Throws、Unsubscribe、Publish；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AdvancedObservable_Dispose之后Subscribe抛但Publish静默返回()
	{
		AdvancedObservable<int> observable = new AdvancedObservable<int>();
		int count = 0;
		observable.Subscribe(new DelegateObserver<int>("a", delegate
		{
			count++;
		}));
		observable.Dispose();
		Assert.Throws<ObjectDisposedException>(delegate
		{
			observable.Subscribe(new DelegateObserver<int>("b", delegate
			{
			}));
		});
		Assert.Throws<ObjectDisposedException>(delegate
		{
			observable.Unsubscribe(new DelegateObserver<int>("b", delegate
			{
			}));
		});
		observable.Publish(1);
		Assert.Equal(0, count);
	}

	/// <summary>
	/// 验证 AdvancedObservable_批量订阅与参数校验 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 SubscribeRange、Equal、UnsubscribeRange、Throws、Subscribe；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AdvancedObservable_批量订阅与参数校验()
	{
		AdvancedObservable<int> observable = new AdvancedObservable<int>();
		DelegateObserver<int> delegateObserver = new DelegateObserver<int>("a", delegate
		{
		});
		DelegateObserver<int> delegateObserver2 = new DelegateObserver<int>("b", delegate
		{
		});
		observable.SubscribeRange(new DelegateObserver<int>[4] { delegateObserver, delegateObserver2, null, delegateObserver });
		Assert.Equal(2, observable.SubscriberCount);
		observable.UnsubscribeRange(new DelegateObserver<int>[2] { delegateObserver, null });
		Assert.Equal(1, observable.SubscriberCount);
		Assert.Throws<ArgumentNullException>(delegate
		{
			observable.Subscribe(null);
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			observable.SubscribeRange(null);
		});
		Assert.False(observable.ContainsSubscriber(null));
	}

	/// <summary>
	/// 验证 AdvancedObservable_PublishAsync会真的通知 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Subscribe、PublishAsync、Equal；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task AdvancedObservable_PublishAsync会真的通知()
	{
		AdvancedObservable<int> observable = new AdvancedObservable<int>();
		int count = 0;
		observable.Subscribe(new DelegateObserver<int>("a", delegate
		{
			count++;
		}));
		await observable.PublishAsync(1);
		Assert.Equal(1, count);
	}

	/// <summary>
	/// 验证 EventArgsBase_EventTime在构造时定格 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 InRange；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void EventArgsBase_EventTime在构造时定格()
	{
		DateTime now = DateTime.Now;
		EventArgsBase eventArgsBase = new EventArgsBase();
		DateTime now2 = DateTime.Now;
		Assert.InRange(eventArgsBase.EventTime, now, now2);
	}

	/// <summary>
	/// 验证 EventArgs_T_继承链挂到BCL的System_EventArgs上 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 True、IsAssignableFrom、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void EventArgs_T_继承链挂到BCL的System_EventArgs上()
	{
		Assert.True(typeof(EventArgs).IsAssignableFrom(typeof(EventArgsBase)));
		Assert.True(typeof(EventArgsBase).IsAssignableFrom(typeof(EventArgs<int>)));
		Assert.Equal(7, new EventArgs<int>(7).Data);
	}

	/// <summary>
	/// 验证 DelegateObserver_构造函数参数校验与事件转发 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Throws、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void DelegateObserver_构造函数参数校验与事件转发()
	{
		Assert.Throws<ArgumentNullException>(() => new DelegateObserver<int>(null, delegate
		{
		}));
		Assert.Throws<ArgumentNullException>(() => new DelegateObserver<int>("a", null));
		DelegateObserver<int> delegateObserver = new DelegateObserver<int>("a", delegate
		{
		});
		Assert.Equal("a", delegateObserver.Name);
	}

	/// <summary>
	/// 验证 AdvancedObserverBase_OnEvent转发给HandleEvent 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 OnEvent、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AdvancedObserverBase_OnEvent转发给HandleEvent()
	{
		ProbeAdvancedObserver probeAdvancedObserver = new ProbeAdvancedObserver();
		probeAdvancedObserver.OnEvent(this, new EventArgs<int>(5));
		Assert.Equal(1, probeAdvancedObserver.HandleCount);
		Assert.Equal(5, probeAdvancedObserver.LastData);
	}

	/// <summary>
	/// 验证 Mediator_Register后能收到Send的消息 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、Send、Equal、Single；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Mediator_Register后能收到Send的消息()
	{
		Mediator mediator = new Mediator();
		ProbeColleague probeColleague = new ProbeColleague("a");
		mediator.Register(probeColleague);
		mediator.Send("sender", "a", "载荷");
		Assert.Equal(1, mediator.ColleagueCount);
		Assert.Single(probeColleague.Received);
		Assert.Equal("sender", probeColleague.Received[0].Key);
		Assert.Equal("载荷", probeColleague.Received[0].Value);
	}

	/// <summary>
	/// 验证 Mediator_重复Id注册被静默忽略_但新对象的SetMediator已生效 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、Equal、Send、Single、Empty；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Mediator_重复Id注册被静默忽略_但新对象的SetMediator已生效()
	{
		Mediator mediator = new Mediator();
		ProbeColleague probeColleague = new ProbeColleague("dup");
		ProbeColleague probeColleague2 = new ProbeColleague("dup");
		mediator.Register(probeColleague);
		mediator.Register(probeColleague2);
		Assert.Equal(1, mediator.ColleagueCount);
		mediator.Send("x", "dup", "给dup");
		Assert.Single(probeColleague.Received);
		Assert.Empty(probeColleague2.Received);
		probeColleague2.Send("dup", "from-second");
		Assert.Equal(2, probeColleague.Received.Count);
		Assert.Equal("dup", probeColleague.Received[1].Key);
		Assert.Equal("from-second", probeColleague.Received[1].Value);
	}

	/// <summary>
	/// 验证 Mediator_Register参数校验 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Throws、Register；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Mediator_Register参数校验()
	{
		Mediator mediator = new Mediator();
		Assert.Throws<ArgumentNullException>(delegate
		{
			mediator.Register(null);
		});
		Assert.Throws<ArgumentException>(delegate
		{
			mediator.Register(new ProbeColleague(""));
		});
	}

	/// <summary>
	/// 验证 Mediator_接收者抛异常被吞掉_不影响调用方 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、Send；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Mediator_接收者抛异常被吞掉_不影响调用方()
	{
		Mediator mediator = new Mediator();
		mediator.Register(new ProbeColleague("bad")
		{
			OnReceive = delegate
			{
				throw new InvalidOperationException("炸");
			}
		});
		mediator.Send("x", "bad", "m");
	}

	/// <summary>
	/// 验证 Mediator_Send的接收者标识为空抛ArgumentException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Throws、Send；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Mediator_Send的接收者标识为空抛ArgumentException()
	{
		Mediator mediator = new Mediator();
		Assert.Throws<ArgumentException>(delegate
		{
			mediator.Send("x", "", "m");
		});
		Assert.Throws<ArgumentException>(delegate
		{
			mediator.Send("x", null, "m");
		});
	}

	/// <summary>
	/// 验证 Mediator_Send目标不存在时静默返回 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Send；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Mediator_Send目标不存在时静默返回()
	{
		Mediator mediator = new Mediator();
		mediator.Send("x", "不存在", "m");
	}

	/// <summary>
	/// 验证 Mediator_Broadcast会跳过发送者自己 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、Broadcast、Empty、Single；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Mediator_Broadcast会跳过发送者自己()
	{
		Mediator mediator = new Mediator();
		ProbeColleague probeColleague = new ProbeColleague("me");
		ProbeColleague probeColleague2 = new ProbeColleague("other");
		mediator.Register(probeColleague);
		mediator.Register(probeColleague2);
		mediator.Broadcast("me", "广播");
		Assert.Empty(probeColleague.Received);
		Assert.Single(probeColleague2.Received);
	}

	/// <summary>
	/// 验证 Mediator_Broadcast_一个同事抛异常不影响其他同事 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、Broadcast、Single；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Mediator_Broadcast_一个同事抛异常不影响其他同事()
	{
		Mediator mediator = new Mediator();
		ProbeColleague probeColleague = new ProbeColleague("good");
		mediator.Register(new ProbeColleague("bad")
		{
			OnReceive = delegate
			{
				throw new InvalidOperationException("炸");
			}
		});
		mediator.Register(probeColleague);
		mediator.Broadcast("sender", "广播");
		Assert.Single(probeColleague.Received);
	}

	/// <summary>
	/// 验证 Mediator_BroadcastAsync_接收者抛异常时会向外抛_与注释相反 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、ThrowsAsync、BroadcastAsync；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task Mediator_BroadcastAsync_接收者抛异常时会向外抛_与注释相反()
	{
		Mediator mediator = new Mediator();
		mediator.Register(new ProbeColleague("bad")
		{
			OnReceive = delegate
			{
				throw new InvalidOperationException("炸");
			}
		});
		await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.BroadcastAsync("sender", "广播"));
	}

	/// <summary>
	/// 验证 Mediator_SendAsync与BroadcastAsync正常路径 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、SendAsync、BroadcastAsync、Equal、Single；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task Mediator_SendAsync与BroadcastAsync正常路径()
	{
		Mediator mediator = new Mediator();
		ProbeColleague target = new ProbeColleague("t");
		ProbeColleague other = new ProbeColleague("o");
		mediator.Register(target);
		mediator.Register(other);
		await mediator.SendAsync("s", "t", "单播");
		await mediator.BroadcastAsync("s", "广播");
		Assert.Equal(2, target.Received.Count);
		Assert.Single(other.Received);
	}

	/// <summary>
	/// 验证 Mediator_Dispose之后Send静默返回但Register抛 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、Dispose、Throws、Unregister、ClearColleagues；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task Mediator_Dispose之后Send静默返回但Register抛()
	{
		Mediator mediator = new Mediator();
		ProbeColleague colleague = new ProbeColleague("a");
		mediator.Register(colleague);
		mediator.Dispose();
		Assert.Throws<ObjectDisposedException>(delegate
		{
			mediator.Register(colleague);
		});
		Assert.Throws<ObjectDisposedException>(delegate
		{
			mediator.Unregister(colleague);
		});
		Assert.Throws<ObjectDisposedException>(delegate
		{
			mediator.ClearColleagues();
		});
		mediator.Send("x", "a", "m");
		mediator.Broadcast("x", "m");
		await mediator.SendAsync("x", "a", "m");
		await mediator.BroadcastAsync("x", "m");
		Assert.Empty(colleague.Received);
		Assert.Equal(0, mediator.ColleagueCount);
	}

	/// <summary>
	/// 验证 Mediator_Unregister后不再收到消息 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、Unregister、Send、Empty、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Mediator_Unregister后不再收到消息()
	{
		Mediator mediator = new Mediator();
		ProbeColleague probeColleague = new ProbeColleague("a");
		mediator.Register(probeColleague);
		mediator.Unregister(probeColleague);
		mediator.Send("x", "a", "m");
		Assert.Empty(probeColleague.Received);
		Assert.Equal(0, mediator.ColleagueCount);
	}

	/// <summary>
	/// 验证 Mediator_批量注册与批量取消 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 RegisterRange、Equal、UnregisterRange、Throws、Unregister；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Mediator_批量注册与批量取消()
	{
		Mediator mediator = new Mediator();
		ProbeColleague probeColleague = new ProbeColleague("a");
		ProbeColleague probeColleague2 = new ProbeColleague("b");
		mediator.RegisterRange(new IColleague[3] { probeColleague, probeColleague2, null });
		Assert.Equal(2, mediator.ColleagueCount);
		mediator.UnregisterRange(new IColleague[2] { probeColleague, null });
		Assert.Equal(1, mediator.ColleagueCount);
		Assert.Throws<ArgumentNullException>(delegate
		{
			mediator.RegisterRange(null);
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			mediator.UnregisterRange(null);
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			mediator.Unregister(null);
		});
	}

	/// <summary>
	/// 验证 ColleagueBase_Dispose之后Send抛但DelegateColleague的Receive不抛 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Dispose、Throws、Send、SetMediator、Broadcast；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task ColleagueBase_Dispose之后Send抛但DelegateColleague的Receive不抛()
	{
		DelegateColleague colleague = new DelegateColleague("d", delegate
		{
		});
		colleague.Dispose();
		Assert.Throws<ObjectDisposedException>(delegate
		{
			colleague.Send("x", "m");
		});
		Assert.Throws<ObjectDisposedException>(delegate
		{
			colleague.SetMediator(new Mediator());
		});
		Assert.Throws<ObjectDisposedException>(delegate
		{
			colleague.Broadcast("m");
		});
		colleague.Receive("x", "m");
		await colleague.ReceiveAsync("x", "m");
	}

	/// <summary>
	/// 验证 ColleagueBase_未覆写ReceiveAsync时Dispose后会抛 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Dispose、ThrowsAsync、ReceiveAsync；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task ColleagueBase_未覆写ReceiveAsync时Dispose后会抛()
	{
		ProbeColleague colleague = new ProbeColleague("p");
		colleague.Dispose();
		await Assert.ThrowsAsync<ObjectDisposedException>(() => colleague.ReceiveAsync("x", "m"));
	}

	/// <summary>
	/// 验证 DelegateColleague_构造函数参数校验 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Throws；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void DelegateColleague_构造函数参数校验()
	{
		Assert.Throws<ArgumentNullException>(() => new DelegateColleague("d", null));
		Assert.Throws<ArgumentNullException>(() => new DelegateColleague(null, delegate
		{
		}));
	}

	/// <summary>
	/// 验证 DelegateColleague_没有异步处理器时回落到同步处理器 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Add、ReceiveAsync、Equal、ToArray；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task DelegateColleague_没有异步处理器时回落到同步处理器()
	{
		List<object> seen = new List<object>();
		DelegateColleague colleague = new DelegateColleague("d", delegate(string from, object message)
		{
			seen.Add(message);
		});
		await colleague.ReceiveAsync("x", "载荷");
		Assert.Equal(new object[1] { "载荷" }, seen.ToArray());
	}

	/// <summary>
	/// 验证 DelegateColleague_有异步处理器时优先用异步的 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 ReceiveAsync、Receive、Equal；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task DelegateColleague_有异步处理器时优先用异步的()
	{
		int syncCount = 0;
		int asyncCount = 0;
		DelegateColleague colleague = new DelegateColleague("d", delegate
		{
			syncCount++;
		}, delegate
		{
			asyncCount++;
			return Task.CompletedTask;
		});
		await colleague.ReceiveAsync("x", "载荷");
		colleague.Receive("x", "载荷");
		Assert.Equal(1, asyncCount);
		Assert.Equal(1, syncCount);
	}

	/// <summary>
	/// 验证 TypeFilter_空白名单放行一切 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 True、ShouldProcess；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void TypeFilter_空白名单放行一切()
	{
		TypeFilter typeFilter = new TypeFilter(new string[0]);
		Assert.True(typeFilter.ShouldProcess(new PriorityMessage(null, MessagePriority.Low)));
	}

	/// <summary>
	/// 验证 TypeFilter_非空白名单会丢弃MessageType为空的消息 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 False、ShouldProcess、True；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void TypeFilter_非空白名单会丢弃MessageType为空的消息()
	{
		TypeFilter typeFilter = new TypeFilter(new string[1] { "A" });
		Assert.False(typeFilter.ShouldProcess(new PriorityMessage(null, MessagePriority.High)));
		Assert.True(typeFilter.ShouldProcess(new PriorityMessage(null, MessagePriority.High, "A")));
		Assert.False(typeFilter.ShouldProcess(new PriorityMessage(null, MessagePriority.High, "B")));
	}

	/// <summary>
	/// 验证 TypeFilter_白名单显式含空串时放行空类型 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 True、ShouldProcess；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void TypeFilter_白名单显式含空串时放行空类型()
	{
		TypeFilter typeFilter = new TypeFilter(new string[1] { "" });
		Assert.True(typeFilter.ShouldProcess(new PriorityMessage(null, MessagePriority.Low)));
	}

	/// <summary>
	/// 验证 TypeFilter_构造函数对null抛ArgumentNullException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Throws；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void TypeFilter_构造函数对null抛ArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => new TypeFilter(null));
	}

	/// <summary>
	/// 验证 PriorityFilter_按最低优先级过滤 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 False、ShouldProcess、True；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void PriorityFilter_按最低优先级过滤()
	{
		PriorityFilter priorityFilter = new PriorityFilter(MessagePriority.High);
		Assert.False(priorityFilter.ShouldProcess(new PriorityMessage(null, MessagePriority.Normal)));
		Assert.True(priorityFilter.ShouldProcess(new PriorityMessage(null, MessagePriority.High)));
		Assert.True(priorityFilter.ShouldProcess(new PriorityMessage(null, MessagePriority.Critical)));
	}

	/// <summary>
	/// 验证 CompositeFilter_空过滤器列表一律通过 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 True、ShouldProcess；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void CompositeFilter_空过滤器列表一律通过()
	{
		CompositeFilter compositeFilter = new CompositeFilter(new IMessageFilter[0]);
		Assert.True(compositeFilter.ShouldProcess(new PriorityMessage(null, MessagePriority.Low)));
	}

	/// <summary>
	/// 验证 CompositeFilter_必须全部通过才通过 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 True、ShouldProcess、False；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void CompositeFilter_必须全部通过才通过()
	{
		CompositeFilter compositeFilter = new CompositeFilter(new IMessageFilter[2]
		{
			new PriorityFilter(MessagePriority.High),
			new TypeFilter(new string[1] { "A" })
		});
		Assert.True(compositeFilter.ShouldProcess(new PriorityMessage(null, MessagePriority.High, "A")));
		Assert.False(compositeFilter.ShouldProcess(new PriorityMessage(null, MessagePriority.Low, "A")));
		Assert.False(compositeFilter.ShouldProcess(new PriorityMessage(null, MessagePriority.High, "B")));
	}

	/// <summary>
	/// 验证 CompositeFilter_构造函数对null抛ArgumentNullException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Throws；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void CompositeFilter_构造函数对null抛ArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => new CompositeFilter(null));
	}

	/// <summary>
	/// 验证 FilteredColleague_会丢弃所有非PriorityMessage消息 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Receive、Empty、Single；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void FilteredColleague_会丢弃所有非PriorityMessage消息()
	{
		ProbeColleague probeColleague = new ProbeColleague("i");
		FilteredColleague filteredColleague = new FilteredColleague(probeColleague, new CompositeFilter(new IMessageFilter[0]));
		filteredColleague.Receive("x", "普通字符串");
		filteredColleague.Receive("x", 123);
		Assert.Empty(probeColleague.Received);
		filteredColleague.Receive("x", new PriorityMessage("数据", MessagePriority.High));
		Assert.Single(probeColleague.Received);
	}

	/// <summary>
	/// 验证 FilteredColleague_的丢弃语义来自is_PriorityMessage这个形态 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 SourcePath、True、Exists、ReadAllText、Contains；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void FilteredColleague_的丢弃语义来自is_PriorityMessage这个形态()
	{
		string text = SourcePath("Patterns", "Coordination", "Mediator", "Advanced", "AdvancedMediatorPattern.cs");
		Assert.True(File.Exists(text), "找不到源码文件：" + text);
		string actualString = File.ReadAllText(text);
		Assert.Contains("if (message is PriorityMessage priorityMessage && _filter.ShouldProcess(priorityMessage))", actualString);
	}

	/// <summary>
	/// 验证 FilteredColleague_Id委托给内层 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void FilteredColleague_Id委托给内层()
	{
		ProbeColleague innerColleague = new ProbeColleague("inner-id");
		FilteredColleague filteredColleague = new FilteredColleague(innerColleague, new CompositeFilter(new IMessageFilter[0]));
		Assert.Equal("inner-id", filteredColleague.Id);
	}

	/// <summary>
	/// 验证 FilteredColleague_Dispose会连带Dispose内层 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Dispose、Equal、False、IsAssignableFrom；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void FilteredColleague_Dispose会连带Dispose内层()
	{
		ProbeColleague probeColleague = new ProbeColleague("i");
		FilteredColleague filteredColleague = new FilteredColleague(probeColleague, new CompositeFilter(new IMessageFilter[0]));
		filteredColleague.Dispose();
		Assert.Equal(1, probeColleague.DisposeCount);
		Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(IColleague)));
		Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(ColleagueBase)));
		filteredColleague.Dispose();
		Assert.Equal(1, probeColleague.DisposeCount);
	}

	/// <summary>
	/// 验证 FilteredColleague_Dispose之后自身全部入口抛ObjectDisposedException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Dispose、Throws、SetMediator、Receive、Send；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void FilteredColleague_Dispose之后自身全部入口抛ObjectDisposedException()
	{
		FilteredColleague filtered = new FilteredColleague(new ProbeColleague("i"), new CompositeFilter(new IMessageFilter[0]));
		filtered.Dispose();
		Assert.Throws<ObjectDisposedException>(delegate
		{
			filtered.SetMediator(new Mediator());
		});
		Assert.Throws<ObjectDisposedException>(delegate
		{
			filtered.Receive("x", "m");
		});
		Assert.Throws<ObjectDisposedException>(delegate
		{
			filtered.Send("x", "m");
		});
		Assert.Throws<ObjectDisposedException>(delegate
		{
			filtered.Broadcast("m");
		});
	}

	/// <summary>
	/// 验证 FilteredColleague_构造函数参数校验 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Throws；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void FilteredColleague_构造函数参数校验()
	{
		Assert.Throws<ArgumentNullException>(() => new FilteredColleague(null, new CompositeFilter(new IMessageFilter[0])));
		Assert.Throws<ArgumentNullException>(() => new FilteredColleague(new ProbeColleague("i"), null));
	}

	/// <summary>
	/// 验证 FilteredColleague_过滤不通过时不转发且异步返回已完成任务 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 ReceiveAsync、Empty、Single；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task FilteredColleague_过滤不通过时不转发且异步返回已完成任务()
	{
		ProbeColleague inner = new ProbeColleague("i");
		FilteredColleague filtered = new FilteredColleague(inner, new PriorityFilter(MessagePriority.Critical));
		await filtered.ReceiveAsync("x", new PriorityMessage("低", MessagePriority.Low));
		Assert.Empty(inner.Received);
		await filtered.ReceiveAsync("x", new PriorityMessage("高", MessagePriority.Critical));
		Assert.Single(inner.Received);
	}

	/// <summary>
	/// 验证 AdvancedMediator_SendWithPriority会把消息包装成PriorityMessage 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、SendWithPriority、IsType、Equal、Null；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AdvancedMediator_SendWithPriority会把消息包装成PriorityMessage()
	{
		AdvancedMediator advancedMediator = new AdvancedMediator();
		ProbeColleague probeColleague = new ProbeColleague("t");
		advancedMediator.Register(probeColleague);
		advancedMediator.SendWithPriority("s", "t", "载荷", MessagePriority.Critical);
		PriorityMessage priorityMessage = Assert.IsType<PriorityMessage>(probeColleague.Received[0].Value);
		Assert.Equal(MessagePriority.Critical, priorityMessage.Priority);
		Assert.Equal("载荷", priorityMessage.Data);
		Assert.Null(priorityMessage.MessageType);
	}

	/// <summary>
	/// 验证 AdvancedMediator_SendWithPriorityAsync会把消息包装成PriorityMessage 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、SendWithPriorityAsync、IsType、Equal；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task AdvancedMediator_SendWithPriorityAsync会把消息包装成PriorityMessage()
	{
		AdvancedMediator mediator = new AdvancedMediator();
		ProbeColleague colleague = new ProbeColleague("t");
		mediator.Register(colleague);
		await mediator.SendWithPriorityAsync("s", "t", "载荷", MessagePriority.Low);
		PriorityMessage received = Assert.IsType<PriorityMessage>(colleague.Received[0].Value);
		Assert.Equal(MessagePriority.Low, received.Priority);
	}

	/// <summary>
	/// 验证 AdvancedMediator_RegisterWithFilter会包成FilteredColleague并沿用内层Id 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 RegisterWithFilter、Equal、SendWithPriority、Empty、Single；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AdvancedMediator_RegisterWithFilter会包成FilteredColleague并沿用内层Id()
	{
		AdvancedMediator advancedMediator = new AdvancedMediator();
		ProbeColleague probeColleague = new ProbeColleague("t");
		advancedMediator.RegisterWithFilter(probeColleague, new PriorityFilter(MessagePriority.High));
		Assert.Equal(1, advancedMediator.ColleagueCount);
		advancedMediator.SendWithPriority("s", "t", "低", MessagePriority.Low);
		Assert.Empty(probeColleague.Received);
		advancedMediator.SendWithPriority("s", "t", "高", MessagePriority.High);
		Assert.Single(probeColleague.Received);
	}

	/// <summary>
	/// 验证 AdvancedMediator_Register与RegisterWithFilter的参数校验 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Throws、Register、RegisterWithFilter；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AdvancedMediator_Register与RegisterWithFilter的参数校验()
	{
		AdvancedMediator mediator = new AdvancedMediator();
		Assert.Throws<ArgumentNullException>(delegate
		{
			mediator.Register(null);
		});
		Assert.Throws<ArgumentException>(delegate
		{
			mediator.Register(new ProbeColleague(""));
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			mediator.RegisterWithFilter(null, new PriorityFilter(MessagePriority.Low));
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			mediator.RegisterWithFilter(new ProbeColleague("a"), null);
		});
	}

	/// <summary>
	/// 验证 AdvancedMediator_重复Id注册是静默忽略 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、Equal、Send、Single、Empty；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AdvancedMediator_重复Id注册是静默忽略()
	{
		AdvancedMediator advancedMediator = new AdvancedMediator();
		ProbeColleague probeColleague = new ProbeColleague("dup");
		ProbeColleague probeColleague2 = new ProbeColleague("dup");
		advancedMediator.Register(probeColleague);
		advancedMediator.Register(probeColleague2);
		Assert.Equal(1, advancedMediator.ColleagueCount);
		advancedMediator.Send("x", "dup", "m");
		Assert.Single(probeColleague.Received);
		Assert.Empty(probeColleague2.Received);
	}

	/// <summary>
	/// 验证 AdvancedMediator_广播不保证投递顺序_只保证都收到 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 RegisterRange、BroadcastWithPriority、Single、All、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AdvancedMediator_广播不保证投递顺序_只保证都收到()
	{
		AdvancedMediator advancedMediator = new AdvancedMediator();
		ProbeColleague probeColleague = new ProbeColleague("a");
		ProbeColleague probeColleague2 = new ProbeColleague("b");
		ProbeColleague probeColleague3 = new ProbeColleague("c");
		advancedMediator.RegisterRange(new IColleague[3] { probeColleague, probeColleague2, probeColleague3 });
		advancedMediator.BroadcastWithPriority("sender", "广播", MessagePriority.Critical);
		Assert.Single(probeColleague.Received);
		Assert.Single(probeColleague2.Received);
		Assert.Single(probeColleague3.Received);
		Assert.All(probeColleague.Received, delegate(KeyValuePair<string, object> r)
		{
			Assert.Equal(MessagePriority.Critical, Assert.IsType<PriorityMessage>(r.Value).Priority);
		});
	}

	/// <summary>
	/// 验证 AdvancedMediator_BroadcastAsync_接收者抛异常会向外抛 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、ThrowsAsync、BroadcastAsync；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task AdvancedMediator_BroadcastAsync_接收者抛异常会向外抛()
	{
		AdvancedMediator mediator = new AdvancedMediator();
		mediator.Register(new ProbeColleague("bad")
		{
			OnReceive = delegate
			{
				throw new InvalidOperationException("炸");
			}
		});
		await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.BroadcastAsync("s", "广播"));
	}

	/// <summary>
	/// 验证 AdvancedMediator_Dispose之后Send静默返回但Register抛 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、Dispose、Throws、Unregister、ClearColleagues；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task AdvancedMediator_Dispose之后Send静默返回但Register抛()
	{
		AdvancedMediator mediator = new AdvancedMediator();
		ProbeColleague colleague = new ProbeColleague("a");
		mediator.Register(colleague);
		mediator.Dispose();
		Assert.Throws<ObjectDisposedException>(delegate
		{
			mediator.Register(colleague);
		});
		Assert.Throws<ObjectDisposedException>(delegate
		{
			mediator.Unregister(colleague);
		});
		Assert.Throws<ObjectDisposedException>(delegate
		{
			mediator.ClearColleagues();
		});
		Assert.Throws<ObjectDisposedException>(delegate
		{
			mediator.RegisterWithFilter(colleague, new PriorityFilter(MessagePriority.Low));
		});
		mediator.Send("x", "a", "m");
		mediator.SendWithPriority("x", "a", "m", MessagePriority.High);
		mediator.Broadcast("x", "m");
		mediator.BroadcastWithPriority("x", "m", MessagePriority.High);
		await mediator.SendAsync("x", "a", "m");
		await mediator.SendWithPriorityAsync("x", "a", "m", MessagePriority.High);
		await mediator.BroadcastAsync("x", "m");
		await mediator.BroadcastWithPriorityAsync("x", "m", MessagePriority.High);
		Assert.Empty(colleague.Received);
		Assert.Equal(0, mediator.ColleagueCount);
	}

	/// <summary>
	/// 验证 MessageRouter_路由到已注册通道 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、RegisterChannel、RouteAndSend、Single；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void MessageRouter_路由到已注册通道()
	{
		AdvancedMediator advancedMediator = new AdvancedMediator();
		ProbeColleague probeColleague = new ProbeColleague("t");
		advancedMediator.Register(probeColleague);
		MessageRouter messageRouter = new MessageRouter((PriorityMessage m) => m.MessageType);
		messageRouter.RegisterChannel("chan", advancedMediator);
		messageRouter.RouteAndSend("s", "t", new PriorityMessage("载荷", MessagePriority.High, "chan"));
		Assert.Single(probeColleague.Received);
	}

	/// <summary>
	/// 验证 MessageRouter_通道未注册或路由结果为空时静默丢弃 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、RegisterChannel、RouteAndSend、Empty；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void MessageRouter_通道未注册或路由结果为空时静默丢弃()
	{
		AdvancedMediator advancedMediator = new AdvancedMediator();
		ProbeColleague probeColleague = new ProbeColleague("t");
		advancedMediator.Register(probeColleague);
		MessageRouter messageRouter = new MessageRouter((PriorityMessage m) => m.MessageType);
		messageRouter.RegisterChannel("chan", advancedMediator);
		messageRouter.RouteAndSend("s", "t", new PriorityMessage("载荷", MessagePriority.High, "未注册通道"));
		messageRouter.RouteAndSend("s", "t", new PriorityMessage("载荷", MessagePriority.High));
		Assert.Empty(probeColleague.Received);
	}

	/// <summary>
	/// 验证 MessageRouter_参数校验与重复通道覆盖 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Throws、RegisterChannel、Register、RouteAndSend、Single；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void MessageRouter_参数校验与重复通道覆盖()
	{
		Assert.Throws<ArgumentNullException>(() => new MessageRouter(null));
		MessageRouter router = new MessageRouter((PriorityMessage m) => "chan");
		Assert.Throws<ArgumentException>(delegate
		{
			router.RegisterChannel("", new AdvancedMediator());
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			router.RegisterChannel("chan", null);
		});
		AdvancedMediator mediator = new AdvancedMediator();
		AdvancedMediator advancedMediator = new AdvancedMediator();
		router.RegisterChannel("chan", mediator);
		router.RegisterChannel("chan", advancedMediator);
		ProbeColleague probeColleague = new ProbeColleague("t");
		advancedMediator.Register(probeColleague);
		router.RouteAndSend("s", "t", new PriorityMessage("载荷", MessagePriority.High, "chan"));
		Assert.Single(probeColleague.Received);
	}

	/// <summary>
	/// 验证 MessageRouter_异步路由 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、RegisterChannel、RouteAndSendAsync、RouteAndBroadcastAsync、Equal；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task MessageRouter_异步路由()
	{
		AdvancedMediator mediator = new AdvancedMediator();
		ProbeColleague colleague = new ProbeColleague("t");
		mediator.Register(colleague);
		MessageRouter router = new MessageRouter((PriorityMessage m) => "chan");
		router.RegisterChannel("chan", mediator);
		await router.RouteAndSendAsync("s", "t", new PriorityMessage("载荷", MessagePriority.High, "chan"));
		await router.RouteAndBroadcastAsync("s", new PriorityMessage("广播", MessagePriority.High, "chan"));
		Assert.Equal(2, colleague.Received.Count);
	}

	/// <summary>
	/// 验证 MessageRouter_Dispose会Dispose所有中介者 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、RegisterChannel、Dispose、Equal、Throws；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void MessageRouter_Dispose会Dispose所有中介者()
	{
		AdvancedMediator advancedMediator = new AdvancedMediator();
		advancedMediator.Register(new ProbeColleague("t"));
		MessageRouter router = new MessageRouter((PriorityMessage m) => "chan");
		router.RegisterChannel("chan", advancedMediator);
		router.Dispose();
		Assert.Equal(0, advancedMediator.ColleagueCount);
		Assert.Throws<ObjectDisposedException>(delegate
		{
			router.RegisterChannel("chan2", new AdvancedMediator());
		});
	}

	/// <summary>
	/// 验证 MessageRouter_Dispose之后路由静默返回 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Register、RegisterChannel、Dispose、RouteAndSend、RouteAndBroadcast；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void MessageRouter_Dispose之后路由静默返回()
	{
		AdvancedMediator advancedMediator = new AdvancedMediator();
		ProbeColleague probeColleague = new ProbeColleague("t");
		advancedMediator.Register(probeColleague);
		MessageRouter messageRouter = new MessageRouter((PriorityMessage m) => "chan");
		messageRouter.RegisterChannel("chan", advancedMediator);
		messageRouter.Dispose();
		messageRouter.RouteAndSend("s", "t", new PriorityMessage("载荷", MessagePriority.High, "chan"));
		messageRouter.RouteAndBroadcast("s", new PriorityMessage("载荷", MessagePriority.High, "chan"));
		Assert.Empty(probeColleague.Received);
	}

	/// <summary>
	/// 验证 ThisFile 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// </summary>
	private static string ThisFile([CallerFilePath] string path = "")
	{
		return path;
	}

	/// <summary>
	/// 验证 SourcePath 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 GetFullPath、Combine、GetDirectoryName、ThisFile；具体条件和先后次序见方法体。
	/// </summary>
	private static string SourcePath(params string[] segments)
	{
		string fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile()), "..", "..", "Extension.Core"));
		string text = fullPath;
		foreach (string path in segments)
		{
			text = Path.Combine(text, path);
		}
		return text;
	}
}
