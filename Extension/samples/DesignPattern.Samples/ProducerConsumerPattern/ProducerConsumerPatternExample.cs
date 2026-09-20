using System;
using System.Threading;
using System.Threading.Tasks;
using Extension.Patterns.Pipelines.ProducerConsumer;

namespace Extension.DesignPattern.ProducerConsumerPattern.Examples
{
    /// <summary>
    /// 消息数据类
    /// </summary>
    public class MessageData
    {
        public int Id { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }

        public MessageData(int id, string content)
        {
            Id = id;
            Content = content;
            Timestamp = DateTime.Now;
        }

        public override string ToString()
        {
            return $"Message #{Id}: {Content} (Time: {Timestamp:HH:mm:ss.fff})";
        }
    }

    /// <summary>
    /// 生产者消费者模式使用示例
    /// </summary>
    public class ProducerConsumerPatternExample
    {
        public static async Task RunBasicExample()
        {
            Console.WriteLine("=== 基础生产者消费者模式示例 ===\n");

            // 1. 创建生产者消费者实例
            using (var producerConsumer = new ProducerConsumer<MessageData>(capacity: 50))
            {
                // 2. 启动生产者消费者模式
                Console.WriteLine("1. 启动生产者消费者模式 (2个生产者, 3个消费者):");
                
                var cancellationTokenSource = new CancellationTokenSource();
                
                // 启动任务
                var task = producerConsumer.StartAsync(
                    producerCount: 2,
                    consumerCount: 3,
                    produceFunc: (producerId) => new MessageData(producerId, $"来自生产者{producerId}的消息"),
                    consumeAction: (message) => 
                    {
                        Console.WriteLine($"  消费者处理: {message}");
                        // 模拟处理时间
                        Task.Delay(10).Wait();
                    },
                    cancellationTokenSource.Token
                );

                // 3. 运行一段时间后停止
                Console.WriteLine("\n2. 运行5秒后停止:");
                await Task.Delay(5000);
                cancellationTokenSource.Cancel();
                
                try
                {
                    await task;
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("任务已取消");
                }

                Console.WriteLine($"\n3. 统计信息:");
                Console.WriteLine($"  生产数量: {producerConsumer.ProducedCount}");
                Console.WriteLine($"  消费数量: {producerConsumer.ConsumedCount}");
            }

            Console.WriteLine("\n=== 基础示例结束 ===\n");
        }

        public static async Task RunAdvancedExample()
        {
            Console.WriteLine("=== 高级生产者消费者模式示例 ===\n");

            // 1. 创建高级生产者消费者实例
            using (var advancedProducerConsumer = new EnhancedProducerConsumer<MessageData>(capacity: 100))
            {
                // 2. 设置速率限制
                advancedProducerConsumer.ProductionRateLimit = 100; // 100ms生产间隔
                advancedProducerConsumer.ConsumptionRateLimit = 50; // 50ms消费间隔

                Console.WriteLine("1. 设置速率限制:");
                Console.WriteLine($"  生产速率: {advancedProducerConsumer.ProductionRateLimit}ms");
                Console.WriteLine($"  消费速率: {advancedProducerConsumer.ConsumptionRateLimit}ms");

                // 3. 启动生产者消费者模式
                Console.WriteLine("\n2. 启动高级生产者消费者模式 (1个生产者, 2个消费者):");
                
                var cancellationTokenSource = new CancellationTokenSource();
                
                // 启动任务
                var task = advancedProducerConsumer.StartAsync(
                    producerCount: 1,
                    consumerCount: 2,
                    produceFunc: (producerId) => new MessageData(producerId, $"高级消息#{advancedProducerConsumer.ProducedCount + 1}"),
                    consumeAction: (message) => 
                    {
                        Console.WriteLine($"  高级消费者处理: {message}");
                    },
                    cancellationTokenSource.Token
                );

                // 4. 运行一段时间
                Console.WriteLine("\n3. 运行3秒:");
                await Task.Delay(3000);

                // 5. 直接生产数据项
                Console.WriteLine("\n4. 直接生产数据项:");
                for (int i = 0; i < 5; i++)
                {
                    var message = new MessageData(999, $"直接生产的消息#{i + 1}");
                    if (advancedProducerConsumer.TryProduceItem(message))
                    {
                        Console.WriteLine($"  成功生产: {message}");
                    }
                    else
                    {
                        Console.WriteLine($"  生产失败: {message}");
                    }
                }

                // 6. 查看等待处理的项目数量
                Console.WriteLine($"\n5. 等待处理的项目数量: {advancedProducerConsumer.WaitingItemCount}");

                // 7. 运行一段时间后停止
                Console.WriteLine("\n6. 再运行2秒后停止:");
                await Task.Delay(2000);
                cancellationTokenSource.Cancel();
                
                try
                {
                    await task;
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("任务已取消");
                }

                Console.WriteLine($"\n7. 统计信息:");
                Console.WriteLine($"  生产数量: {advancedProducerConsumer.ProducedCount}");
                Console.WriteLine($"  消费数量: {advancedProducerConsumer.ConsumedCount}");
                Console.WriteLine($"  剩余数量: {advancedProducerConsumer.WaitingItemCount}");

                // 8. 清空剩余数据
                Console.WriteLine($"\n8. 清空剩余数据:");
                int clearedCount = advancedProducerConsumer.Clear();
                Console.WriteLine($"  清空了 {clearedCount} 个项目");
            }

            Console.WriteLine("\n=== 高级示例结束 ===\n");
        }

        public static async Task RunExample()
        {
            await RunBasicExample();
            await RunAdvancedExample();
        }
    }
}