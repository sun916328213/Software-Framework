// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：ChannelHelper。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Threading、System.Threading.Channels、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Extension.Core.Concurrency
{
    /// <summary>
    /// Channel管理器，封装Channel的创建和管理
    /// </summary>
    /// <typeparam name="T">通道中传输的数据类型</typeparam>
    public class ChannelHelper<T> : IDisposable
    {
        private readonly Channel<T> _channel;
        private readonly ChannelWriter<T> _writer;
        private readonly ChannelReader<T> _reader;
        private volatile bool _isCompleted = false;
        private bool _disposed = false;

        /// <summary>
        /// 初始化Channel管理器
        /// </summary>
        /// <param name="capacity">通道容量，默认为100</param>
        /// <param name="fullMode">通道满时的行为，默认为等待</param>
        public ChannelHelper(int capacity = 100, BoundedChannelFullMode fullMode = BoundedChannelFullMode.DropOldest)
        {
            _channel = System.Threading.Channels.Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
            {
                FullMode = fullMode,
                SingleWriter=false,// 允许多个生产者（多个设备同时写入）
                SingleReader = false,// 允许多个消费者（并行处理回调）
            });
            _writer = _channel.Writer;
            _reader = _channel.Reader;
        }

        /// <summary>
        /// 写入数据到通道
        /// </summary>
        /// <param name="item">要写入的数据</param>
        public void Write(T item)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ChannelHelper<T>));

            if (_isCompleted)
                throw new InvalidOperationException("通道已完成，无法写入新数据");

            try
            {
                _writer.TryWrite(item);
            }
            catch (OperationCanceledException)
            {
                // 通道已完成，忽略异常
            }
        }

        /// <summary>
        /// 异步写入数据到通道
        /// </summary>
        /// <param name="item">要写入的数据</param>
        /// <returns>表示异步写入操作的任务</returns>
        public async   Task WriteAsync(T item)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ChannelHelper<T>));

            if (_isCompleted)
                throw new InvalidOperationException("通道已完成，无法写入新数据");

            try
            {
                await _writer.WriteAsync(item);
            }
            catch (OperationCanceledException)
            {
                // 通道已完成，忽略异常
            }
        }

        /// <summary>
        /// 清空通道中的所有数据
        /// </summary>
        /// <returns>被清除的数据项数量</returns>
        public int Clear()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ChannelHelper<T>));

            if (_isCompleted)
                throw new InvalidOperationException("通道已完成，无法清空数据");

            int count = 0;
            // 尝试读取并丢弃所有可用数据
            while (_reader.TryRead(out _))
            {
                count++;
            }
            
            return count;
        }

        /// <summary>
        /// 异步清空通道中的所有数据
        /// </summary>
        /// <returns>被清除的数据项数量</returns>
        public async Task<int> ClearAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ChannelHelper<T>));

            if (_isCompleted)
                throw new InvalidOperationException("通道已完成，无法清空数据");

            int count = 0;
            // 尝试读取并丢弃所有可用数据
            while (await _reader.WaitToReadAsync())
            {
                while (_reader.TryRead(out _))
                {
                    count++;
                }
            }
            
            return count;
        }

        /// <summary>
        /// 异步读取通道中的所有数据
        /// </summary>
        /// <returns>异步迭代器</returns>
        public ChannelReader<T> Reader => _reader;

        /// <summary>
        /// 异步等待通道中有数据可读
        /// </summary>
        /// <returns>如果有数据可读返回true，否则返回false</returns>
        public Task<bool> WaitToReadAsync() => _reader.WaitToReadAsync().AsTask();

        /// <summary>
        /// 尝试从通道中读取数据并移除
        /// </summary>
        /// <param name="item">读取到的数据</param>
        /// <returns>如果成功读取数据返回true，否则返回false</returns>
        public bool TryRead(out T item) => _reader.TryRead(out item);

        /// <summary>
        /// 尝试从通道中读取数据 
        /// </summary>
        /// <returns></returns>
        public Task<T> TryReadAsync() => _reader.ReadAsync().AsTask();

        /// <summary>
        /// 完成通道写入端，表示不再有新数据写入
        /// </summary>
        public void Complete()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ChannelHelper<T>));

            if (!_isCompleted)
            {
                _isCompleted = true;
                _writer.TryComplete();
            }
        }

        /// <summary>
        /// 检查通道是否已完成（无更多数据）
        /// </summary>
        public bool IsCompleted => _reader.Completion.IsCompleted;

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
                    // 完成通道写入端
                    if (!_isCompleted)
                    {
                        _writer.Complete();
                    }
                }

                _disposed = true;
            }
        }
    }
}