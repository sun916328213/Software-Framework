// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：PriorityItem、BatchItem、ProducerConsumerConfig、ConfigurableProducerConsumer、PriorityQueueProducerConsumer。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.Patterns.Pipelines.ProducerConsumer、System、System.Collections.Concurrent、System.Collections.Generic、System.Threading、System.Threading.Channels。导入不代表所有类型都会在运行时加载。
using Extension.Patterns.Pipelines.ProducerConsumer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Extension.Patterns.Pipelines.ProducerConsumer.Advanced
{
    /// <summary>
    /// 带优先级的数据项
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public class PriorityItem<T> : IComparable<PriorityItem<T>>
    {
        public T Data { get; }
        public int Priority { get; }
        public DateTime CreationTime { get; }

        /// <summary>
        /// 初始化 PriorityItem 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public PriorityItem(T data, int priority)
        {
            Data = data;
            Priority = priority;
            CreationTime = DateTime.Now;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 CompareTo。
        /// 正文中的关键协作调用包括 CompareTo；具体条件和先后次序见方法体。
        /// </summary>
        public int CompareTo(PriorityItem<T> other)
        {
            if (other == null) return 1;
            
            // 高优先级数字表示高优先级
            int priorityComparison = other.Priority.CompareTo(Priority);
            if (priorityComparison != 0)
                return priorityComparison;
            
            // 如果优先级相同，按创建时间排序（先进先出）
            return CreationTime.CompareTo(other.CreationTime);
        }
    }

    /// <summary>
    /// 批处理数据项
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public class BatchItem<T>
    {
        public List<T> Items { get; }
        public DateTime BatchTime { get; }

        /// <summary>
        /// 初始化 BatchItem 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
        /// </summary>
        public BatchItem(List<T> items)
        {
            Items = items ?? throw new ArgumentNullException(nameof(items));
            BatchTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 高级生产者消费者配置
    /// </summary>
    public class ProducerConsumerConfig
    {
        /// <summary>
        /// 通道容量
        /// </summary>
        public int Capacity { get; set; } = 100;

        /// <summary>
        /// 通道满时的行为
        /// </summary>
        public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.Wait;

        /// <summary>
        /// 批处理大小
        /// </summary>
        public int BatchSize { get; set; } = 1;

        /// <summary>
        /// 批处理超时时间（毫秒）
        /// </summary>
        public int BatchTimeoutMs { get; set; } = 1000;

        /// <summary>
        /// 是否启用优先级队列
        /// </summary>
        public bool EnablePriorityQueue { get; set; } = false;

        /// <summary>
        /// 是否启用批处理
        /// </summary>
        public bool EnableBatching { get; set; } = false;
    }

    /// <summary>
    /// 高级生产者消费者模式实现
    /// 支持优先级队列、批处理等功能
    /// </summary>
    /// <typeparam name="T">队列中存储的数据类型</typeparam>
    public class ConfigurableProducerConsumer<T> : IProducerConsumer<T>
    {
        private readonly Channel<T> _channel;
        private readonly ChannelWriter<T> _writer;
        private readonly ChannelReader<T> _reader;
        private readonly ProducerConsumerConfig _config;
        
        // 控制是否继续运行的标志
        private volatile bool _running = true;
        
        // 用于跟踪是否已释放资源
        private bool _disposed = false;

        // ⛔ 序 72（2026-09-18 修）：与 `ProducerConsumer<T>`（序 41）同型 —— `Stop()` 会 `_writer.Complete()`
        // 把 channel **永久关闭**，而 Channel 不可重开。用这个标志把"已停止"与"从未启动"区分开
        //（`_running` 做不到：构造函数里它就是 true）。
        private volatile bool _stopped = false;

        // 生产和消费统计
        private long _producedCount = 0;
        private long _consumedCount = 0;

        /// <summary>
        /// 获取已生产的项目数量
        /// </summary>
        public long ProducedCount => _producedCount;

        /// <summary>
        /// 获取已消费的项目数量
        /// </summary>
        public long ConsumedCount => _consumedCount;

        /// <summary>
        /// 初始化高级生产者消费者模式
        /// </summary>
        /// <param name="config">配置选项</param>
        public ConfigurableProducerConsumer(ProducerConsumerConfig config = null)
        {
            _config = config ?? new ProducerConsumerConfig();
            
            var options = new BoundedChannelOptions(_config.Capacity)
            {
                FullMode = _config.FullMode
            };
            _channel = Channel.CreateBounded<T>(options);
            _writer = _channel.Writer;
            _reader = _channel.Reader;
        }

        /// <summary>
        /// 生产者方法
        /// </summary>
        /// <param name="producerId">生产者ID</param>
        /// <param name="produceFunc">生产函数，用于生成要放入队列的数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task ProduceAsync(int producerId, Func<T> produceFunc, CancellationToken cancellationToken = default)
        {
            while (_running && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    T item = produceFunc();
                    await _writer.WriteAsync(item, cancellationToken);

                    // 原子性增加计数器
                    Interlocked.Increment(ref _producedCount);

                    // 减少日志输出频率以提高性能
                    long count = _producedCount;
                    if (count % 1000 == 0)
                    {
                        Console.WriteLine($"[Producer-{producerId}] Produced item #{count}");
                    }
                }
                catch (OperationCanceledException)
                {
                    // 任务被取消是正常的，忽略异常
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Producer-{producerId}] 发生异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 消费者方法（支持批处理）
        /// </summary>
        /// <param name="consumerId">消费者ID</param>
        /// <param name="consumeAction">消费动作，用于处理从队列中取出的数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task ConsumeAsync(int consumerId, Action<T> consumeAction, CancellationToken cancellationToken = default)
        {
            if (_config.EnableBatching && _config.BatchSize > 1)
            {
                await ConsumeWithBatchingAsync(consumerId, consumeAction, cancellationToken);
            }
            else
            {
                await ConsumeSingleAsync(consumerId, consumeAction, cancellationToken);
            }
        }

        /// <summary>
        /// 单项消费方法
        /// </summary>
        private async Task ConsumeSingleAsync(int consumerId, Action<T> consumeAction, CancellationToken cancellationToken)
        {
            while (_running && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 等待数据可用
                    if (await _reader.WaitToReadAsync(cancellationToken))
                    {
                        if (_reader.TryRead(out T item))
                        {
                            consumeAction(item);

                            // 原子性增加计数器
                            Interlocked.Increment(ref _consumedCount);

                            // 减少日志输出频率以提高性能
                            long count = _consumedCount;
                            if (count % 1000 == 0)
                            {
                                Console.WriteLine($"[Consumer-{consumerId}] Consumed an item #{count}");
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // 任务被取消是正常的，忽略异常
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Consumer-{consumerId}] 发生异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 批处理消费方法
        /// </summary>
        private async Task ConsumeWithBatchingAsync(int consumerId, Action<T> consumeAction, CancellationToken cancellationToken)
        {
            var batch = new List<T>();
            var batchTimer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);

            try
            {
                while (_running && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // 检查批处理是否已满或超时
                        if (batch.Count >= _config.BatchSize)
                        {
                            ProcessBatch(consumerId, batch, consumeAction);
                            batch.Clear();
                        }

                        // 等待数据可用或超时
                        var waitTask = _reader.WaitToReadAsync(cancellationToken).AsTask();
                        var timeoutTask = Task.Delay(_config.BatchTimeoutMs, cancellationToken);
                        var completedTask = await Task.WhenAny(waitTask, timeoutTask);

                        if (completedTask == waitTask)
                        {
                            // 数据可用
                            var hasData = await waitTask;
                            if (hasData && _reader.TryRead(out T item))
                            {
                                batch.Add(item);
                            }
                        }
                        else if (completedTask == timeoutTask && batch.Count > 0)
                        {
                            // 超时，处理当前批次
                            ProcessBatch(consumerId, batch, consumeAction);
                            batch.Clear();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 任务被取消是正常的，处理剩余批次
                        if (batch.Count > 0)
                        {
                            ProcessBatch(consumerId, batch, consumeAction);
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Consumer-{consumerId}] 发生异常: {ex.Message}");
                    }
                }
            }
            finally
            {
                batchTimer.Dispose();
            }
        }

        /// <summary>
        /// 处理批次数据
        /// </summary>
        private void ProcessBatch(int consumerId, List<T> batch, Action<T> consumeAction)
        {
            foreach (var item in batch)
            {
                consumeAction(item);
                Interlocked.Increment(ref _consumedCount);
            }

            long count = _consumedCount;
            if (count % 1000 == 0)
            {
                Console.WriteLine($"[Consumer-{consumerId}] Consumed batch of {batch.Count} items, total #{count}");
            }
        }

        /// <summary>
        /// 启动生产者消费者模式
        /// </summary>
        /// <param name="producerCount">生产者数量</param>
        /// <param name="consumerCount">消费者数量</param>
        /// <param name="produceFunc">生产函数，用于生成要放入队列的数据</param>
        /// <param name="consumeAction">消费动作，用于处理从队列中取出的数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task StartAsync(int producerCount, int consumerCount, Func<int, T> produceFunc, Action<T> consumeAction, CancellationToken cancellationToken = default)
        {
            if (producerCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(producerCount), "生产者数量必须大于0");
            
            if (consumerCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(consumerCount), "消费者数量必须大于0");
            
            if (produceFunc == null)
                throw new ArgumentNullException(nameof(produceFunc));
            
            if (consumeAction == null)
                throw new ArgumentNullException(nameof(consumeAction));

            // ⛔ 序 72（2026-09-18 修）：与 `ProducerConsumer<T>`（序 41）**逐字同型** —— `Stop()` 会
            // `_writer.Complete()` 永久关闭 channel，而 channel 不可重开；原实现无条件把 `_running` 置回 true
            // 就启动任务 ⇒ 生产者写已关闭的 channel（ChannelClosedException 被 catch(Exception) 吞掉后继续循环）、
            // 消费者 `WaitToReadAsync` 在已完成的 channel 上**立即返回 false**
            // ⇒ 两个循环同时空转，`Task.WhenAll` **永不返回**（只能靠取消令牌逃出）。
            // ⇒ 已停止 / 已释放的实例必须先挡在门外，绝不能把 `_running` 置回 true。
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConfigurableProducerConsumer<T>));

            if (_stopped)
                throw new InvalidOperationException(
                    "ConfigurableProducerConsumer 已停止：Stop() 会永久关闭内部 channel，之后不能再 StartAsync，请新建实例。");

            _running = true;

            // 重置计数器
            _producedCount = 0;
            _consumedCount = 0;

            var tasks = new List<Task>();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                // 创建并启动生产者任务
                for (int i = 0; i < producerCount; i++)
                {
                    int producerId = i + 1;
                    // 为每个生产者创建一个包装函数，捕获索引
                    int index = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await ProduceAsync(producerId, () => produceFunc(index), cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            // 任务被取消是正常的，忽略异常
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Producer-{producerId}] 发生异常: {ex.Message}");
                        }
                    }, cts.Token));
                }

                // 创建并启动消费者任务
                for (int i = 0; i < consumerCount; i++)
                {
                    int consumerId = i + 1;
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await ConsumeAsync(consumerId, consumeAction, cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            // 任务被取消是正常的，忽略异常
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Consumer-{consumerId}] 发生异常: {ex.Message}");
                        }
                    }, cts.Token));
                }

                // 等待所有任务完成
                await Task.WhenAll(tasks);
            }
            finally
            {
                cts.Dispose();
            }
        }

        /// <summary>
        /// 停止生产者消费者模式
        /// </summary>
        /// <remarks>
        /// ⛔ <b>终态操作</b>：内部 channel 会被 <c>Complete()</c> 永久关闭且**不可重开**
        /// ⇒ 之后再调 <see cref="StartAsync"/> 会抛 <see cref="InvalidOperationException"/>（序 72）。
        /// </remarks>
        public void Stop()
        {
            if (!_disposed && _running)
            {
                _running = false;
                _stopped = true;   // ⛔ 序 72：标记 channel 已永久关闭，StartAsync 据此拒绝重启
                _writer.Complete();
            }
        }

        /// <summary>
        /// 异步停止生产者消费者模式
        /// </summary>
        public async Task StopAsync()
        {
            Stop();
            // 等待所有未完成的操作完成
            try
            {
                await _reader.Completion.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 忽略取消异常
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在 disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Stop();
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 优先级队列生产者消费者模式实现
    /// </summary>
    /// <typeparam name="T">队列中存储的数据类型</typeparam>
    public class PriorityQueueProducerConsumer<T> : IProducerConsumer<T>
    {
        private readonly ConcurrentQueue<PriorityItem<T>> _priorityQueue;
        private readonly SemaphoreSlim _semaphore;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private volatile bool _running = true;
        private bool _disposed = false;

        // 生产和消费统计
        private long _producedCount = 0;
        private long _consumedCount = 0;

        /// <summary>
        /// 获取已生产的项目数量
        /// </summary>
        public long ProducedCount => _producedCount;

        /// <summary>
        /// 获取已消费的项目数量
        /// </summary>
        public long ConsumedCount => _consumedCount;

        /// <summary>
        /// 初始化优先级队列生产者消费者模式
        /// </summary>
        public PriorityQueueProducerConsumer()
        {
            _priorityQueue = new ConcurrentQueue<PriorityItem<T>>();
            _semaphore = new SemaphoreSlim(0);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// 生产带有优先级的数据项
        /// </summary>
        /// <param name="item">数据项</param>
        /// <param name="priority">优先级（数字越大优先级越高）</param>
        public void Produce(T item, int priority)
        {
            if (!_running)
                throw new InvalidOperationException("生产者消费者模式已停止");

            var priorityItem = new PriorityItem<T>(item, priority);
            _priorityQueue.Enqueue(priorityItem);
            _semaphore.Release();
            Interlocked.Increment(ref _producedCount);
        }

        /// <summary>
        /// 消费数据项（按优先级顺序）
        /// </summary>
        /// <param name="consumeAction">消费动作</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task ConsumeAsync(Action<T> consumeAction, CancellationToken cancellationToken = default)
        {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _cancellationTokenSource.Token);

            try
            {
                while (_running && !linkedCts.Token.IsCancellationRequested)
                {
                    // 等待有数据可用
                    await _semaphore.WaitAsync(linkedCts.Token);

                    if (_running && TryDequeueHighestPriorityItem(out var item))
                    {
                        consumeAction(item.Data);
                        Interlocked.Increment(ref _consumedCount);

                        // 减少日志输出频率以提高性能
                        long count = _consumedCount;
                        if (count % 1000 == 0)
                        {
                            Console.WriteLine($"[PriorityConsumer] Consumed item #{count} with priority {item.Priority}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 任务被取消是正常的，忽略异常
            }
            finally
            {
                linkedCts.Dispose();
            }
        }

        /// <summary>
        /// 尝试获取优先级最高的数据项
        /// </summary>
        private bool TryDequeueHighestPriorityItem(out PriorityItem<T> highestPriorityItem)
        {
            var items = new List<PriorityItem<T>>();
            PriorityItem<T> currentItem;

            // 将所有项目取出并排序
            while (_priorityQueue.TryDequeue(out currentItem))
            {
                items.Add(currentItem);
            }

            if (items.Count > 0)
            {
                // 按优先级排序
                items.Sort();

                // 返回优先级最高的项目
                highestPriorityItem = items[0];

                // 将其余项目重新放入队列
                for (int i = 1; i < items.Count; i++)
                {
                    _priorityQueue.Enqueue(items[i]);
                }

                // 更新信号量计数
                if (items.Count > 1)
                {
                    _semaphore.Release(items.Count - 1);
                }

                return true;
            }

            highestPriorityItem = null;
            return false;
        }

        /// <summary>
        /// 启动消费者任务
        /// </summary>
        /// <param name="consumerCount">消费者数量</param>
        /// <param name="consumeAction">消费动作</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task StartConsumersAsync(int consumerCount, Action<T> consumeAction, CancellationToken cancellationToken = default)
        {
            if (consumerCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(consumerCount), "消费者数量必须大于0");

            if (consumeAction == null)
                throw new ArgumentNullException(nameof(consumeAction));

            var tasks = new Task[consumerCount];

            for (int i = 0; i < consumerCount; i++)
            {
                tasks[i] = ConsumeAsync(consumeAction, cancellationToken);
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 停止生产者消费者模式
        /// </summary>
        public void Stop()
        {
            if (!_disposed && _running)
            {
                _running = false;
                _cancellationTokenSource.Cancel();
                _semaphore.Release(); // 释放信号量以唤醒等待的消费者
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在 disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Stop();
                    _semaphore?.Dispose();
                    _cancellationTokenSource?.Dispose();
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// 启动生产者消费者模式
        /// </summary>
        /// <param name="producerCount">生产者数量</param>
        /// <param name="consumerCount">消费者数量</param>
        /// <param name="produceFunc">生产函数，用于生成要放入队列的数据</param>
        /// <param name="consumeAction">消费动作，用于处理从队列中取出的数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task StartAsync(int producerCount, int consumerCount, Func<int, T> produceFunc, Action<T> consumeAction, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("PriorityQueueProducerConsumer不支持此方法，请使用StartConsumersAsync方法");
        }

        /// <summary>
        /// 异步停止生产者消费者模式
        /// </summary>
        public async Task StopAsync()
        {
            await Task.Run(() => { Stop(); });
        }
    }
}