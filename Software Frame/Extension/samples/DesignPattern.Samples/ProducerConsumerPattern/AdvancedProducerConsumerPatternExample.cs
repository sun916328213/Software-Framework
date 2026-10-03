// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：OrderData、AdvancedProducerConsumerPatternExample。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Threading、System.Threading.Tasks、Extension.Patterns.Pipelines.ProducerConsumer.Advanced。导入不代表所有类型都会在运行时加载。
using System;
using System.Threading;
using System.Threading.Tasks;
using Extension.Patterns.Pipelines.ProducerConsumer.Advanced;

namespace DesignPattern.Samples.DesignPattern.ProducerConsumerPattern.Examples
{
    /// <summary>
    /// 订单数据类
    /// </summary>
    public class OrderData
    {
        public int OrderId { get; set; }
        public string ProductName { get; set; }
        public decimal Amount { get; set; }
        public DateTime OrderTime { get; set; }

        /// <summary>
        /// 初始化 OrderData 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public OrderData(int orderId, string productName, decimal amount)
        {
            OrderId = orderId;
            ProductName = productName;
            Amount = amount;
            OrderTime = DateTime.Now;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// </summary>
        public override string ToString()
        {
            return $"Order #{OrderId}: {ProductName} - ${Amount:F2} ({OrderTime:HH:mm:ss.fff})";
        }
    }

    /// <summary>
    /// 高级生产者消费者模式使用示例
    /// </summary>
    public class AdvancedProducerConsumerPatternExample
    {
        /// <summary>
        /// 运行当前处理流程；对应入口为 RunBatchingExample。
        /// 正文中的关键协作调用包括 WriteLine、StartAsync、Delay、Cancel；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public static async Task RunBatchingExample()
        {
            Console.WriteLine("=== 批处理生产者消费者模式示例 ===\n");

            // 1. 创建批处理配置
            var config = new ProducerConsumerConfig
            {
                Capacity = 100,
                BatchSize = 5, // 每批处理5个订单
                BatchTimeoutMs = 2000, // 2秒超时
                EnableBatching = true
            };

            // 2. 创建高级生产者消费者实例
            using (var producerConsumer = new ConfigurableProducerConsumer<OrderData>(config))
            {
                Console.WriteLine("1. 启动批处理生产者消费者模式 (1个生产者, 1个消费者):");
                
                var cancellationTokenSource = new CancellationTokenSource();
                
                // 启动任务
                var task = producerConsumer.StartAsync(
                    producerCount: 1,
                    consumerCount: 1,
                    produceFunc: (producerId) => new OrderData(
                        (int)producerConsumer.ProducedCount + 1, 
                        $"产品{producerConsumer.ProducedCount + 1}", 
                        (producerConsumer.ProducedCount + 1) * 10.5m),
                    consumeAction: (order) => 
                    {
                        Console.WriteLine($"  处理订单: {order}");
                    },
                    cancellationTokenSource.Token
                );

                // 3. 运行一段时间
                Console.WriteLine("\n2. 运行5秒:");
                await Task.Delay(5000);
                
                // 4. 停止
                Console.WriteLine("\n3. 停止处理:");
                cancellationTokenSource.Cancel();
                
                try
                {
                    await task;
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("任务已取消");
                }

                Console.WriteLine($"\n4. 统计信息:");
                Console.WriteLine($"  生产数量: {producerConsumer.ProducedCount}");
                Console.WriteLine($"  消费数量: {producerConsumer.ConsumedCount}");
            }

            Console.WriteLine("\n=== 批处理示例结束 ===\n");
        }

        /// <summary>
        /// 运行当前处理流程；对应入口为 RunPriorityQueueExample。
        /// 正文中的关键协作调用包括 WriteLine、StartConsumersAsync、Wait、Delay、Produce；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public static async Task RunPriorityQueueExample()
        {
            Console.WriteLine("=== 优先级队列生产者消费者模式示例 ===\n");

            // 1. 创建优先级队列生产者消费者实例
            using (var priorityProducerConsumer = new PriorityQueueProducerConsumer<OrderData>())
            {
                Console.WriteLine("1. 启动优先级队列生产者消费者模式 (3个消费者):");
                
                var cancellationTokenSource = new CancellationTokenSource();
                
                // 启动消费者任务
                var consumerTask = priorityProducerConsumer.StartConsumersAsync(
                    consumerCount: 3,
                    consumeAction: (order) => 
                    {
                        Console.WriteLine($"  处理订单: {order}");
                        // 模拟处理时间
                        Task.Delay(50).Wait();
                    },
                    cancellationTokenSource.Token
                );

                // 2. 生产不同优先级的订单
                Console.WriteLine("\n2. 生产不同优先级的订单:");
                for (int i = 0; i < 10; i++)
                {
                    int priority = i % 3; // 优先级 0, 1, 2 循环
                    var order = new OrderData(i + 1, $"产品{i + 1}", (i + 1) * 10.5m);
                    priorityProducerConsumer.Produce(order, priority);
                    Console.WriteLine($"  生产订单 (优先级 {priority}): {order}");
                }

                // 3. 运行一段时间
                Console.WriteLine("\n3. 运行3秒:");
                await Task.Delay(3000);
                
                // 4. 停止
                Console.WriteLine("\n4. 停止处理:");
                cancellationTokenSource.Cancel();
                
                try
                {
                    await consumerTask;
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("任务已取消");
                }

                Console.WriteLine($"\n5. 统计信息:");
                Console.WriteLine($"  生产数量: {priorityProducerConsumer.ProducedCount}");
                Console.WriteLine($"  消费数量: {priorityProducerConsumer.ConsumedCount}");
            }

            Console.WriteLine("\n=== 优先级队列示例结束 ===\n");
        }

        /// <summary>
        /// 运行当前处理流程；对应入口为 RunExample。
        /// 正文中的关键协作调用包括 RunBatchingExample、RunPriorityQueueExample；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public static async Task RunExample()
        {
            await RunBatchingExample();
            await RunPriorityQueueExample();
        }
    }
}