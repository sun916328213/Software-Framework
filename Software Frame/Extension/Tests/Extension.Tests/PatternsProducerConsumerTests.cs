// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：PatternsProducerConsumerTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.IO、System.Linq、System.Reflection、System.Runtime.CompilerServices。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Extension.Patterns.Pipelines.ProducerConsumer;
using Extension.Patterns.Pipelines.ProducerConsumer.Advanced;
using Xunit;

namespace Extension.Tests;

/// <summary>
/// PatternsProducerConsumerTests 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class PatternsProducerConsumerTests
{
	/// <summary>
	/// 验证 ShortLived 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 FromMilliseconds；具体条件和先后次序见方法体。
	/// </summary>
	private static CancellationTokenSource ShortLived(int milliseconds = 300)
	{
		return new CancellationTokenSource(TimeSpan.FromMilliseconds(milliseconds, 0L));
	}

	/// <summary>
	/// 验证 ProducerConsumer_初始计数为0 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ProducerConsumer_初始计数为0()
	{
		ProducerConsumer<int> producerConsumer = new ProducerConsumer<int>();
		Assert.Equal(0L, producerConsumer.ProducedCount);
		Assert.Equal(0L, producerConsumer.ConsumedCount);
	}

	/// <summary>
	/// 验证 ProducerConsumer_StartAsync参数校验 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 ThrowsAsync、StartAsync；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task ProducerConsumer_StartAsync参数校验()
	{
		ProducerConsumer<int> pc = new ProducerConsumer<int>();
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => pc.StartAsync(0, 1, (int i) => i, delegate
		{
		}));
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => pc.StartAsync(1, 0, (int i) => i, delegate
		{
		}));
		await Assert.ThrowsAsync<ArgumentNullException>(() => pc.StartAsync(1, 1, null, delegate
		{
		}));
		await Assert.ThrowsAsync<ArgumentNullException>(() => pc.StartAsync(1, 1, (int i) => i, null));
	}

	/// <summary>
	/// 验证 ProducerConsumer_Stop是幂等的 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Stop、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ProducerConsumer_Stop是幂等的()
	{
		ProducerConsumer<int> producerConsumer = new ProducerConsumer<int>();
		producerConsumer.Stop();
		producerConsumer.Stop();
		Assert.Equal(0L, producerConsumer.ProducedCount);
	}

	/// <summary>
	/// 验证 ProducerConsumer_Dispose之后Stop是空操作 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Dispose、Stop；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ProducerConsumer_Dispose之后Stop是空操作()
	{
		ProducerConsumer<int> producerConsumer = new ProducerConsumer<int>();
		producerConsumer.Dispose();
		producerConsumer.Stop();
		producerConsumer.Dispose();
	}

	/// <summary>
	/// 验证 ProducerConsumer_Stop之后StartAsync抛InvalidOperationException_不再忙循环挂起 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Stop、ShortLived、Contains、ThrowsAsync、StartAsync；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task ProducerConsumer_Stop之后StartAsync抛InvalidOperationException_不再忙循环挂起()
	{
		ProducerConsumer<int> pc = new ProducerConsumer<int>();
		pc.Stop();
		int consumed = 0;
		CancellationTokenSource cts = ShortLived();
		try
		{
			Assert.Contains("已停止", (await Assert.ThrowsAsync<InvalidOperationException>(() => pc.StartAsync(1, 1, (int i) => i, delegate
			{
				Interlocked.Increment(ref consumed);
			}, cts.Token))).Message);
			Assert.Equal(0, consumed);
			Assert.Equal(0L, pc.ProducedCount);
			Assert.Equal(0L, pc.ConsumedCount);
		}
		finally
		{
			if (cts != null)
			{
				((IDisposable)cts).Dispose();
			}
		}
	}

	/// <summary>
	/// 验证 ProducerConsumer_Dispose之后StartAsync抛ObjectDisposedException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Dispose、ShortLived、ThrowsAsync、StartAsync；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task ProducerConsumer_Dispose之后StartAsync抛ObjectDisposedException()
	{
		ProducerConsumer<int> pc = new ProducerConsumer<int>();
		pc.Dispose();
		CancellationTokenSource cts = ShortLived();
		try
		{
			await Assert.ThrowsAsync<ObjectDisposedException>(() => pc.StartAsync(1, 1, (int i) => i, delegate
			{
			}, cts.Token));
		}
		finally
		{
			if (cts != null)
			{
				((IDisposable)cts).Dispose();
			}
		}
	}

	/// <summary>
	/// 验证 EnhancedProducerConsumer_Stop之后StartAsync抛InvalidOperationException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Stop、ShortLived、Contains、ThrowsAsync、StartAsync；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task EnhancedProducerConsumer_Stop之后StartAsync抛InvalidOperationException()
	{
		EnhancedProducerConsumer<int> pc = new EnhancedProducerConsumer<int>();
		pc.Stop();
		int consumed = 0;
		CancellationTokenSource cts = ShortLived();
		try
		{
			Assert.Contains("已停止", (await Assert.ThrowsAsync<InvalidOperationException>(() => pc.StartAsync(1, 1, (int i) => i, delegate
			{
				Interlocked.Increment(ref consumed);
			}, cts.Token))).Message);
			Assert.Equal(0, consumed);
			Assert.Equal(0L, pc.ProducedCount);
		}
		finally
		{
			if (cts != null)
			{
				((IDisposable)cts).Dispose();
			}
		}
	}

	/// <summary>
	/// 验证 EnhancedProducerConsumer_Dispose之后StartAsync抛ObjectDisposedException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Dispose、ShortLived、ThrowsAsync、StartAsync；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task EnhancedProducerConsumer_Dispose之后StartAsync抛ObjectDisposedException()
	{
		EnhancedProducerConsumer<int> pc = new EnhancedProducerConsumer<int>();
		pc.Dispose();
		CancellationTokenSource cts = ShortLived();
		try
		{
			await Assert.ThrowsAsync<ObjectDisposedException>(() => pc.StartAsync(1, 1, (int i) => i, delegate
			{
			}, cts.Token));
		}
		finally
		{
			if (cts != null)
			{
				((IDisposable)cts).Dispose();
			}
		}
	}

	/// <summary>
	/// 验证 ProducerConsumer_能真的生产并累计计数 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 ShortLived、StartAsync、True；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task ProducerConsumer_能真的生产并累计计数()
	{
		ProducerConsumer<int> pc = new ProducerConsumer<int>(50);
		using CancellationTokenSource cts = ShortLived(250);
		await pc.StartAsync(1, 1, (int i) => i, delegate
		{
		}, cts.Token);
		Assert.True(pc.ProducedCount > 0, $"实际 ProducedCount = {pc.ProducedCount}");
	}

	/// <summary>
	/// 验证 ProducerConsumer_Stop不重置计数器 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 ShortLived、StartAsync、Stop、Equal、True；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task ProducerConsumer_Stop不重置计数器()
	{
		ProducerConsumer<int> pc = new ProducerConsumer<int>(50);
		using (CancellationTokenSource cts = ShortLived(250))
		{
			await pc.StartAsync(1, 1, (int i) => i, delegate
			{
			}, cts.Token);
		}
		long before = pc.ProducedCount;
		pc.Stop();
		Assert.Equal(before, pc.ProducedCount);
		Assert.True(before > 0);
	}

	/// <summary>
	/// 验证 EnhancedProducerConsumer_未Stop时可以生产与消费且保持FIFO 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 True、TryProduceItem、Equal、TryConsumeItem；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void EnhancedProducerConsumer_未Stop时可以生产与消费且保持FIFO()
	{
		EnhancedProducerConsumer<int> enhancedProducerConsumer = new EnhancedProducerConsumer<int>();
		Assert.True(enhancedProducerConsumer.TryProduceItem(1));
		Assert.True(enhancedProducerConsumer.TryProduceItem(2));
		Assert.Equal(2L, enhancedProducerConsumer.ProducedCount);
		Assert.Equal(2, enhancedProducerConsumer.WaitingItemCount);
		Assert.True(enhancedProducerConsumer.TryConsumeItem(out var item));
		Assert.Equal(1, item);
		Assert.Equal(1L, enhancedProducerConsumer.ConsumedCount);
		Assert.Equal(1, enhancedProducerConsumer.WaitingItemCount);
	}

	/// <summary>
	/// 验证 EnhancedProducerConsumer_ConsumeItemAsync取到最早入队的 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 TryProduceItem、Equal、ConsumeItemAsync；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task EnhancedProducerConsumer_ConsumeItemAsync取到最早入队的()
	{
		EnhancedProducerConsumer<int> pc = new EnhancedProducerConsumer<int>();
		pc.TryProduceItem(7);
		Assert.Equal(7, await pc.ConsumeItemAsync());
		Assert.Equal(1L, pc.ConsumedCount);
	}

	/// <summary>
	/// 验证 EnhancedProducerConsumer_TryConsumeItem失败时out参数是default 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 False、TryConsumeItem、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void EnhancedProducerConsumer_TryConsumeItem失败时out参数是default()
	{
		EnhancedProducerConsumer<int> enhancedProducerConsumer = new EnhancedProducerConsumer<int>();
		Assert.False(enhancedProducerConsumer.TryConsumeItem(out var item));
		Assert.Equal(0, item);
	}

	/// <summary>
	/// 验证 EnhancedProducerConsumer_通道满时TryProduceItem返回false 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 True、TryProduceItem、False、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void EnhancedProducerConsumer_通道满时TryProduceItem返回false()
	{
		EnhancedProducerConsumer<int> enhancedProducerConsumer = new EnhancedProducerConsumer<int>(2);
		Assert.True(enhancedProducerConsumer.TryProduceItem(1));
		Assert.True(enhancedProducerConsumer.TryProduceItem(2));
		Assert.False(enhancedProducerConsumer.TryProduceItem(3));
		Assert.Equal(2L, enhancedProducerConsumer.ProducedCount);
	}

	/// <summary>
	/// 验证 EnhancedProducerConsumer_Clear返回被清掉的条数 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 TryProduceItem、Equal、Clear；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void EnhancedProducerConsumer_Clear返回被清掉的条数()
	{
		EnhancedProducerConsumer<int> enhancedProducerConsumer = new EnhancedProducerConsumer<int>();
		enhancedProducerConsumer.TryProduceItem(1);
		enhancedProducerConsumer.TryProduceItem(2);
		Assert.Equal(2, enhancedProducerConsumer.Clear());
		Assert.Equal(0, enhancedProducerConsumer.WaitingItemCount);
		Assert.Equal(2L, enhancedProducerConsumer.ProducedCount);
	}

	/// <summary>
	/// 验证 EnhancedProducerConsumer_Stop之后写入抛读取抛而Try版本返回false 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Stop、ThrowsAsync、ProduceItemAsync、ConsumeItemAsync、False；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task EnhancedProducerConsumer_Stop之后写入抛读取抛而Try版本返回false()
	{
		EnhancedProducerConsumer<int> pc = new EnhancedProducerConsumer<int>();
		pc.Stop();
		await Assert.ThrowsAsync<InvalidOperationException>(() => pc.ProduceItemAsync(1));
		await Assert.ThrowsAsync<InvalidOperationException>(() => pc.ConsumeItemAsync());
		Assert.False(pc.TryProduceItem(1));
		Assert.False(pc.TryConsumeItem(out var _));
	}

	/// <summary>
	/// 验证 EnhancedProducerConsumer_Clear在Dispose后抛但TryProduceItem返回false 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Dispose、Throws、Clear、False、TryProduceItem；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void EnhancedProducerConsumer_Clear在Dispose后抛但TryProduceItem返回false()
	{
		EnhancedProducerConsumer<int> pc = new EnhancedProducerConsumer<int>();
		pc.Dispose();
		Assert.Throws<ObjectDisposedException>(() => pc.Clear());
		Assert.False(pc.TryProduceItem(1));
		Assert.False(pc.TryConsumeItem(out var _));
	}

	/// <summary>
	/// 验证 EnhancedProducerConsumer_StartAsync参数校验与速率限制默认值 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Equal、ThrowsAsync、StartAsync；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task EnhancedProducerConsumer_StartAsync参数校验与速率限制默认值()
	{
		EnhancedProducerConsumer<int> pc = new EnhancedProducerConsumer<int>();
		Assert.Equal(0, pc.ProductionRateLimit);
		Assert.Equal(0, pc.ConsumptionRateLimit);
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => pc.StartAsync(0, 1, (int i) => i, delegate
		{
		}));
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => pc.StartAsync(1, 0, (int i) => i, delegate
		{
		}));
		await Assert.ThrowsAsync<ArgumentNullException>(() => pc.StartAsync(1, 1, null, delegate
		{
		}));
		await Assert.ThrowsAsync<ArgumentNullException>(() => pc.StartAsync(1, 1, (int i) => i, null));
	}

	/// <summary>
	/// 验证 EnhancedProducerConsumer_Dispose会停掉生产 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Dispose、False、TryProduceItem；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void EnhancedProducerConsumer_Dispose会停掉生产()
	{
		EnhancedProducerConsumer<int> enhancedProducerConsumer = new EnhancedProducerConsumer<int>();
		enhancedProducerConsumer.Dispose();
		Assert.False(enhancedProducerConsumer.TryProduceItem(1));
	}

	/// <summary>
	/// 验证 PriorityItem_优先级数字大的排前面 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 True、CompareTo；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void PriorityItem_优先级数字大的排前面()
	{
		PriorityItem<string> priorityItem = new PriorityItem<string>("high", 10);
		PriorityItem<string> priorityItem2 = new PriorityItem<string>("low", 1);
		Assert.True(priorityItem.CompareTo(priorityItem2) < 0);
		Assert.True(priorityItem2.CompareTo(priorityItem) > 0);
	}

	/// <summary>
	/// 验证 PriorityItem_CompareTo对null返回1 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Equal、CompareTo；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void PriorityItem_CompareTo对null返回1()
	{
		Assert.Equal(1, new PriorityItem<string>("a", 1).CompareTo(null));
	}

	/// <summary>
	/// 验证 PriorityItem_同优先级按创建时间先进先出 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Sleep、True、CompareTo；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void PriorityItem_同优先级按创建时间先进先出()
	{
		PriorityItem<string> priorityItem = new PriorityItem<string>("first", 5);
		Thread.Sleep(20);
		PriorityItem<string> priorityItem2 = new PriorityItem<string>("second", 5);
		Assert.True(priorityItem.CompareTo(priorityItem2) < 0);
		Assert.True(priorityItem2.CompareTo(priorityItem) > 0);
	}

	/// <summary>
	/// 验证 PriorityItem_排序之后第一个就是优先级最高的 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Sort、Equal、ToArray、Select；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void PriorityItem_排序之后第一个就是优先级最高的()
	{
		List<PriorityItem<string>> list = new List<PriorityItem<string>>
		{
			new PriorityItem<string>("a", 1),
			new PriorityItem<string>("b", 3),
			new PriorityItem<string>("c", 2)
		};
		list.Sort();
		Assert.Equal(new string[3] { "b", "c", "a" }, list.Select((PriorityItem<string> i) => i.Data).ToArray());
	}

	/// <summary>
	/// 验证 PriorityItem_属性只读 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Equal、True；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void PriorityItem_属性只读()
	{
		PriorityItem<string> priorityItem = new PriorityItem<string>("载荷", 7);
		Assert.Equal("载荷", priorityItem.Data);
		Assert.Equal(7, priorityItem.Priority);
		Assert.True(priorityItem.CreationTime <= DateTime.Now);
	}

	/// <summary>
	/// 验证 BatchItem_Items为null抛ArgumentNullException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Throws；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void BatchItem_Items为null抛ArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => new BatchItem<int>(null));
	}

	/// <summary>
	/// 验证 BatchItem_持有传入的列表引用 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Add、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void BatchItem_持有传入的列表引用()
	{
		List<int> list = new List<int> { 1, 2 };
		BatchItem<int> batchItem = new BatchItem<int>(list);
		list.Add(3);
		Assert.Equal(3, batchItem.Items.Count);
	}

	/// <summary>
	/// 验证 ProducerConsumerConfig_默认值 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Equal、False；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ProducerConsumerConfig_默认值()
	{
		ProducerConsumerConfig producerConsumerConfig = new ProducerConsumerConfig();
		Assert.Equal(100, producerConsumerConfig.Capacity);
		Assert.Equal(BoundedChannelFullMode.Wait, producerConsumerConfig.FullMode);
		Assert.Equal(1, producerConsumerConfig.BatchSize);
		Assert.Equal(1000, producerConsumerConfig.BatchTimeoutMs);
		Assert.False(producerConsumerConfig.EnablePriorityQueue);
		Assert.False(producerConsumerConfig.EnableBatching);
	}

	/// <summary>
	/// 验证 ConfigurableProducerConsumer_默认配置下逐条消费 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 ShortLived、StartAsync、Increment、True；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task ConfigurableProducerConsumer_默认配置下逐条消费()
	{
		ConfigurableProducerConsumer<int> pc = new ConfigurableProducerConsumer<int>();
		int consumed = 0;
		using CancellationTokenSource cts = ShortLived(250);
		await pc.StartAsync(1, 1, (int i) => i, delegate
		{
			Interlocked.Increment(ref consumed);
		}, cts.Token);
		Assert.True(consumed > 0, $"实际消费 {consumed} 条");
	}

	/// <summary>
	/// 验证 ConfigurableProducerConsumer_批处理_批次满时会成批处理 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 ShortLived、StartAsync、Add、True；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
	/// </summary>
	[Fact]
	public async Task ConfigurableProducerConsumer_批处理_批次满时会成批处理()
	{
		ProducerConsumerConfig config = new ProducerConsumerConfig
		{
			Capacity = 60,
			EnableBatching = true,
			BatchSize = 3,
			BatchTimeoutMs = 200
		};
		ConfigurableProducerConsumer<int> pc = new ConfigurableProducerConsumer<int>(config);
		List<int> collected = new List<int>();
		using CancellationTokenSource cts = ShortLived(600);
		await pc.StartAsync(1, 1, (int i) => i, delegate(int item)
		{
			lock (collected)
			{
				collected.Add(item);
			}
		}, cts.Token);
		Assert.True(collected.Count >= 3, $"实际收到 {collected.Count} 条");
	}

	/// <summary>
	/// 验证 ConfigurableProducerConsumer_批处理里的Timer创建后从未使用 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 SourcePath、True、Exists、ReadAllText、Contains；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ConfigurableProducerConsumer_批处理里的Timer创建后从未使用()
	{
		string text = SourcePath("Patterns", "Pipelines", "ProducerConsumer", "Advanced", "AdvancedProducerConsumerPattern.cs");
		Assert.True(File.Exists(text), "找不到源码文件：" + text);
		string actualString = File.ReadAllText(text);
		Assert.Contains("var batchTimer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);", actualString);
		Assert.DoesNotContain("batchTimer.Change", actualString);
	}

	/// <summary>
	/// 验证 ConfigurableProducerConsumer_Stop之后ProduceAsync安静返回不抛 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Stop、ProduceAsync、Equal；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task ConfigurableProducerConsumer_Stop之后ProduceAsync安静返回不抛()
	{
		ConfigurableProducerConsumer<int> pc = new ConfigurableProducerConsumer<int>();
		pc.Stop();
		await pc.ProduceAsync(1, () => 1);
		Assert.Equal(0L, pc.ProducedCount);
	}

	/// <summary>
	/// 验证 ConfigurableProducerConsumer_Stop之后StartAsync抛InvalidOperationException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Stop、ShortLived、Contains、ThrowsAsync、StartAsync；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task ConfigurableProducerConsumer_Stop之后StartAsync抛InvalidOperationException()
	{
		ConfigurableProducerConsumer<int> pc = new ConfigurableProducerConsumer<int>();
		pc.Stop();
		int consumed = 0;
		CancellationTokenSource cts = ShortLived();
		try
		{
			Assert.Contains("已停止", (await Assert.ThrowsAsync<InvalidOperationException>(() => pc.StartAsync(1, 1, (int i) => i, delegate
			{
				Interlocked.Increment(ref consumed);
			}, cts.Token))).Message);
			Assert.Equal(0, consumed);
			Assert.Equal(0L, pc.ProducedCount);
		}
		finally
		{
			if (cts != null)
			{
				((IDisposable)cts).Dispose();
			}
		}
	}

	/// <summary>
	/// 验证 ConfigurableProducerConsumer_Dispose之后StartAsync抛ObjectDisposedException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Dispose、ShortLived、ThrowsAsync、StartAsync；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task ConfigurableProducerConsumer_Dispose之后StartAsync抛ObjectDisposedException()
	{
		ConfigurableProducerConsumer<int> pc = new ConfigurableProducerConsumer<int>();
		pc.Dispose();
		CancellationTokenSource cts = ShortLived();
		try
		{
			await Assert.ThrowsAsync<ObjectDisposedException>(() => pc.StartAsync(1, 1, (int i) => i, delegate
			{
			}, cts.Token));
		}
		finally
		{
			if (cts != null)
			{
				((IDisposable)cts).Dispose();
			}
		}
	}

	/// <summary>
	/// 验证 ConfigurableProducerConsumer_StartAsync参数校验 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 ThrowsAsync、StartAsync；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task ConfigurableProducerConsumer_StartAsync参数校验()
	{
		ConfigurableProducerConsumer<int> pc = new ConfigurableProducerConsumer<int>();
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => pc.StartAsync(0, 1, (int i) => i, delegate
		{
		}));
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => pc.StartAsync(1, 0, (int i) => i, delegate
		{
		}));
		await Assert.ThrowsAsync<ArgumentNullException>(() => pc.StartAsync(1, 1, null, delegate
		{
		}));
		await Assert.ThrowsAsync<ArgumentNullException>(() => pc.StartAsync(1, 1, (int i) => i, null));
	}

	/// <summary>
	/// 验证 ConfigurableProducerConsumer_构造时传null配置会用默认配置 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ConfigurableProducerConsumer_构造时传null配置会用默认配置()
	{
		ConfigurableProducerConsumer<int> configurableProducerConsumer = new ConfigurableProducerConsumer<int>();
		Assert.Equal(0L, configurableProducerConsumer.ProducedCount);
		Assert.Equal(0L, configurableProducerConsumer.ConsumedCount);
	}

	/// <summary>
	/// 验证 ConfigurableProducerConsumer_Stop与Dispose都是幂等的 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Stop、Dispose；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ConfigurableProducerConsumer_Stop与Dispose都是幂等的()
	{
		ConfigurableProducerConsumer<int> configurableProducerConsumer = new ConfigurableProducerConsumer<int>();
		configurableProducerConsumer.Stop();
		configurableProducerConsumer.Stop();
		configurableProducerConsumer.Dispose();
		configurableProducerConsumer.Dispose();
	}

	/// <summary>
	/// 验证 PriorityQueueProducerConsumer_StartAsync必抛NotImplementedException且是同步抛 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 StartAsync、Throws、Contains、GetMethod、NotNull；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void PriorityQueueProducerConsumer_StartAsync必抛NotImplementedException且是同步抛()
	{
		PriorityQueueProducerConsumer<int> pc = new PriorityQueueProducerConsumer<int>();
		Action testCode = delegate
		{
			pc.StartAsync(1, 1, (int i) => i, delegate
			{
			});
		};
		NotImplementedException ex = Assert.Throws<NotImplementedException>(testCode);
		Assert.Contains("StartConsumersAsync", ex.Message);
		MethodInfo method = typeof(PriorityQueueProducerConsumer<int>).GetMethod("StartAsync");
		Assert.NotNull(method);
		Assert.Equal<Type>(typeof(Task), method.ReturnType);
		Assert.False(method.IsDefined(typeof(AsyncStateMachineAttribute), inherit: false), "StartAsync 变成了 async 方法 ⇒ 异常会被包进 Task，调用方语义随之改变");
	}

	/// <summary>
	/// 验证 PriorityQueueProducerConsumer_按优先级从高到低消费 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Produce、ShortLived、ConsumeAsync、Add、Equal；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
	/// </summary>
	[Fact]
	public async Task PriorityQueueProducerConsumer_按优先级从高到低消费()
	{
		PriorityQueueProducerConsumer<string> pc = new PriorityQueueProducerConsumer<string>();
		pc.Produce("低", 1);
		pc.Produce("高", 10);
		pc.Produce("中", 5);
		List<string> consumed = new List<string>();
		using CancellationTokenSource cts = ShortLived(500);
		await pc.ConsumeAsync(delegate(string item)
		{
			lock (consumed)
			{
				consumed.Add(item);
			}
		}, cts.Token);
		Assert.Equal(new string[3] { "高", "中", "低" }, consumed);
		Assert.Equal(3L, pc.ConsumedCount);
		Assert.Equal(3L, pc.ProducedCount);
	}

	/// <summary>
	/// 验证 PriorityQueueProducerConsumer_Stop之后Produce抛InvalidOperationException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Stop、Throws、Produce；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void PriorityQueueProducerConsumer_Stop之后Produce抛InvalidOperationException()
	{
		PriorityQueueProducerConsumer<int> pc = new PriorityQueueProducerConsumer<int>();
		pc.Stop();
		Assert.Throws<InvalidOperationException>(delegate
		{
			pc.Produce(1, 1);
		});
	}

	/// <summary>
	/// 验证 PriorityQueueProducerConsumer_Stop是幂等的 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Stop；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void PriorityQueueProducerConsumer_Stop是幂等的()
	{
		PriorityQueueProducerConsumer<int> priorityQueueProducerConsumer = new PriorityQueueProducerConsumer<int>();
		priorityQueueProducerConsumer.Stop();
		priorityQueueProducerConsumer.Stop();
	}

	/// <summary>
	/// 验证 PriorityQueueProducerConsumer_Dispose之后Stop是空操作 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Dispose、Stop；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void PriorityQueueProducerConsumer_Dispose之后Stop是空操作()
	{
		PriorityQueueProducerConsumer<int> priorityQueueProducerConsumer = new PriorityQueueProducerConsumer<int>();
		priorityQueueProducerConsumer.Dispose();
		priorityQueueProducerConsumer.Stop();
		priorityQueueProducerConsumer.Dispose();
	}

	/// <summary>
	/// 验证 PriorityQueueProducerConsumer_StopAsync会真的停掉 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 StopAsync、Throws、Produce；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task PriorityQueueProducerConsumer_StopAsync会真的停掉()
	{
		PriorityQueueProducerConsumer<int> pc = new PriorityQueueProducerConsumer<int>();
		await pc.StopAsync();
		Assert.Throws<InvalidOperationException>(delegate
		{
			pc.Produce(1, 1);
		});
	}

	/// <summary>
	/// 验证 PriorityQueueProducerConsumer_StartConsumersAsync参数校验 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 ThrowsAsync、StartConsumersAsync；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task PriorityQueueProducerConsumer_StartConsumersAsync参数校验()
	{
		PriorityQueueProducerConsumer<int> pc = new PriorityQueueProducerConsumer<int>();
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => pc.StartConsumersAsync(0, delegate
		{
		}));
		await Assert.ThrowsAsync<ArgumentNullException>(() => pc.StartConsumersAsync(1, null));
	}

	/// <summary>
	/// 验证 PriorityQueueProducerConsumer_Stop会让消费者从等待中退出 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Run、Set、ConsumeAsync、True、Wait；具体条件和先后次序见方法体。
	/// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
	/// </summary>
	[Fact]
	public async Task PriorityQueueProducerConsumer_Stop会让消费者从等待中退出()
	{
		PriorityQueueProducerConsumer<int> pc = new PriorityQueueProducerConsumer<int>();
		ManualResetEventSlim started = new ManualResetEventSlim(initialState: false);
		ManualResetEventSlim finished = new ManualResetEventSlim(initialState: false);
		Task consumer = Task.Run(async delegate
		{
			started.Set();
			await pc.ConsumeAsync(delegate
			{
			});
			finished.Set();
		});
		Assert.True(started.Wait(TimeSpan.FromSeconds(5L)));
		await Task.Delay(50);
		pc.Stop();
		Assert.True(finished.Wait(TimeSpan.FromSeconds(5L)), "Stop 之后消费者没有退出");
		await consumer;
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
