using System.Collections.ObjectModel;

namespace MotionControl.Definition
{
    /// <summary>一次批量操作中单个设备的执行结果。</summary>
    public readonly record struct MotionDeviceResult(int DeviceIndex, string DeviceName, MotionResult Result);

    /// <summary>保持设备顺序的批量运动操作结果。</summary>
    public sealed class MotionBatchResult
    {
        private readonly ReadOnlyCollection<MotionDeviceResult> _results;

        internal MotionBatchResult(IEnumerable<MotionDeviceResult> results)
        {
            ArgumentNullException.ThrowIfNull(results);
            _results = Array.AsReadOnly(results.ToArray());
        }

        /// <summary>每个设备的执行结果，顺序与管理器设备集合一致。</summary>
        public IReadOnlyList<MotionDeviceResult> Results => _results;

        /// <summary>所有设备是否均执行成功；空集合视为成功。</summary>
        public bool IsSuccess => _results.All(item => item.Result.IsSuccess);

        /// <summary>执行成功的设备数量。</summary>
        public int SuccessCount => _results.Count(item => item.Result.IsSuccess);

        /// <summary>执行失败的设备数量。</summary>
        public int FailureCount => _results.Count - SuccessCount;
    }
}
