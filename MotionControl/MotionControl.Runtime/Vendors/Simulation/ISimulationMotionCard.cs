namespace MotionControl.Runtime.Vendors.Simulation
{
    /// <summary>
    /// 仿真卡测试钩子接口（SimulationMotionCard为internal，通过该接口访问仿真能力）
    /// </summary>
    public interface ISimulationMotionCard
    {
        /// <summary>
        /// 模拟设置输入点状态（如模拟限位开关、传感器信号）
        /// </summary>
        /// <param name="index">输入点索引，从0开始</param>
        /// <param name="state">输入状态</param>
        void SetSimInput(int index, bool state);
        /// <summary>
        /// 模拟触发轴报警并抛出AlarmOccurred事件
        /// </summary>
        /// <param name="axis">轴索引，从0开始</param>
        void TriggerSimAlarm(int axis);
    }
}

