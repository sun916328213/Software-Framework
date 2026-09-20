using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Extension.Patterns.Messaging.PublishSubscribe;
using Extension.Patterns.Messaging.PublishSubscribe.Advanced;
using Xunit;

namespace Extension.Tests;

public class PatternsPublishSubscribeTests
{
	private sealed class ProbeSubscriber : SubscriberBase<string>
	{
		public List<string> Received { get; } = new List<string>();

		public int DisposeCount { get; private set; }

		public ProbeSubscriber(string name)
			: base(name)
		{
		}

		public override void HandleMessage(string message)
		{
			Received.Add(message);
		}

		protected override void Dispose(bool disposing)
		{
			DisposeCount++;
			base.Dispose(disposing);
		}
	}

	private sealed class ThrowingSubscriber : ISubscriber<string>
	{
		public void HandleMessage(string message)
		{
			throw new InvalidOperationException("订阅者炸了");
		}
	}

	[Fact]
	public void SubscriberBase_Name为null抛ArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => new ProbeSubscriber(null));
		Assert.Equal("s", new ProbeSubscriber("s").Name);
	}

	[Fact]
	public void SubscriberBase_Dispose之后HandleMessage仍可调用_没有_disposed检查()
	{
		ProbeSubscriber probeSubscriber = new ProbeSubscriber("s");
		probeSubscriber.Dispose();
		probeSubscriber.HandleMessage("Dispose 之后仍然收到");
		Assert.Single(probeSubscriber.Received);
	}

	[Fact]
	public void DelegateSubscriber_Dispose之后HandleMessage抛ObjectDisposedException()
	{
		DelegateSubscriber<string> subscriber = new DelegateSubscriber<string>("d", delegate
		{
		});
		subscriber.Dispose();
		Assert.Throws<ObjectDisposedException>(delegate
		{
			subscriber.HandleMessage("m");
		});
	}

	[Fact]
	public void DelegateSubscriber_构造函数参数校验与Dispose幂等()
	{
		Assert.Throws<ArgumentNullException>(() => new DelegateSubscriber<string>(null, delegate
		{
		}));
		Assert.Throws<ArgumentNullException>(() => new DelegateSubscriber<string>("d", null));
		DelegateSubscriber<string> delegateSubscriber = new DelegateSubscriber<string>("d", delegate
		{
		});
		delegateSubscriber.Dispose();
		delegateSubscriber.Dispose();
	}

	[Fact]
	public void MessageBroker_Subscribe去重_SubscriberCount()
	{
		MessageBroker<string> messageBroker = new MessageBroker<string>();
		ProbeSubscriber subscriber = new ProbeSubscriber("s");
		messageBroker.Subscribe(subscriber);
		messageBroker.Subscribe(subscriber);
		Assert.Equal(1, messageBroker.SubscriberCount);
	}

	[Fact]
	public void MessageBroker_Publish送达所有订阅者()
	{
		MessageBroker<string> messageBroker = new MessageBroker<string>();
		ProbeSubscriber probeSubscriber = new ProbeSubscriber("a");
		ProbeSubscriber probeSubscriber2 = new ProbeSubscriber("b");
		messageBroker.Subscribe(probeSubscriber);
		messageBroker.Subscribe(probeSubscriber2);
		messageBroker.Publish("广播");
		Assert.Equal(new string[1] { "广播" }, probeSubscriber.Received.ToArray());
		Assert.Equal(new string[1] { "广播" }, probeSubscriber2.Received.ToArray());
	}

	[Fact]
	public void MessageBroker_一个订阅者抛异常不影响其他订阅者()
	{
		MessageBroker<string> messageBroker = new MessageBroker<string>();
		ProbeSubscriber probeSubscriber = new ProbeSubscriber("good");
		messageBroker.Subscribe(new ThrowingSubscriber());
		messageBroker.Subscribe(probeSubscriber);
		messageBroker.Publish("广播");
		Assert.Single(probeSubscriber.Received);
	}

	[Fact]
	public void MessageBroker_Unsubscribe后不再收到()
	{
		MessageBroker<string> messageBroker = new MessageBroker<string>();
		ProbeSubscriber probeSubscriber = new ProbeSubscriber("s");
		messageBroker.Subscribe(probeSubscriber);
		messageBroker.Unsubscribe(probeSubscriber);
		messageBroker.Publish("广播");
		Assert.Empty(probeSubscriber.Received);
		Assert.Equal(0, messageBroker.SubscriberCount);
	}

	[Fact]
	public void MessageBroker_ClearSubscribers会清空名单()
	{
		MessageBroker<string> messageBroker = new MessageBroker<string>();
		ProbeSubscriber probeSubscriber = new ProbeSubscriber("s");
		messageBroker.Subscribe(probeSubscriber);
		messageBroker.ClearSubscribers();
		messageBroker.Publish("广播");
		Assert.Equal(0, messageBroker.SubscriberCount);
		Assert.Empty(probeSubscriber.Received);
	}

	[Fact]
	public void MessageBroker_Dispose会清空订阅者()
	{
		MessageBroker<string> messageBroker = new MessageBroker<string>();
		messageBroker.Subscribe(new ProbeSubscriber("s"));
		messageBroker.Dispose();
		Assert.Equal(0, messageBroker.SubscriberCount);
	}

	[Fact]
	public async Task MessageBroker_Dispose之后Publish静默返回但Subscribe抛()
	{
		MessageBroker<string> broker = new MessageBroker<string>();
		ProbeSubscriber subscriber = new ProbeSubscriber("s");
		broker.Dispose();
		Assert.Throws<ObjectDisposedException>(delegate
		{
			broker.Subscribe(subscriber);
		});
		Assert.Throws<ObjectDisposedException>(delegate
		{
			broker.Unsubscribe(subscriber);
		});
		Assert.Throws<ObjectDisposedException>(delegate
		{
			broker.ClearSubscribers();
		});
		broker.Publish("广播");
		await broker.PublishAsync("广播");
		Assert.Empty(subscriber.Received);
	}

	[Fact]
	public void MessageBroker_批量订阅与取消会跳过null()
	{
		MessageBroker<string> messageBroker = new MessageBroker<string>();
		ProbeSubscriber probeSubscriber = new ProbeSubscriber("a");
		ProbeSubscriber probeSubscriber2 = new ProbeSubscriber("b");
		messageBroker.SubscribeRange(new ISubscriber<string>[4] { probeSubscriber, probeSubscriber2, null, probeSubscriber });
		Assert.Equal(2, messageBroker.SubscriberCount);
		messageBroker.UnsubscribeRange(new ISubscriber<string>[2] { probeSubscriber, null });
		Assert.Equal(1, messageBroker.SubscriberCount);
	}

	[Fact]
	public void MessageBroker_参数校验()
	{
		MessageBroker<string> broker = new MessageBroker<string>();
		Assert.Throws<ArgumentNullException>(delegate
		{
			broker.Subscribe(null);
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			broker.Unsubscribe(null);
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			broker.SubscribeRange(null);
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			broker.UnsubscribeRange(null);
		});
	}

	[Fact]
	public async Task MessageBroker_PublishAsync会真的送达()
	{
		MessageBroker<string> broker = new MessageBroker<string>();
		ProbeSubscriber subscriber = new ProbeSubscriber("s");
		broker.Subscribe(subscriber);
		await broker.PublishAsync("异步广播");
		Assert.Single(subscriber.Received);
	}

	[Fact]
	public void AdvancedMessageBroker_主题订阅者不会收到Publish()
	{
		Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string> advancedMessageBroker = new Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string>();
		ProbeSubscriber probeSubscriber = new ProbeSubscriber("global");
		ProbeSubscriber probeSubscriber2 = new ProbeSubscriber("topic");
		advancedMessageBroker.Subscribe(probeSubscriber);
		advancedMessageBroker.SubscribeToTopic("t", probeSubscriber2);
		advancedMessageBroker.Publish("全局广播");
		Assert.Single(probeSubscriber.Received);
		Assert.Empty(probeSubscriber2.Received);
		advancedMessageBroker.PublishToTopic("t", "定向消息");
		Assert.Single(probeSubscriber2.Received);
		Assert.Single(probeSubscriber.Received);
	}

	[Fact]
	public void AdvancedMessageBroker_PublishToTopic只发给该主题的订阅者()
	{
		Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string> advancedMessageBroker = new Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string>();
		ProbeSubscriber probeSubscriber = new ProbeSubscriber("onT");
		ProbeSubscriber probeSubscriber2 = new ProbeSubscriber("onU");
		advancedMessageBroker.SubscribeToTopic("t", probeSubscriber);
		advancedMessageBroker.SubscribeToTopic("u", probeSubscriber2);
		advancedMessageBroker.PublishToTopic("t", "给t");
		Assert.Single(probeSubscriber.Received);
		Assert.Empty(probeSubscriber2.Received);
	}

	[Fact]
	public void AdvancedMessageBroker_SubscriberCount只算全局订阅者()
	{
		Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string> advancedMessageBroker = new Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string>();
		advancedMessageBroker.SubscribeToTopic("t", new ProbeSubscriber("topic"));
		Assert.Equal(0, advancedMessageBroker.SubscriberCount);
		advancedMessageBroker.Subscribe(new ProbeSubscriber("global"));
		Assert.Equal(1, advancedMessageBroker.SubscriberCount);
	}

	[Fact]
	public void AdvancedMessageBroker_Unsubscribe会同时从所有主题移除()
	{
		Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string> advancedMessageBroker = new Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string>();
		ProbeSubscriber probeSubscriber = new ProbeSubscriber("s");
		advancedMessageBroker.Subscribe(probeSubscriber);
		advancedMessageBroker.SubscribeToTopic("t", probeSubscriber);
		advancedMessageBroker.SubscribeToTopic("u", probeSubscriber);
		advancedMessageBroker.Unsubscribe(probeSubscriber);
		advancedMessageBroker.Publish("全局");
		advancedMessageBroker.PublishToTopic("t", "给t");
		advancedMessageBroker.PublishToTopic("u", "给u");
		Assert.Empty(probeSubscriber.Received);
		Assert.Equal(0, advancedMessageBroker.SubscriberCount);
	}

	[Fact]
	public void AdvancedMessageBroker_UnsubscribeFromTopic只影响该主题()
	{
		Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string> advancedMessageBroker = new Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string>();
		ProbeSubscriber probeSubscriber = new ProbeSubscriber("s");
		advancedMessageBroker.SubscribeToTopic("t", probeSubscriber);
		advancedMessageBroker.SubscribeToTopic("u", probeSubscriber);
		advancedMessageBroker.UnsubscribeFromTopic("t", probeSubscriber);
		advancedMessageBroker.PublishToTopic("t", "给t");
		Assert.Empty(probeSubscriber.Received);
		advancedMessageBroker.PublishToTopic("u", "给u");
		Assert.Single(probeSubscriber.Received);
	}

	[Fact]
	public void AdvancedMessageBroker_ClearSubscribers会连主题名单一起清空()
	{
		Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string> advancedMessageBroker = new Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string>();
		ProbeSubscriber probeSubscriber = new ProbeSubscriber("global");
		ProbeSubscriber probeSubscriber2 = new ProbeSubscriber("topic");
		advancedMessageBroker.Subscribe(probeSubscriber);
		advancedMessageBroker.SubscribeToTopic("t", probeSubscriber2);
		advancedMessageBroker.ClearSubscribers();
		advancedMessageBroker.Publish("全局");
		advancedMessageBroker.PublishToTopic("t", "给t");
		Assert.Empty(probeSubscriber.Received);
		Assert.Empty(probeSubscriber2.Received);
		Assert.Equal(0, advancedMessageBroker.SubscriberCount);
	}

	[Fact]
	public void AdvancedMessageBroker_参数校验()
	{
		Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string> broker = new Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string>();
		Assert.Throws<ArgumentNullException>(delegate
		{
			broker.Subscribe(null);
		});
		Assert.Throws<ArgumentException>(delegate
		{
			broker.SubscribeToTopic("", new ProbeSubscriber("s"));
		});
		Assert.Throws<ArgumentException>(delegate
		{
			broker.SubscribeToTopic(null, new ProbeSubscriber("s"));
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			broker.SubscribeToTopic("t", null);
		});
		Assert.Throws<ArgumentException>(delegate
		{
			broker.UnsubscribeFromTopic("", new ProbeSubscriber("s"));
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			broker.SubscribeRange(null);
		});
	}

	[Fact]
	public async Task AdvancedMessageBroker_Dispose之后Publish静默返回但Subscribe抛()
	{
		Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string> broker = new Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string>();
		ProbeSubscriber subscriber = new ProbeSubscriber("s");
		broker.SubscribeToTopic("t", subscriber);
		broker.Dispose();
		Assert.Throws<ObjectDisposedException>(delegate
		{
			broker.Subscribe(new ProbeSubscriber("x"));
		});
		Assert.Throws<ObjectDisposedException>(delegate
		{
			broker.ClearSubscribers();
		});
		broker.Publish("全局");
		broker.PublishToTopic("t", "给t");
		await broker.PublishAsync("全局");
		await broker.PublishToTopicAsync("t", "给t");
		Assert.Empty(subscriber.Received);
	}

	[Fact]
	public async Task AdvancedMessageBroker_PublishToTopicAsync会真的送达()
	{
		Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string> broker = new Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string>();
		ProbeSubscriber topic = new ProbeSubscriber("topic");
		broker.SubscribeToTopic("t", topic);
		await broker.PublishToTopicAsync("t", "给t");
		Assert.Single(topic.Received);
	}

	[Fact]
	public void TopicFilter_空Topic的语义是空白名单才放行()
	{
		Assert.True(new TopicFilter<string>(new string[0]).ShouldProcess(new PriorityMessage<string>("d", MessagePriority.Low)));
		Assert.False(new TopicFilter<string>(new string[1] { "t" }).ShouldProcess(new PriorityMessage<string>("d", MessagePriority.Low)));
		Assert.True(new TopicFilter<string>(new string[1] { "" }).ShouldProcess(new PriorityMessage<string>("d", MessagePriority.Low)));
		Assert.True(new TopicFilter<string>(new string[1] { "t" }).ShouldProcess(new PriorityMessage<string>("d", MessagePriority.Low, "t")));
		Assert.False(new TopicFilter<string>(new string[1] { "t" }).ShouldProcess(new PriorityMessage<string>("d", MessagePriority.Low, "u")));
	}

	[Fact]
	public void TopicFilter_构造函数对null抛ArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => new TopicFilter<string>(null));
	}

	[Fact]
	public void PriorityFilter_T_按最低优先级过滤()
	{
		PriorityFilter<string> priorityFilter = new PriorityFilter<string>(MessagePriority.High);
		Assert.False(priorityFilter.ShouldProcess(new PriorityMessage<string>("d", MessagePriority.Normal)));
		Assert.True(priorityFilter.ShouldProcess(new PriorityMessage<string>("d", MessagePriority.High)));
		Assert.True(priorityFilter.ShouldProcess(new PriorityMessage<string>("d", MessagePriority.Critical)));
	}

	[Fact]
	public void CompositeFilter_T_空列表一律通过()
	{
		CompositeFilter<string> compositeFilter = new CompositeFilter<string>(new IMessageFilter<string>[0]);
		Assert.True(compositeFilter.ShouldProcess("任意"));
	}

	[Fact]
	public void CompositeFilter_T_必须全部通过才通过()
	{
		CompositeFilter<PriorityMessage<string>> compositeFilter = new CompositeFilter<PriorityMessage<string>>(new IMessageFilter<PriorityMessage<string>>[2]
		{
			new TopicFilter<string>(new string[1] { "t" }),
			new PriorityFilter<string>(MessagePriority.High)
		});
		Assert.True(compositeFilter.ShouldProcess(new PriorityMessage<string>("d", MessagePriority.High, "t")));
		Assert.False(compositeFilter.ShouldProcess(new PriorityMessage<string>("d", MessagePriority.Low, "t")));
		Assert.False(compositeFilter.ShouldProcess(new PriorityMessage<string>("d", MessagePriority.High, "u")));
	}

	[Fact]
	public void CompositeFilter_T_构造函数对null抛ArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => new CompositeFilter<string>(null));
	}

	[Fact]
	public void FilteredSubscriber_过滤通过才转发()
	{
		List<PriorityMessage<string>> received = new List<PriorityMessage<string>>();
		DelegateSubscriber<PriorityMessage<string>> innerSubscriber = new DelegateSubscriber<PriorityMessage<string>>("inner", delegate(PriorityMessage<string> m)
		{
			received.Add(m);
		});
		PriorityFilter<string> filter = new PriorityFilter<string>(MessagePriority.High);
		FilteredSubscriber<PriorityMessage<string>> filteredSubscriber = new FilteredSubscriber<PriorityMessage<string>>(innerSubscriber, filter);
		filteredSubscriber.HandleMessage(new PriorityMessage<string>("低", MessagePriority.Low));
		Assert.Empty(received);
		filteredSubscriber.HandleMessage(new PriorityMessage<string>("高", MessagePriority.High));
		Assert.Single(received);
	}

	[Fact]
	public void FilteredSubscriber_Dispose会连带Dispose内层()
	{
		ProbeSubscriber probeSubscriber = new ProbeSubscriber("inner");
		FilteredSubscriber<string> filteredSubscriber = new FilteredSubscriber<string>(probeSubscriber, new CompositeFilter<string>(new IMessageFilter<string>[0]));
		filteredSubscriber.Dispose();
		Assert.Equal(1, probeSubscriber.DisposeCount);
		Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(ISubscriber<string>)));
		Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(SubscriberBase<string>)));
		Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(DelegateSubscriber<string>)));
		filteredSubscriber.Dispose();
		Assert.Equal(1, probeSubscriber.DisposeCount);
	}

	[Fact]
	public void FilteredSubscriber_Dispose会连带Dispose内层的DelegateSubscriber()
	{
		DelegateSubscriber<string> inner = new DelegateSubscriber<string>("委托名", delegate
		{
		});
		FilteredSubscriber<string> filteredSubscriber = new FilteredSubscriber<string>(inner, new CompositeFilter<string>(new IMessageFilter<string>[0]));
		inner.HandleMessage("释放前还能用");
		filteredSubscriber.Dispose();
		Assert.Throws<ObjectDisposedException>(delegate
		{
			inner.HandleMessage("释放后必须抛");
		});
	}

	[Fact]
	public void FilteredSubscriber_Name会带上Filtered前缀_内层两种实现都能分辨()
	{
		CompositeFilter<string> filter = new CompositeFilter<string>(new IMessageFilter<string>[0]);
		FilteredSubscriber<string> filteredSubscriber = new FilteredSubscriber<string>(new ProbeSubscriber("真实名字"), filter);
		Assert.Equal("Filtered(真实名字)", filteredSubscriber.Name);
		FilteredSubscriber<string> filteredSubscriber2 = new FilteredSubscriber<string>(new DelegateSubscriber<string>("委托名", delegate
		{
		}), filter);
		Assert.Equal("Filtered(委托名)", filteredSubscriber2.Name);
		Assert.NotEqual(filteredSubscriber.Name, filteredSubscriber2.Name);
		FilteredSubscriber<string> filteredSubscriber3 = new FilteredSubscriber<string>(new ThrowingSubscriber(), filter);
		Assert.Equal("Filtered(ThrowingSubscriber)", filteredSubscriber3.Name);
	}

	[Fact]
	public void FilteredSubscriber_Dispose之后HandleMessage抛ObjectDisposedException()
	{
		FilteredSubscriber<string> subscriber = new FilteredSubscriber<string>(new ProbeSubscriber("inner"), new CompositeFilter<string>(new IMessageFilter<string>[0]));
		subscriber.Dispose();
		Assert.Throws<ObjectDisposedException>(delegate
		{
			subscriber.HandleMessage("m");
		});
	}

	[Fact]
	public void FilteredSubscriber_构造函数参数校验()
	{
		CompositeFilter<string> empty = new CompositeFilter<string>(new IMessageFilter<string>[0]);
		Assert.Throws<ArgumentNullException>(() => new FilteredSubscriber<string>(null, empty));
		Assert.Throws<ArgumentNullException>(() => new FilteredSubscriber<string>(new ProbeSubscriber("i"), null));
	}

	[Fact]
	public void Advanced_AdvancedMessageBroker_投递顺序不可由优先级决定()
	{
		Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string> advancedMessageBroker = new Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string>();
		List<string> seen = new List<string>();
		for (int i = 0; i < 3; i++)
		{
			string tag = "s" + i;
			advancedMessageBroker.Subscribe(new DelegateSubscriber<PriorityMessage<string>>(tag, delegate
			{
				seen.Add(tag);
			}));
		}
		advancedMessageBroker.Publish(new PriorityMessage<string>("载荷", MessagePriority.Critical));
		Assert.Equal(3, seen.Count);
		Assert.Equal(new string[3] { "s0", "s1", "s2" }, seen.OrderBy((string x) => x).ToArray());
	}

	[Fact]
	public void Advanced_AdvancedMessageBroker_的排序键与订阅者无关()
	{
		string text = SourcePath("Patterns", "Messaging", "PublishSubscribe", "Advanced", "AdvancedPublishSubscribePattern.cs");
		Assert.True(File.Exists(text), "找不到源码文件：" + text);
		string actualString = File.ReadAllText(text);
		Assert.Contains("private int GetSubscriberPriority(ISubscriber<PriorityMessage<T>> subscriber, PriorityMessage<T> message)", actualString);
		Assert.Contains("return -(int)message.Priority;", actualString);
	}

	[Fact]
	public void Advanced_AdvancedMessageBroker_订阅与主题发布()
	{
		Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string> advancedMessageBroker = new Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string>();
		int globalCount = 0;
		int topicCount = 0;
		advancedMessageBroker.Subscribe(new DelegateSubscriber<PriorityMessage<string>>("g", delegate
		{
			globalCount++;
		}));
		advancedMessageBroker.SubscribeToTopic("t", new DelegateSubscriber<PriorityMessage<string>>("t", delegate
		{
			topicCount++;
		}));
		advancedMessageBroker.Publish(new PriorityMessage<string>("全局", MessagePriority.High));
		advancedMessageBroker.PublishToTopic("t", new PriorityMessage<string>("定向", MessagePriority.High));
		Assert.Equal(1, globalCount);
		Assert.Equal(1, topicCount);
	}

	[Fact]
	public void Advanced_AdvancedMessageBroker_SubscriberCount只算全局订阅者()
	{
		Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string> advancedMessageBroker = new Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string>();
		advancedMessageBroker.SubscribeToTopic("t", new DelegateSubscriber<PriorityMessage<string>>("t", delegate
		{
		}));
		Assert.Equal(0, advancedMessageBroker.SubscriberCount);
		advancedMessageBroker.Subscribe(new DelegateSubscriber<PriorityMessage<string>>("g", delegate
		{
		}));
		Assert.Equal(1, advancedMessageBroker.SubscriberCount);
	}

	[Fact]
	public void 两个同名AdvancedMessageBroker只能靠命名空间区分()
	{
		Assert.Equal("AdvancedMessageBroker`1", typeof(Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<>).Name);
		Assert.Equal("AdvancedMessageBroker`1", typeof(Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<>).Name);
		Assert.Equal("Extension.Patterns.Messaging.PublishSubscribe", typeof(Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<>).Namespace);
		Assert.Equal("Extension.Patterns.Messaging.PublishSubscribe.Advanced", typeof(Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<>).Namespace);
		Assert.NotEqual(typeof(Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<>).FullName, typeof(Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<>).FullName);
		Assert.True(typeof(IMessageBroker<string>).IsAssignableFrom(typeof(Extension.Patterns.Messaging.PublishSubscribe.AdvancedMessageBroker<string>)));
		Assert.True(typeof(IMessageBroker<PriorityMessage<string>>).IsAssignableFrom(typeof(Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string>)));
	}

	[Fact]
	public void 两个同名MessagePriority枚举都还在()
	{
		Assert.Equal(0, 0);
		Assert.Equal(3, 3);
		Assert.Equal("MessagePriority", typeof(MessagePriority).Name);
		Assert.Equal("Extension.Patterns.Messaging.PublishSubscribe.Advanced", typeof(MessagePriority).Namespace);
	}

	[Fact]
	public async Task Advanced_AdvancedMessageBroker_Dispose之后Publish静默返回但Subscribe抛()
	{
		Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string> broker = new Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string>();
		int count = 0;
		broker.Subscribe(new DelegateSubscriber<PriorityMessage<string>>("g", delegate
		{
			count++;
		}));
		broker.Dispose();
		Assert.Throws<ObjectDisposedException>(delegate
		{
			broker.Subscribe(new DelegateSubscriber<PriorityMessage<string>>("x", delegate
			{
			}));
		});
		Assert.Throws<ObjectDisposedException>(delegate
		{
			broker.ClearSubscribers();
		});
		broker.Publish(new PriorityMessage<string>("全局", MessagePriority.High));
		broker.PublishToTopic("t", new PriorityMessage<string>("定向", MessagePriority.High));
		await broker.PublishAsync(new PriorityMessage<string>("全局", MessagePriority.High));
		await broker.PublishToTopicAsync("t", new PriorityMessage<string>("定向", MessagePriority.High));
		Assert.Equal(0, count);
	}

	[Fact]
	public void Advanced_AdvancedMessageBroker_参数校验()
	{
		Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string> broker = new Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string>();
		Assert.Throws<ArgumentNullException>(delegate
		{
			broker.Subscribe(null);
		});
		Assert.Throws<ArgumentException>(delegate
		{
			broker.SubscribeToTopic("", new DelegateSubscriber<PriorityMessage<string>>("s", delegate
			{
			}));
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			broker.SubscribeToTopic("t", null);
		});
		Assert.Throws<ArgumentException>(delegate
		{
			broker.PublishToTopic("", new PriorityMessage<string>("d", MessagePriority.Low));
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			broker.SubscribeRange(null);
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			broker.UnsubscribeRange(null);
		});
	}

	[Fact]
	public void MessageRouter_T_路由到已注册的broker()
	{
		Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string> advancedMessageBroker = new Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string>();
		int count = 0;
		advancedMessageBroker.Subscribe(new DelegateSubscriber<PriorityMessage<string>>("g", delegate
		{
			count++;
		}));
		MessageRouter<string> messageRouter = new MessageRouter<string>((PriorityMessage<string> m) => m.Topic);
		messageRouter.RegisterChannel("chan", advancedMessageBroker);
		messageRouter.RouteAndPublish(new PriorityMessage<string>("载荷", MessagePriority.High, "chan"));
		Assert.Equal(1, count);
	}

	[Fact]
	public void MessageRouter_T_通道未注册或路由结果为空时静默丢弃()
	{
		Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string> advancedMessageBroker = new Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string>();
		int count = 0;
		advancedMessageBroker.Subscribe(new DelegateSubscriber<PriorityMessage<string>>("g", delegate
		{
			count++;
		}));
		MessageRouter<string> messageRouter = new MessageRouter<string>((PriorityMessage<string> m) => m.Topic);
		messageRouter.RegisterChannel("chan", advancedMessageBroker);
		messageRouter.RouteAndPublish(new PriorityMessage<string>("载荷", MessagePriority.High, "未注册"));
		messageRouter.RouteAndPublish(new PriorityMessage<string>("载荷", MessagePriority.High));
		Assert.Equal(0, count);
	}

	[Fact]
	public void MessageRouter_T_参数校验与重复通道覆盖()
	{
		Assert.Throws<ArgumentNullException>(() => new MessageRouter<string>(null));
		MessageRouter<string> router = new MessageRouter<string>((PriorityMessage<string> m) => "chan");
		Assert.Throws<ArgumentException>(delegate
		{
			router.RegisterChannel("", new Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string>());
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			router.RegisterChannel("chan", null);
		});
		Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string> broker = new Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string>();
		Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string> advancedMessageBroker = new Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string>();
		int secondCount = 0;
		advancedMessageBroker.Subscribe(new DelegateSubscriber<PriorityMessage<string>>("g", delegate
		{
			secondCount++;
		}));
		router.RegisterChannel("chan", broker);
		router.RegisterChannel("chan", advancedMessageBroker);
		router.RouteAndPublish(new PriorityMessage<string>("载荷", MessagePriority.High, "chan"));
		Assert.Equal(1, secondCount);
	}

	[Fact]
	public void MessageRouter_T_Dispose会Dispose所有broker()
	{
		Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string> advancedMessageBroker = new Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string>();
		advancedMessageBroker.Subscribe(new DelegateSubscriber<PriorityMessage<string>>("g", delegate
		{
		}));
		MessageRouter<string> router = new MessageRouter<string>((PriorityMessage<string> m) => "chan");
		router.RegisterChannel("chan", advancedMessageBroker);
		router.Dispose();
		Assert.Equal(0, advancedMessageBroker.SubscriberCount);
		Assert.Throws<ObjectDisposedException>(delegate
		{
			router.RegisterChannel("chan2", new Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string>());
		});
	}

	[Fact]
	public async Task MessageRouter_T_Dispose之后路由静默返回()
	{
		Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string> broker = new Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string>();
		int count = 0;
		broker.Subscribe(new DelegateSubscriber<PriorityMessage<string>>("g", delegate
		{
			count++;
		}));
		MessageRouter<string> router = new MessageRouter<string>((PriorityMessage<string> m) => "chan");
		router.RegisterChannel("chan", broker);
		router.Dispose();
		router.RouteAndPublish(new PriorityMessage<string>("载荷", MessagePriority.High, "chan"));
		await router.RouteAndPublishAsync(new PriorityMessage<string>("载荷", MessagePriority.High, "chan"));
		Assert.Equal(0, count);
	}

	[Fact]
	public async Task MessageRouter_T_异步路由会真的送达()
	{
		Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string> broker = new Extension.Patterns.Messaging.PublishSubscribe.Advanced.AdvancedMessageBroker<string>();
		int count = 0;
		broker.Subscribe(new DelegateSubscriber<PriorityMessage<string>>("g", delegate
		{
			count++;
		}));
		MessageRouter<string> router = new MessageRouter<string>((PriorityMessage<string> m) => "chan");
		router.RegisterChannel("chan", broker);
		await router.RouteAndPublishAsync(new PriorityMessage<string>("载荷", MessagePriority.High, "chan"));
		Assert.Equal(1, count);
	}

	private static string ThisFile([CallerFilePath] string path = "")
	{
		return path;
	}

	private static string SourcePath(params string[] segments)
	{
		string fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile()), "..", "Extension"));
		string text = fullPath;
		foreach (string path in segments)
		{
			text = Path.Combine(text, path);
		}
		return text;
	}
}
