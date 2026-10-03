// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：ILeadshineTransport、LeadshineNativeTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Core.Definitions。导入不代表所有类型都会在运行时加载。
using MotionControl.Core.Definitions;

namespace MotionControl.Runtime.Vendors.Leadshine
{
    /// <summary>雷赛 LTDMC 原生 SDK seam；实现负责进程级库生命周期和当前卡号。</summary>
    internal interface ILeadshineTransport : IDisposable
    {
        bool IsOpen { get; }
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Open。
        /// </summary>
        EMotionResult Open(ushort cardNumber, int requiredAxes);
        /// <summary>
        /// 执行关闭步骤；对应入口为 Close。
        /// </summary>
        void Close();
        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetProfile。
        /// </summary>
        bool SetProfile(int axis, double minimumVelocity, double maximumVelocity,
            double accelerationTime, double decelerationTime, double stopVelocity);
        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetServo。
        /// </summary>
        bool SetServo(int axis, bool enabled);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MovePosition。
        /// </summary>
        bool MovePosition(int axis, int pulse, bool absolute);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MoveVelocity。
        /// </summary>
        bool MoveVelocity(int axis, bool positive);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Stop。
        /// </summary>
        bool Stop(int axis, bool emergency);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 EmergencyStopAll。
        /// </summary>
        bool EmergencyStopAll();
        /// <summary>
        /// 配置当前对象或组合入口；对应入口为 ConfigureHome。
        /// </summary>
        bool ConfigureHome(int axis, ushort direction, double velocity, ushort mode,
            bool enableLimitReturn, int? offsetPulse);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StartHome。
        /// </summary>
        bool StartHome(int axis);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetHomeResult。
        /// </summary>
        bool TryGetHomeResult(int axis, out ushort state);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 CheckDone。
        /// </summary>
        short CheckDone(int axis);
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetAxisIoStatus。
        /// </summary>
        uint GetAxisIoStatus(int axis);
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetCommandPosition。
        /// </summary>
        int GetCommandPosition(int axis);
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetEncoderPosition。
        /// </summary>
        int GetEncoderPosition(int axis);
        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetCurrentPosition。
        /// </summary>
        bool SetCurrentPosition(int axis, int pulse);
        /// <summary>
        /// 读取当前实现提供的数据；对应入口为 ReadInput。
        /// </summary>
        short ReadInput(int index);
        /// <summary>
        /// 读取当前实现提供的数据；对应入口为 ReadOutput。
        /// </summary>
        short ReadOutput(int index);
        /// <summary>
        /// 执行写入操作；对应入口为 WriteOutput。
        /// </summary>
        bool WriteOutput(int index, bool state);
    }

    /// <summary>通过 LTDMC.dll 调用真实板卡的生产适配器。</summary>
    internal sealed class LeadshineNativeTransport : ILeadshineTransport
    {
        private static readonly object LibrarySync = new();
        private static int _openCount;
        private ushort _cardNumber;

        public bool IsOpen { get; private set; }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Open。
        /// 正文中的关键协作调用包括 dmc_board_init、dmc_get_CardInfList、Contains、Take、CloseFailedOpen；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public EMotionResult Open(ushort cardNumber, int requiredAxes)
        {
            lock (LibrarySync)
            {
                if (IsOpen)
                    return EMotionResult.Success;

                var initializedHere = false;
                if (_openCount == 0)
                {
                    if (LTDMC.dmc_board_init() <= 0)
                        return EMotionResult.Fail;
                    initializedHere = true;
                }

                ushort cardCount = 0;
                var typeList = new uint[16];
                var idList = new ushort[16];
                if (LTDMC.dmc_get_CardInfList(ref cardCount, typeList, idList) != 0 ||
                    !idList.Take(cardCount).Contains(cardNumber))
                {
                    CloseFailedOpen(initializedHere);
                    return EMotionResult.Fail;
                }

                uint totalAxes = 0;
                if (LTDMC.dmc_get_total_axes(cardNumber, ref totalAxes) != 0 ||
                    requiredAxes <= 0 || requiredAxes > totalAxes)
                {
                    CloseFailedOpen(initializedHere);
                    return EMotionResult.InvalidParam;
                }

                _cardNumber = cardNumber;
                IsOpen = true;
                _openCount++;
                return EMotionResult.Success;
            }
        }

        /// <summary>
        /// 执行关闭步骤；对应入口为 Close。
        /// 正文中的关键协作调用包括 dmc_board_close；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public void Close()
        {
            lock (LibrarySync)
            {
                if (!IsOpen)
                    return;

                IsOpen = false;
                if (_openCount > 0)
                    _openCount--;
                if (_openCount == 0)
                    LTDMC.dmc_board_close();
            }
        }

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetProfile。
        /// 正文中的关键协作调用包括 dmc_set_profile；具体条件和先后次序见方法体。
        /// </summary>
        public bool SetProfile(int axis, double minimumVelocity, double maximumVelocity,
            double accelerationTime, double decelerationTime, double stopVelocity)
            => LTDMC.dmc_set_profile(_cardNumber, (ushort)axis, minimumVelocity, maximumVelocity,
                accelerationTime, decelerationTime, stopVelocity) == 0;

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetServo。
        /// 正文中的关键协作调用包括 dmc_write_sevon_pin；具体条件和先后次序见方法体。
        /// </summary>
        public bool SetServo(int axis, bool enabled)
            => LTDMC.dmc_write_sevon_pin(_cardNumber, (ushort)axis,
                enabled ? (ushort)0 : (ushort)1) == 0;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MovePosition。
        /// 正文中的关键协作调用包括 dmc_pmove；具体条件和先后次序见方法体。
        /// </summary>
        public bool MovePosition(int axis, int pulse, bool absolute)
            => LTDMC.dmc_pmove(_cardNumber, (ushort)axis, pulse, absolute ? (ushort)1 : (ushort)0) == 0;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MoveVelocity。
        /// 正文中的关键协作调用包括 dmc_vmove；具体条件和先后次序见方法体。
        /// </summary>
        public bool MoveVelocity(int axis, bool positive)
            => LTDMC.dmc_vmove(_cardNumber, (ushort)axis, positive ? (ushort)1 : (ushort)0) == 0;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Stop。
        /// 正文中的关键协作调用包括 dmc_stop；具体条件和先后次序见方法体。
        /// </summary>
        public bool Stop(int axis, bool emergency)
            => LTDMC.dmc_stop(_cardNumber, (ushort)axis, emergency ? (ushort)1 : (ushort)0) == 0;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 EmergencyStopAll。
        /// 正文中的关键协作调用包括 dmc_emg_stop；具体条件和先后次序见方法体。
        /// </summary>
        public bool EmergencyStopAll() => LTDMC.dmc_emg_stop(_cardNumber) == 0;

        /// <summary>
        /// 配置当前对象或组合入口；对应入口为 ConfigureHome。
        /// 正文中的关键协作调用包括 dmc_set_homemode、dmc_set_home_el_return、dmc_set_home_position；具体条件和先后次序见方法体。
        /// </summary>
        public bool ConfigureHome(int axis, ushort direction, double velocity, ushort mode,
            bool enableLimitReturn, int? offsetPulse)
        {
            if (LTDMC.dmc_set_homemode(_cardNumber, (ushort)axis, direction, velocity, mode, 0) != 0)
                return false;
            if (LTDMC.dmc_set_home_el_return(_cardNumber, (ushort)axis,
                    enableLimitReturn ? (ushort)1 : (ushort)0) != 0)
                return false;
            return !offsetPulse.HasValue ||
                LTDMC.dmc_set_home_position(_cardNumber, (ushort)axis, 1, offsetPulse.Value) == 0;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 StartHome。
        /// 正文中的关键协作调用包括 dmc_home_move；具体条件和先后次序见方法体。
        /// </summary>
        public bool StartHome(int axis) => LTDMC.dmc_home_move(_cardNumber, (ushort)axis) == 0;

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetHomeResult。
        /// 正文中的关键协作调用包括 dmc_get_home_result；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryGetHomeResult(int axis, out ushort state)
        {
            state = 0;
            return LTDMC.dmc_get_home_result(_cardNumber, (ushort)axis, ref state) == 0;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 CheckDone。
        /// 正文中的关键协作调用包括 dmc_check_done；具体条件和先后次序见方法体。
        /// </summary>
        public short CheckDone(int axis) => LTDMC.dmc_check_done(_cardNumber, (ushort)axis);

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetAxisIoStatus。
        /// 正文中的关键协作调用包括 dmc_axis_io_status；具体条件和先后次序见方法体。
        /// </summary>
        public uint GetAxisIoStatus(int axis) => LTDMC.dmc_axis_io_status(_cardNumber, (ushort)axis);

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetCommandPosition。
        /// 正文中的关键协作调用包括 dmc_get_position；具体条件和先后次序见方法体。
        /// </summary>
        public int GetCommandPosition(int axis) => LTDMC.dmc_get_position(_cardNumber, (ushort)axis);

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetEncoderPosition。
        /// 正文中的关键协作调用包括 dmc_get_encoder；具体条件和先后次序见方法体。
        /// </summary>
        public int GetEncoderPosition(int axis) => LTDMC.dmc_get_encoder(_cardNumber, (ushort)axis);

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetCurrentPosition。
        /// 正文中的关键协作调用包括 dmc_set_position、dmc_set_encoder；具体条件和先后次序见方法体。
        /// </summary>
        public bool SetCurrentPosition(int axis, int pulse)
            => LTDMC.dmc_set_position(_cardNumber, (ushort)axis, pulse) == 0 &&
                LTDMC.dmc_set_encoder(_cardNumber, (ushort)axis, pulse) == 0;

        /// <summary>
        /// 读取当前实现提供的数据；对应入口为 ReadInput。
        /// 正文中的关键协作调用包括 dmc_read_inbit；具体条件和先后次序见方法体。
        /// </summary>
        public short ReadInput(int index) => LTDMC.dmc_read_inbit(_cardNumber, (ushort)index);

        /// <summary>
        /// 读取当前实现提供的数据；对应入口为 ReadOutput。
        /// 正文中的关键协作调用包括 dmc_read_outbit；具体条件和先后次序见方法体。
        /// </summary>
        public short ReadOutput(int index) => LTDMC.dmc_read_outbit(_cardNumber, (ushort)index);

        /// <summary>
        /// 执行写入操作；对应入口为 WriteOutput。
        /// 正文中的关键协作调用包括 dmc_write_outbit；具体条件和先后次序见方法体。
        /// </summary>
        public bool WriteOutput(int index, bool state)
            => LTDMC.dmc_write_outbit(_cardNumber, (ushort)index, (ushort)(state ? 1 : 0)) == 0;

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 Close；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose() => Close();

        /// <summary>
        /// 执行关闭步骤；对应入口为 CloseFailedOpen。
        /// 正文中的关键协作调用包括 dmc_board_close；具体条件和先后次序见方法体。
        /// </summary>
        private static void CloseFailedOpen(bool initializedHere)
        {
            if (initializedHere && _openCount == 0)
                LTDMC.dmc_board_close();
        }
    }
}

