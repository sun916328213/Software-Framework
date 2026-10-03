// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：DelayEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Diagnostics、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.UI.Controls
{
    /// <summary>
    /// DelayEx 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// </summary>
    public static class DelayEx
    {
        /// <summary>
        /// 延时同步
        /// </summary>
        /// <param name="milliseconds">毫秒</param>
        public static void Delay(int milliseconds)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < milliseconds)
            {
                // 使用Dispatcher让出时间给UI线程进行绘制
                // 注意：这需要在WPF应用程序中使用
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() => { }));
              
                // 短暂睡眠让出CPU时间片
                System.Threading.Thread.Sleep(1);
            }
        }

        /// <summary>
        /// 延时异步
        /// </summary>
        /// <param name="milliseconds">毫秒</param>
        /// <returns></returns>
        public static async Task DelayAsync(int milliseconds)
        {
            await Task.Delay(milliseconds);
        }
    }
}
