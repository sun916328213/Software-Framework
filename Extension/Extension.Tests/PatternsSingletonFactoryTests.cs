using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Extension.Patterns.Creation.Factory;
using Extension.Patterns.Creation.Factory.Advanced;
using Extension.Patterns.Creation.Singleton;
using Xunit;

namespace Extension.Tests;

public class PatternsSingletonFactoryTests
{
	private sealed class LazyProbe
	{
	}

	private sealed class BaseProbe
	{
	}

	private sealed class ThreadProbe
	{
	}

	private sealed class FactoryPathProbe
	{
	}

	private sealed class FactoryRetryProbe
	{
	}

	private sealed class NullReturningFactoryProbe
	{
	}

	private sealed class AsyncOkProbe
	{
	}

	private sealed class AsyncFailProbe
	{
	}

	private sealed class AsyncNullProbe
	{
	}

	private sealed class ProbeFactory : AdvancedFactoryBase
	{
		public override string FactoryName => "ProbeFactory";

		public ProbeFactory()
		{
		}

		public ProbeFactory(CacheStrategy strategy, bool threadSafe = false)
			: base(strategy, threadSafe)
		{
		}
	}

	private sealed class DerivedFactoryManager : FactoryManager
	{
	}

	private sealed class OtherProbeFactory : AdvancedFactoryBase
	{
		public override string FactoryName => "OtherProbeFactory";
	}

	private class ProbeProduct : AdvancedProductBase
	{
		public int InitializeCount { get; private set; }

		public int DisposeCount { get; private set; }

		public ProbeProduct()
			: this("probe-1")
		{
		}

		public ProbeProduct(string id)
			: base(id, "探针产品", "probe")
		{
		}

		protected override void OnInitialize()
		{
			InitializeCount++;
		}

		protected override void OnDispose()
		{
			DisposeCount++;
		}
	}

	private sealed class WrongProduct : AdvancedProductBase
	{
		public WrongProduct()
			: base("wrong-1", "错型产品", "probe")
		{
		}
	}

	private sealed class NullIdProduct : AdvancedProductBase
	{
		public NullIdProduct()
			: base(null, "name", "type")
		{
		}
	}

	[Fact]
	public void Singleton_GetInstance泛型_同类型返回同一实例()
	{
		Assert.Same(Singleton.GetInstance<LazyProbe>(), Singleton.GetInstance<LazyProbe>());
	}

	[Fact]
	public void Singleton_无参路径与工厂路径互不相干_同一个类型会得到两个单例()
	{
		FactoryPathProbe instance = Singleton.GetInstance<FactoryPathProbe>();
		FactoryPathProbe instance2 = Singleton.GetInstance(() => new FactoryPathProbe());
		Assert.NotNull(instance);
		Assert.NotNull(instance2);
		Assert.NotSame(instance, instance2);
		Assert.Same(instance2, Singleton.GetInstance(() => new FactoryPathProbe()));
		Assert.Same(instance, Singleton.GetInstance<FactoryPathProbe>());
	}

	[Fact]
	public void Singleton_工厂返回null抛InvalidOperationException()
	{
		InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Singleton.GetInstance(() => (NullReturningFactoryProbe)null));
		Assert.Contains("null", ex.Message);
	}

	[Fact]
	public void Singleton_工厂抛null之后不记忆失败_下次仍会重试()
	{
		int calls = 0;
		Func<FactoryRetryProbe> failing = delegate
		{
			calls++;
			return (FactoryRetryProbe)null;
		};
		Assert.Throws<InvalidOperationException>(() => Singleton.GetInstance(failing));
		Assert.Throws<InvalidOperationException>(() => Singleton.GetInstance(failing));
		Assert.Equal(2, calls);
	}

	[Fact]
	public async Task Singleton_异步工厂失败后永不重试_抛不会重试()
	{
		int calls = 0;
		Func<Task<AsyncFailProbe>> failing = delegate
		{
			calls++;
			throw new InvalidOperationException("boom");
		};
		InvalidOperationException first = await Assert.ThrowsAsync<InvalidOperationException>(() => Singleton.GetInstanceAsync(failing));
		InvalidOperationException second = await Assert.ThrowsAsync<InvalidOperationException>(() => Singleton.GetInstanceAsync(failing));
		Assert.Equal("boom", first.Message);
		Assert.Equal(1, calls);
		Assert.Contains("不会重试", second.Message);
	}

	[Fact]
	public async Task Singleton_异步工厂返回null抛InvalidOperationException()
	{
		Assert.Contains("null", (await Assert.ThrowsAsync<InvalidOperationException>(() => Singleton.GetInstanceAsync(() => Task.FromResult<AsyncNullProbe>(null)))).Message);
	}

	[Fact]
	public async Task Singleton_异步工厂成功后返回同一实例()
	{
		Assert.Same(await Singleton.GetInstanceAsync(() => Task.FromResult(new AsyncOkProbe())), await Singleton.GetInstanceAsync(() => Task.FromResult(new AsyncOkProbe())));
	}

	[Fact]
	public async Task Singleton_工厂参数为null一律抛ArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => Singleton.GetInstance<LazyProbe>(null));
		await Assert.ThrowsAsync<ArgumentNullException>(() => Singleton.GetInstanceAsync<AsyncOkProbe>(null));
	}

	[Fact]
	public void SingletonBase_Instance是懒加载且唯一()
	{
		Assert.Same(SingletonBase<BaseProbe>.Instance, SingletonBase<BaseProbe>.Instance);
	}

	[Fact]
	public void ThreadSafeSingleton_Instance唯一_ResetInstance之后是新实例()
	{
		ThreadProbe instance = ThreadSafeSingleton<ThreadProbe>.Instance;
		Assert.Same(instance, ThreadSafeSingleton<ThreadProbe>.Instance);
		ThreadSafeSingleton<ThreadProbe>.ResetInstance();
		ThreadProbe instance2 = ThreadSafeSingleton<ThreadProbe>.Instance;
		Assert.NotSame(instance, instance2);
		Assert.Same(instance2, ThreadSafeSingleton<ThreadProbe>.Instance);
	}

	[Fact]
	public void Singleton_双重检查锁均已补volatile_工厂版与异步版()
	{
		string text = SourcePath("Patterns", "Creation", "Singleton", "SingletonPattern.cs");
		Assert.True(File.Exists(text), "找不到源码文件：" + text);
		string text2 = File.ReadAllText(text);
		int num = text2.IndexOf("class SingletonInstanceWithFactory<T>", StringComparison.Ordinal);
		int num2 = text2.IndexOf("class SingletonInstanceAsync<T>", StringComparison.Ordinal);
		Assert.True(num > 0, "找不到 SingletonInstanceWithFactory<T> 声明");
		Assert.True(num2 > num, "找不到 SingletonInstanceAsync<T> 声明（或声明顺序变了）");
		List<string> list = new List<string>();
		string[] array = text2.Substring(num, num2 - num).Split('\n');
		foreach (string text3 in array)
		{
			if (!text3.TrimStart().StartsWith("//"))
			{
				list.Add(text3);
			}
		}
		string actualString = string.Join("\n", list);
		Assert.Contains("private static volatile T _instance;", actualString);
		Assert.DoesNotContain("private static T _instance;", actualString);
		Assert.Contains("private static volatile T _instance;", text2);
		int num3 = text2.IndexOf("class SingletonBase<T>", num2, StringComparison.Ordinal);
		Assert.True(num3 > num2, "找不到 SingletonInstanceAsync<T> 之后的类声明");
		List<string> list2 = new List<string>();
		string[] array2 = text2.Substring(num2, num3 - num2).Split('\n');
		foreach (string text4 in array2)
		{
			if (!text4.TrimStart().StartsWith("//"))
			{
				list2.Add(text4);
			}
		}
		string actualString2 = string.Join("\n", list2);
		Assert.Contains("private static volatile T _instance;", actualString2);
		Assert.DoesNotContain("private static T _instance;", actualString2);
		Assert.Contains("private static volatile bool _initialized = false;", actualString2);
		Assert.DoesNotContain("private static bool _initialized = false;", actualString2);
		Assert.Contains("private static volatile bool _initializationFailed = false;", actualString2);
		Assert.DoesNotContain("private static bool _initializationFailed = false;", actualString2);
	}

	[Fact]
	public void Products_六个具体产品的Name是固定常量()
	{
		Assert.Equal("产品A1", new ConcreteProductA1().Name);
		Assert.Equal("产品A2", new ConcreteProductA2().Name);
		Assert.Equal("产品B1", new ConcreteProductB1().Name);
		Assert.Equal("产品B2", new ConcreteProductB2().Name);
	}

	[Fact]
	public void Products_Name是只读属性_没有setter()
	{
		Assert.Null(typeof(IProductA).GetProperty("Name").SetMethod);
		Assert.Null(typeof(IProductB).GetProperty("Name").SetMethod);
	}

	[Fact]
	public void Products_Collaborate对null产品抛ArgumentNullException()
	{
		ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
		{
			new ConcreteProductB1().Collaborate(null);
		});
		ArgumentNullException ex2 = Assert.Throws<ArgumentNullException>(delegate
		{
			new ConcreteProductB2().Collaborate(null);
		});
		Assert.Equal("productA", ex.ParamName);
		Assert.Equal("productA", ex2.ParamName);
	}

	[Fact]
	public void Products_全部行为方法返回void_只写控制台()
	{
		Assert.Equal<Type>(typeof(void), typeof(IProductA).GetMethod("UseFeatureA").ReturnType);
		Assert.Equal<Type>(typeof(void), typeof(IProductB).GetMethod("UseFeatureB").ReturnType);
		Assert.Equal<Type>(typeof(void), typeof(IProductB).GetMethod("Collaborate").ReturnType);
	}

	[Fact]
	public void AdvancedFactory_CacheStrategy_Singleton与All行为完全一致()
	{
		CacheStrategy[] array = new CacheStrategy[2]
		{
			CacheStrategy.All,
			CacheStrategy.Singleton
		};
		foreach (CacheStrategy strategy in array)
		{
			ProbeFactory probeFactory = new ProbeFactory(strategy);
			probeFactory.RegisterProduct("p", () => new ProbeProduct());
			IAdvancedProduct expected = probeFactory.CreateProduct("p");
			IAdvancedProduct actual = probeFactory.CreateProduct("p");
			Assert.Same(expected, actual);
			Assert.Equal(1, probeFactory.CachedProductCount);
		}
	}

	[Fact]
	public void AdvancedFactory_CacheStrategy_None每次都是新实例且不缓存()
	{
		ProbeFactory probeFactory = new ProbeFactory(CacheStrategy.None);
		probeFactory.RegisterProduct("p", () => new ProbeProduct());
		IAdvancedProduct expected = probeFactory.CreateProduct("p");
		IAdvancedProduct actual = probeFactory.CreateProduct("p");
		Assert.NotSame(expected, actual);
		Assert.Equal(0, probeFactory.CachedProductCount);
	}

	[Fact]
	public void AdvancedFactory_默认缓存策略是All_默认非线程安全()
	{
		ProbeFactory probeFactory = new ProbeFactory();
		Assert.Equal(CacheStrategy.All, probeFactory.CacheStrategy);
		Assert.False(probeFactory.IsProductThreadSafe);
	}

	[Fact]
	public void AdvancedFactory_构造函数可设置缓存策略与线程安全标志()
	{
		ProbeFactory probeFactory = new ProbeFactory(CacheStrategy.None, threadSafe: true);
		Assert.Equal(CacheStrategy.None, probeFactory.CacheStrategy);
		Assert.True(probeFactory.IsProductThreadSafe);
	}

	[Fact]
	public void AdvancedFactory_创建器返回null时抛InvalidOperationException_与未注册可区分()
	{
		ProbeFactory factory = new ProbeFactory();
		factory.RegisterProduct("p", () => (IAdvancedProduct)null);
		InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => factory.CreateProduct("p"));
		Assert.Contains("返回了 null", ex.Message);
		Assert.Contains("'p'", ex.Message);
		Assert.DoesNotContain("未找到", ex.Message);
		KeyNotFoundException ex2 = Assert.Throws<KeyNotFoundException>(() => factory.CreateProduct("没注册过"));
		Assert.Contains("未找到", ex2.Message);
	}

	[Fact]
	public void AdvancedFactory_未注册的产品抛KeyNotFoundException()
	{
		ProbeFactory factory = new ProbeFactory();
		KeyNotFoundException ex = Assert.Throws<KeyNotFoundException>(() => factory.CreateProduct("不存在"));
		Assert.Contains(factory.FactoryName, ex.Message);
	}

	[Fact]
	public void AdvancedFactory_productId为空或null都抛ArgumentException()
	{
		ProbeFactory factory = new ProbeFactory();
		Assert.Throws<ArgumentException>(() => factory.CreateProduct(""));
		Assert.Throws<ArgumentException>(() => factory.CreateProduct(null));
	}

	[Fact]
	public void AdvancedFactory_RegisterProduct的参数校验()
	{
		ProbeFactory factory = new ProbeFactory();
		Assert.Throws<ArgumentException>(delegate
		{
			factory.RegisterProduct("", () => new ProbeProduct());
		});
		Assert.Throws<ArgumentException>(delegate
		{
			factory.RegisterProduct(null, () => new ProbeProduct());
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			factory.RegisterProduct("p", null);
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			factory.RegisterProduct<ProbeProduct>("p", null);
		});
	}

	[Fact]
	public void AdvancedFactory_CreateProduct会调用生命周期Initialize()
	{
		ProbeFactory probeFactory = new ProbeFactory(CacheStrategy.None);
		ProbeProduct product = new ProbeProduct();
		probeFactory.RegisterProduct("p", () => product);
		probeFactory.CreateProduct("p");
		Assert.True(product.IsInitialized);
		Assert.Equal(1, product.InitializeCount);
	}

	[Fact]
	public void AdvancedFactory_CreateProduct_T_类型不匹配抛InvalidCastException()
	{
		ProbeFactory factory = new ProbeFactory();
		factory.RegisterProduct("p", () => new WrongProduct());
		Assert.Throws<InvalidCastException>(() => factory.CreateProduct<ProbeProduct>("p"));
		Assert.NotNull(factory.CreateProduct<WrongProduct>("p"));
	}

	[Fact]
	public void AdvancedFactory_RegisterProduct泛型用默认构造函数()
	{
		ProbeFactory probeFactory = new ProbeFactory();
		probeFactory.RegisterProduct<ProbeProduct>("p");
		Assert.True(probeFactory.ContainsProduct("p"));
		Assert.IsType<ProbeProduct>(probeFactory.CreateProduct("p"));
	}

	[Fact]
	public void AdvancedFactory_RemoveProduct会Dispose缓存实例并摘掉注册()
	{
		ProbeFactory probeFactory = new ProbeFactory();
		ProbeProduct product = new ProbeProduct();
		probeFactory.RegisterProduct("p", () => product);
		probeFactory.CreateProduct("p");
		Assert.True(probeFactory.RemoveProduct("p"));
		Assert.Equal(1, product.DisposeCount);
		Assert.False(probeFactory.ContainsProduct("p"));
		Assert.Equal(0, probeFactory.CachedProductCount);
	}

	[Fact]
	public void AdvancedFactory_ClearCache会Dispose缓存但保留注册()
	{
		ProbeFactory probeFactory = new ProbeFactory();
		ProbeProduct product = new ProbeProduct();
		probeFactory.RegisterProduct("p", () => product);
		probeFactory.CreateProduct("p");
		probeFactory.ClearCache();
		Assert.Equal(1, product.DisposeCount);
		Assert.True(probeFactory.ContainsProduct("p"));
		Assert.Equal(0, probeFactory.CachedProductCount);
	}

	[Fact]
	public void AdvancedFactory_ClearProducts会清空注册与缓存()
	{
		ProbeFactory probeFactory = new ProbeFactory();
		ProbeProduct product = new ProbeProduct();
		probeFactory.RegisterProduct("p", () => product);
		probeFactory.CreateProduct("p");
		probeFactory.ClearProducts();
		Assert.Equal(1, product.DisposeCount);
		Assert.False(probeFactory.ContainsProduct("p"));
		Assert.Empty(probeFactory.GetRegisteredProductIds());
	}

	[Fact]
	public void AdvancedFactory_ContainsProduct对空id返回false_不抛()
	{
		ProbeFactory probeFactory = new ProbeFactory();
		Assert.False(probeFactory.ContainsProduct(""));
		Assert.False(probeFactory.ContainsProduct(null));
	}

	[Fact]
	public void AdvancedFactory_RemoveProduct对空id返回false_不抛()
	{
		ProbeFactory probeFactory = new ProbeFactory();
		Assert.False(probeFactory.RemoveProduct(""));
		Assert.False(probeFactory.RemoveProduct(null));
	}

	[Fact]
	public void AdvancedFactory_GetRegisteredProductIds是只读集合()
	{
		ProbeFactory probeFactory = new ProbeFactory();
		probeFactory.RegisterProduct<ProbeProduct>("a");
		probeFactory.RegisterProduct<ProbeProduct>("b");
		IReadOnlyCollection<string> registeredProductIds = probeFactory.GetRegisteredProductIds();
		Assert.Equal(2, registeredProductIds.Count);
		Assert.Contains("a", registeredProductIds);
		Assert.Contains("b", registeredProductIds);
	}

	[Fact]
	public async Task AdvancedFactory_CreateProductAsync可正常创建_实为TaskRun包装的伪异步()
	{
		ProbeFactory factory = new ProbeFactory();
		factory.RegisterProduct<ProbeProduct>("p");
		Assert.NotNull(await factory.CreateProductAsync("p"));
		Assert.NotNull(await factory.CreateProductAsync<ProbeProduct>("p"));
	}

	[Fact]
	public void FactoryManager_重复注册同名工厂是覆盖_不是去重()
	{
		FactoryManager factoryManager = new FactoryManager();
		ProbeFactory factory = new ProbeFactory();
		ProbeFactory probeFactory = new ProbeFactory();
		factoryManager.RegisterFactory("f", factory);
		factoryManager.RegisterFactory("f", probeFactory);
		Assert.Same(probeFactory, factoryManager.GetFactory("f"));
		Assert.Single(factoryManager.GetRegisteredFactoryNames());
	}

	[Fact]
	public void FactoryManager_参数校验与未找到语义()
	{
		FactoryManager manager = new FactoryManager();
		Assert.Throws<ArgumentException>(delegate
		{
			manager.RegisterFactory("", new ProbeFactory());
		});
		Assert.Throws<ArgumentNullException>(delegate
		{
			manager.RegisterFactory("f", null);
		});
		Assert.Throws<KeyNotFoundException>(() => manager.GetFactory("不存在"));
		Assert.False(manager.ContainsFactory(""));
		Assert.False(manager.ContainsFactory(null));
	}

	[Fact]
	public void FactoryManager_GetFactory_T_类型不匹配抛InvalidCastException()
	{
		FactoryManager manager = new FactoryManager();
		manager.RegisterFactory("f", new ProbeFactory());
		Assert.NotNull(manager.GetFactory<AdvancedFactoryBase>("f"));
		Assert.NotNull(manager.GetFactory<ProbeFactory>("f"));
		Assert.Throws<InvalidCastException>(() => manager.GetFactory<OtherProbeFactory>("f"));
	}

	[Fact]
	public void FactoryManager_RemoveFactory会先清空该工厂的产品()
	{
		ProbeFactory probeFactory = new ProbeFactory();
		ProbeProduct product = new ProbeProduct();
		probeFactory.RegisterProduct("p", () => product);
		probeFactory.CreateProduct("p");
		FactoryManager factoryManager = new FactoryManager();
		factoryManager.RegisterFactory("f", probeFactory);
		Assert.True(factoryManager.RemoveFactory("f"));
		Assert.Equal(1, product.DisposeCount);
		Assert.False(factoryManager.ContainsFactory("f"));
		Assert.False(factoryManager.RemoveFactory("f"));
	}

	[Fact]
	public void FactoryManager_ClearFactories会清空全部工厂的产品()
	{
		ProbeFactory probeFactory = new ProbeFactory();
		ProbeProduct product = new ProbeProduct();
		probeFactory.RegisterProduct("p", () => product);
		probeFactory.CreateProduct("p");
		FactoryManager factoryManager = new FactoryManager();
		factoryManager.RegisterFactory("f", probeFactory);
		factoryManager.ClearFactories();
		Assert.Equal(1, product.DisposeCount);
		Assert.Empty(factoryManager.GetRegisteredFactoryNames());
	}

	[Fact]
	public void FactoryManager_CreateProduct转发到目标工厂()
	{
		ProbeFactory probeFactory = new ProbeFactory();
		probeFactory.RegisterProduct<ProbeProduct>("p");
		FactoryManager factoryManager = new FactoryManager();
		factoryManager.RegisterFactory("f", probeFactory);
		Assert.IsType<ProbeProduct>(factoryManager.CreateProduct("f", "p"));
		Assert.IsType<ProbeProduct>(factoryManager.CreateProduct<ProbeProduct>("f", "p"));
		Assert.NotNull(factoryManager.CreateProductAsync("f", "p").Result);
	}

	[Fact]
	public void FactoryBuilder_Build_T_只对FactoryManager本身成立()
	{
		Assert.NotNull(FactoryBuilder.Create().Build());
		Assert.NotNull(FactoryBuilder.Create().Build<FactoryManager>());
		Assert.Throws<InvalidCastException>(() => FactoryBuilder.Create().Build<DerivedFactoryManager>());
	}

	[Fact]
	public void FactoryBuilder_AddFactory带策略_反射能写进protected_setter()
	{
		ProbeFactory probeFactory = new ProbeFactory(CacheStrategy.All);
		FactoryManager factoryManager = FactoryBuilder.Create().AddFactory("f", probeFactory, CacheStrategy.None).Build();
		Assert.Equal(CacheStrategy.None, probeFactory.CacheStrategy);
		Assert.Same(probeFactory, factoryManager.GetFactory("f"));
	}

	[Fact]
	public void FactoryBuilder_AddFactory是链式的_返回自身()
	{
		FactoryBuilder factoryBuilder = FactoryBuilder.Create();
		Assert.Same(factoryBuilder, factoryBuilder.AddFactory("a", new ProbeFactory()));
		Assert.Same(factoryBuilder, factoryBuilder.AddFactory("b", new ProbeFactory(), CacheStrategy.None));
	}

	[Fact]
	public void ProductLifecycle_Initialize与Dispose都是幂等的()
	{
		ProbeProduct probeProduct = new ProbeProduct();
		probeProduct.Initialize();
		probeProduct.Initialize();
		probeProduct.Dispose();
		probeProduct.Dispose();
		Assert.Equal(1, probeProduct.InitializeCount);
		Assert.Equal(1, probeProduct.DisposeCount);
	}

	[Fact]
	public void ProductLifecycle_Dispose之后仍可Initialize_两个标志互相独立()
	{
		ProbeProduct probeProduct = new ProbeProduct();
		probeProduct.Dispose();
		Assert.True(probeProduct.IsDisposed);
		Assert.False(probeProduct.IsInitialized);
		probeProduct.Initialize();
		Assert.True(probeProduct.IsInitialized);
		Assert.True(probeProduct.IsDisposed);
		Assert.Equal(1, probeProduct.InitializeCount);
	}

	[Fact]
	public void AdvancedProductBase_两条Dispose入口共享同一个幂等标志()
	{
		ProbeProduct probeProduct = new ProbeProduct();
		((IDisposable)probeProduct).Dispose();
		probeProduct.Dispose();
		Assert.Equal(1, probeProduct.DisposeCount);
		Assert.True(probeProduct.IsDisposed);
	}

	[Fact]
	public void AdvancedProductBase_构造函数对null参数抛ArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => new NullIdProduct());
	}

	[Fact]
	public void AdvancedProductBase_属性与Execute可用()
	{
		ProbeProduct probeProduct = new ProbeProduct("自定义ID");
		Assert.Equal("自定义ID", probeProduct.Id);
		Assert.Equal("探针产品", probeProduct.Name);
		Assert.Equal("probe", probeProduct.Type);
		probeProduct.Execute();
	}

	private static string SourcePath(params string[] segments)
	{
		string path = ThisFile();
		string fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), "..", "Extension"));
		string text = fullPath;
		foreach (string path2 in segments)
		{
			text = Path.Combine(text, path2);
		}
		return text;
	}

	private static string ThisFile([CallerFilePath] string path = "")
	{
		return path;
	}
}
