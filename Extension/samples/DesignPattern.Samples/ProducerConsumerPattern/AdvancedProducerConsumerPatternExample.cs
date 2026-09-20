using System;
using System.Threading;
using System.Threading.Tasks;
using Extension.Patterns.Pipelines.ProducerConsumer.Advanced;

namespace Extension.DesignPattern.ProducerConsumerPattern.Examples
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

        public OrderData(int orderId, string productName, decimal amount)
        {
            OrderId = orderId;
            ProductName = productName;
            Amount = amount;
            OrderTime = DateTime.Now;
        }

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

        public static async Task RunExample()
        {
            await RunBatchingExample();
            await RunPriorityQueueExample();
        }
    }
}