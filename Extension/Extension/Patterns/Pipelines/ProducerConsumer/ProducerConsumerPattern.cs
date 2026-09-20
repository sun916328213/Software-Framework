using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Extension.Patterns.Pipelines.ProducerConsumer
{
    /// <summary>
    /// 生产者消费者模式接口
    /// 定义生产者消费者模式的基本操作
    /// </summary>
    /// <typeparam name="T">队列中存储的数据类型</typeparam>
    public interface IProducerConsumer<T> : IDisposable
    {
        /// <summary>
        /// 获取已生产的项目数量
        /// </summary>
        long ProducedCount { get; }

        /// <summary>
        /// 获取已消费的项目数量
        /// </summary>
        long ConsumedCount { get; }

        /// <summary>
        /// 启动生产者消费者模式
        /// </summary>
        /// <param name="producerCount">生产者数量</param>
        /// <param name="consumerCount">消费者数量</param>
        /// <param name="produceFunc">生产函数，用于生成要放入队列的数据</param>
        /// <param name="consumeAction">消费动作，用于处理从队列中取出的数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task StartAsync(int producerCount, int consumerCount, Func<int, T> produceFunc, Action<T> consumeAction, CancellationToken cancellationToken = default);

        /// <summary>
        /// 停止生产者消费者模式
        /// </summary>
        void Stop();

        /// <summary>
        /// 异步停止生产者消费者模式
        /// </summary>
        Task StopAsync();
    }

    /// <summary>
    /// 泛型生产者消费者模式实现
    /// 基于System.Threading.Channels实现，支持多生产者、多消费者场景
    /// </summary>
    /// <typeparam name="T">队列中存储的数据类型</typeparam>
    public class ProducerConsumer<T> : IProducerConsumer<T>
    {
        private readonly Channel<T> _channel;
        private readonly ChannelWriter<T> _writer;
        private readonly ChannelReader<T> _reader;
        
        // 控制是否继续运行的标志
        private volatile bool _running = true;
        
        // 用于跟踪是否已释放资源
        private bool _disposed = false;

        // ⛔ 序 41（2026-09-18 修）：Stop() 会 _writer.Complete() 把 channel **永久关闭**，而 Channel 不可重开。
        // 用这个标志把"已停止"与"从未启动"区分开 —— _running 做不到（构造函数里它就是 true）。
        private volatile bool _stopped = false;

        // 生产和消费统计
        private long _producedCount = 0;
        private long _consumedCount = 0;

        /// <summary>
        /// 初始化生产者消费者模式
        /// </summary>
        /// <param name="capacity">通道容量，默认为100</param>
        /// <param name="fullMode">通道满时的行为，默认为等待</param>
        public ProducerConsumer(int capacity = 100, BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait)
        {
            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = fullMode
            };
            _channel = Channel.CreateBounded<T>(options);
            _writer = _channel.Writer;
            _reader = _channel.Reader;
        }

        /// <summary>
        /// 获取已生产的项目数量
        /// </summary>
        public long ProducedCount => _producedCount;

        /// <summary>
        /// 获取已消费的项目数量
        /// </summary>
        public long ConsumedCount => _consumedCount;

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
                    // 根据需要决定是否继续或抛出异常
                }
            }
        }

        /// <summary>
        /// 消费者方法
        /// </summary>
        /// <param name="consumerId">消费者ID</param>
        /// <param name="consumeAction">消费动作，用于处理从队列中取出的数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task ConsumeAsync(int consumerId, Action<T> consumeAction, CancellationToken cancellationToken = default)
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
                    // 根据需要决定是否继续或抛出异常
                }
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

            // ⛔ 序 41（2026-09-18 修）：Stop() 会 _writer.Complete()，而 Channel 一旦 Complete 就**永久关闭、不可重开**。
            // 原实现无条件把 _running 置回 true 再启动任务 ⇒ 生产者写已关闭的 channel（ChannelClosedException 被
            // catch (Exception) 吞掉后继续循环）、消费者 WaitToReadAsync 在已完成的 channel 上**立即返回 false**
            // ⇒ 两个循环同时空转，Task.WhenAll **永不返回**（只能靠取消令牌逃出来）。
            // ⇒ 已停止 / 已释放的实例必须先挡在门外，绝不能把 _running 置回 true。
            if (_disposed)
                throw new ObjectDisposedException(nameof(ProducerConsumer<T>));

            if (_stopped)
                throw new InvalidOperationException(
                    "ProducerConsumer 已停止：Stop() 会永久关闭内部 channel，之后不能再 StartAsync，请新建实例。");

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
                            throw;
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
                            throw;
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
        /// ⇒ 之后再调 <see cref="StartAsync"/> 会抛 <see cref="InvalidOperationException"/>（序 41）。
        /// </remarks>
        public void Stop()
        {
            if (!_disposed && _running)
            {
                _running = false;
                _stopped = true;   // ⛔ 序 41：标记 channel 已永久关闭，StartAsync 据此拒绝重启
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
    /// 高级生产者消费者模式实现
    /// 提供更多控制选项和功能
    /// </summary>
    /// <typeparam name="T">队列中存储的数据类型</typeparam>
    public class EnhancedProducerConsumer<T> : IProducerConsumer<T>
    {
        private readonly Channel<T> _channel;
        private readonly ChannelWriter<T> _writer;
        private readonly ChannelReader<T> _reader;
        
        // 控制是否继续运行的标志
        private volatile bool _running = true;
        
        // 用于跟踪是否已释放资源
        private bool _disposed = false;

        // ⛔ 序 72（2026-09-18 修）：与 `ProducerConsumer<T>` 的同型缺陷 —— `Stop()` 会 `_writer.Complete()`
        // 把 channel **永久关闭**，而 Channel 不可重开。用这个标志把"已停止"与"从未启动"区分开
        //（`_running` 做不到：构造函数里它就是 true）。
        private volatile bool _stopped = false;

        // 生产和消费统计
        private long _producedCount = 0;
        private long _consumedCount = 0;

        /// <summary>
        /// 获取或设置生产速率限制（毫秒）
        /// </summary>
        public int ProductionRateLimit { get; set; } = 0;

        /// <summary>
        /// 获取或设置消费速率限制（毫秒）
        /// </summary>
        public int ConsumptionRateLimit { get; set; } = 0;

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
        /// <param name="capacity">通道容量，默认为100</param>
        /// <param name="fullMode">通道满时的行为，默认为等待</param>
        public EnhancedProducerConsumer(int capacity = 100, BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait)
        {
            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = fullMode
            };
            _channel = Channel.CreateBounded<T>(options);
            _writer = _channel.Writer;
            _reader = _channel.Reader;
        }

        /// <summary>
        /// 生产数据项
        /// </summary>
        /// <param name="item">要生产的项</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task ProduceItemAsync(T item, CancellationToken cancellationToken = default)
        {
            if (!_running)
                throw new InvalidOperationException("生产者消费者模式已停止");

            await _writer.WriteAsync(item, cancellationToken);
            Interlocked.Increment(ref _producedCount);
        }

        /// <summary>
        /// 尝试生产数据项（非阻塞）
        /// </summary>
        /// <param name="item">要生产的项</param>
        /// <returns>如果成功生产返回true，否则返回false</returns>
        public bool TryProduceItem(T item)
        {
            if (!_running)
                return false;

            if (_writer.TryWrite(item))
            {
                Interlocked.Increment(ref _producedCount);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 消费数据项
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>消费的数据项，如果没有数据可消费则返回默认值</returns>
        public async Task<T> ConsumeItemAsync(CancellationToken cancellationToken = default)
        {
            if (!_running)
                throw new InvalidOperationException("生产者消费者模式已停止");

            if (await _reader.WaitToReadAsync(cancellationToken))
            {
                if (_reader.TryRead(out T item))
                {
                    Interlocked.Increment(ref _consumedCount);
                    return item;
                }
            }
            return default(T);
        }

        /// <summary>
        /// 尝试消费数据项（非阻塞）
        /// </summary>
        /// <param name="item">消费的数据项</param>
        /// <returns>如果成功消费返回true，否则返回false</returns>
        public bool TryConsumeItem(out T item)
        {
            if (!_running)
            {
                item = default(T);
                return false;
            }

            if (_reader.TryRead(out item))
            {
                Interlocked.Increment(ref _consumedCount);
                return true;
            }
            return false;
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

                    // 应用生产速率限制
                    if (ProductionRateLimit > 0)
                    {
                        await Task.Delay(ProductionRateLimit, cancellationToken);
                    }

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
        /// 消费者方法
        /// </summary>
        /// <param name="consumerId">消费者ID</param>
        /// <param name="consumeAction">消费动作，用于处理从队列中取出的数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task ConsumeAsync(int consumerId, Action<T> consumeAction, CancellationToken cancellationToken = default)
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

                            // 应用消费速率限制
                            if (ConsumptionRateLimit > 0)
                            {
                                await Task.Delay(ConsumptionRateLimit, cancellationToken);
                            }

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
                throw new ObjectDisposedException(nameof(EnhancedProducerConsumer<T>));

            if (_stopped)
                throw new InvalidOperationException(
                    "EnhancedProducerConsumer 已停止：Stop() 会永久关闭内部 channel，之后不能再 StartAsync，请新建实例。");

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
        /// 清空通道中的所有数据
        /// </summary>
        /// <returns>被清除的数据项数量</returns>
        public int Clear()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EnhancedProducerConsumer<T>));

            int count = 0;
            while (_reader.TryRead(out _))
            {
                count++;
            }
            
            return count;
        }

        /// <summary>
        /// 获取通道中等待处理的项目数量
        /// </summary>
        public int WaitingItemCount => _reader.Count;

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
}