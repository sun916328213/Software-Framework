// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：MotionParameterValidator。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Core.Definitions。导入不代表所有类型都会在运行时加载。
using MotionControl.Core.Definitions;

namespace MotionControl.Runtime.Internal
{
    /// <summary>连接前校验所有驱动共享的运动参数约束。</summary>
    internal static class MotionParameterValidator
    {
        private const double Int64ExclusiveUpperBound = 9223372036854775808d;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Validate。
        /// 正文中的关键协作调用包括 Invalid、IsNullOrWhiteSpace、ValidateAxis、Success；具体条件和先后次序见方法体。
        /// </summary>
        public static MotionResult Validate(MotionCardParam? parameter)
        {
            if (parameter == null)
                return Invalid("运动控制卡参数不能为空。");
            if (parameter.CardNo < 0)
                return Invalid("卡号不能小于 0。");
            if (parameter.Axises == null || parameter.Axises.Count == 0)
                return Invalid("至少需要配置一个轴。");
            if (!string.IsNullOrWhiteSpace(parameter.IpAddress) &&
                (parameter.TcpPort <= 0 || parameter.TcpPort > ushort.MaxValue))
                return Invalid("TCP 端口必须在 1 到 65535 之间。");

            for (var axis = 0; axis < parameter.Axises.Count; axis++)
            {
                var config = parameter.Axises[axis];
                if (config == null)
                    return Invalid($"轴 {axis} 的参数不能为空。");

                var result = ValidateAxis(axis, config);
                if (!result.IsSuccess)
                    return result;
            }

            return MotionResult.Success();
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ValidateAxis。
        /// 正文中的关键协作调用包括 IsFinitePositive、Invalid、IsFiniteNonNegative、IsFinite、CanConvertToPulse；具体条件和先后次序见方法体。
        /// </summary>
        private static MotionResult ValidateAxis(int axis, AxisParam config)
        {
            if (!IsFinitePositive(config.PulseEquivalent))
                return Invalid($"轴 {axis} 的脉冲当量必须是有限正数。");
            if (!IsFiniteNonNegative(config.VelLow))
                return Invalid($"轴 {axis} 的启动速度必须是有限非负数。");
            if (!IsFinitePositive(config.VelHigh))
                return Invalid($"轴 {axis} 的运行速度必须是有限正数。");
            if (config.VelLow > config.VelHigh)
                return Invalid($"轴 {axis} 的启动速度不能大于运行速度。");
            if (!IsFinitePositive(config.Accel) || !IsFinitePositive(config.Decel))
                return Invalid($"轴 {axis} 的加速度和减速度必须是有限正数。");
            if (!double.IsFinite(config.HomeOffset))
                return Invalid($"轴 {axis} 的回零偏移必须是有限数值。");
            if (config.HomeMode is not EHomeMode.None and not EHomeMode.SetCurrentAsOrigin &&
                !IsFinitePositive(config.HomeVelocity))
                return Invalid($"轴 {axis} 的回零速度必须是有限正数。");
            if (config.EnableSoftLimit &&
                (!double.IsFinite(config.SoftLimitNegative) ||
                 !double.IsFinite(config.SoftLimitPositive) ||
                 config.SoftLimitNegative >= config.SoftLimitPositive))
                return Invalid($"轴 {axis} 的负软限位必须小于正软限位。");
            if (!CanConvertToPulse(config.VelLow, config.PulseEquivalent) ||
                !CanConvertToPulse(config.VelHigh, config.PulseEquivalent) ||
                !CanConvertToPulse(config.HomeOffset, config.PulseEquivalent) ||
                (config.HomeMode is not EHomeMode.None and not EHomeMode.SetCurrentAsOrigin &&
                 !CanConvertToPulse(config.HomeVelocity, config.PulseEquivalent)) ||
                (config.EnableSoftLimit &&
                 (!CanConvertToPulse(config.SoftLimitNegative, config.PulseEquivalent) ||
                  !CanConvertToPulse(config.SoftLimitPositive, config.PulseEquivalent))))
                return Invalid($"轴 {axis} 的参数换算为脉冲后超出 Int64 范围。");

            return MotionResult.Success();
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 IsFinitePositive。
        /// 正文中的关键协作调用包括 IsFinite；具体条件和先后次序见方法体。
        /// </summary>
        private static bool IsFinitePositive(double value) => double.IsFinite(value) && value > 0;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 IsFiniteNonNegative。
        /// 正文中的关键协作调用包括 IsFinite；具体条件和先后次序见方法体。
        /// </summary>
        private static bool IsFiniteNonNegative(double value) => double.IsFinite(value) && value >= 0;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 CanConvertToPulse。
        /// 正文中的关键协作调用包括 Round、IsFinite；具体条件和先后次序见方法体。
        /// </summary>
        private static bool CanConvertToPulse(double value, double pulseEquivalent)
        {
            var scaled = Math.Round(value * pulseEquivalent);
            return double.IsFinite(scaled) && scaled >= long.MinValue && scaled < Int64ExclusiveUpperBound;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Invalid。
        /// 正文中的关键协作调用包括 Failure；具体条件和先后次序见方法体。
        /// </summary>
        private static MotionResult Invalid(string message)
            => MotionResult.Failure(EMotionResult.InvalidParam, message);
    }
}

