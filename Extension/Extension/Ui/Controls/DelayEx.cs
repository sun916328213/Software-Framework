using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.UI.Controls
{
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
