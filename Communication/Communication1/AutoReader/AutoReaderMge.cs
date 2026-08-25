using Communication.Core.Interfaces;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Communication.Management
{

    public static class AutoReaderMge
    {
        private static readonly ConditionalWeakTable<Core.Interfaces.ICommunication, AutoReader> mRunners = new ConditionalWeakTable<Core.Interfaces.ICommunication, AutoReader>();

        /// <summary>
        /// 启动自动批量读取
        /// </summary>
        /// <param name="device"></param>
        /// <param name="options"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static void StartAutoRead(this Core.Interfaces.ICommunication device, AutoReadOptions options = null)
        {
            if (device == null) throw new ArgumentNullException(nameof(device));
            if (device is ICanAutoRead ar && !ar.CanAutoRead)
                throw new NotSupportedException($"{device.GetType().Name} 不支持自动读取。");

            var runner = mRunners.GetValue(device, d => new AutoReader(d, options));
            runner.Start();
        }
        /// <summary>
        /// 停止批量读取
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static async Task StopAutoRead(this Core.Interfaces.ICommunication device)
        {
            if (device == null) throw new ArgumentNullException(nameof(device));

            if (mRunners.TryGetValue(device, out var runner))
            {
                await runner.StopAsync().ConfigureAwait(false);
            }
        }

        public static bool IsRunning(this Core.Interfaces.ICommunication device)
        {
            return device != null
                && mRunners.TryGetValue(device, out var runner)
                && runner.IsRunning;
        }

        public static int GetIntervalMs(this Core.Interfaces.ICommunication device)
        {
            if (device == null || !mRunners.TryGetValue(device, out var runner))
            {
                return -1;
            }
            else 
            {
                return runner.Options.IntervalMs;
            }
        }

        public static bool SetIntervalMs(this Core.Interfaces.ICommunication device,int val)
        {
            if (device == null || !mRunners.TryGetValue(device, out var runner))
            {
                return false;
            }
            else
            {
                runner.Options.IntervalMs = val;
                return true;
            }
        }

    }
}
