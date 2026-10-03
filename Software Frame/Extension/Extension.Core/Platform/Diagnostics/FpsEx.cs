// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：FpsEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Diagnostics。导入不代表所有类型都会在运行时加载。
using System;
using System.Diagnostics;

namespace Extension.Platform.Diagnostics
{
    /// <summary>
    /// 帧率计算器，用于计算和跟踪帧率信息
    /// </summary>
    public class FpsEx
    {
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private int _frameCount = 0;
        private double _currentFps = 0;
        private readonly object _lockObject = new object();
        private readonly int _calculationInterval;

        /// <summary>
        /// 获取当前帧率
        /// </summary>
        public double CurrentFps
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentFps;
                }
            }
        }

        /// <summary>
        /// 获取总帧数
        /// </summary>
        public int TotalFrameCount { get; private set; }

        /// <summary>
        /// 初始化帧率计算器
        /// </summary>
        /// <param name="calculationInterval">计算间隔（帧数），默认10帧计算一次</param>
        public FpsEx(int calculationInterval = 10)
        {
            _calculationInterval = calculationInterval > 0 ? calculationInterval : 10;
            _stopwatch.Start();
        }

        /// <summary>
        /// 记录一帧，更新帧率计算
        /// </summary>
        public void Tick()
        {
            lock (_lockObject)
            {
                _frameCount++;
                TotalFrameCount++;

                // 达到计算间隔时更新FPS
                if (_frameCount >= _calculationInterval)
                {
                    if (_stopwatch.IsRunning)
                    {
                        var elapsed = _stopwatch.ElapsedMilliseconds;
                        if (elapsed > 0)
                        {
                            _currentFps = (_frameCount * 1000.0) / elapsed;
                        }
                    }

                    // 重置计数器和计时器
                    _frameCount = 0;
                    _stopwatch.Restart();
                }
            }
        }

        /// <summary>
        /// 重置帧率计算
        /// </summary>
        public void Reset()
        {
            lock (_lockObject)
            {
                _frameCount = 0;
                TotalFrameCount = 0;
                _currentFps = 0;
                _stopwatch.Restart();
            }
        }

        /// <summary>
        /// 停止计时器
        /// </summary>
        public void Stop()
        {
            lock (_lockObject)
            {
                _stopwatch.Stop();
            }
        }

        /// <summary>
        /// 开始计时器
        /// </summary>
        public void Start()
        {
            lock (_lockObject)
            {
                _stopwatch.Start();
            }
        }

        /// <summary>
        /// 获取格式化的FPS字符串
        /// </summary>
        /// <param name="format">格式化字符串，默认为"F1"</param>
        /// <returns>格式化的FPS字符串</returns>
        public string GetFormattedFps(string format = "F1")
        {
            return $"FPS: {CurrentFps.ToString(format)}";
        }

        /// <summary>
        /// 获取详细的帧率信息
        /// </summary>
        /// <param name="format">格式化字符串，默认为"F1"</param>
        /// <returns>详细的帧率信息字符串</returns>
        public string GetDetailedFpsInfo(string format = "F1")
        {
            return $"FPS: {CurrentFps.ToString(format)} (Total: {TotalFrameCount})";
        }
    }
}