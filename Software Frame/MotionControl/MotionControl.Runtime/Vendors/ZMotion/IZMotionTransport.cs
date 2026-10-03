// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：IZMotionTransport、ZMotionNativeTransport。成员说明可结合下方具体实现阅读。
namespace MotionControl.Runtime.Vendors.ZMotion
{
    /// <summary>正运动控制器原生 SDK seam；实现负责持有和释放原生句柄。</summary>
    internal interface IZMotionTransport : IDisposable
    {
        bool IsConnected { get; }
        /// <summary>
        /// 发起连接操作；对应入口为 Connect。
        /// </summary>
        bool Connect(string ipAddress, uint port);
        /// <summary>
        /// 执行断开连接操作；对应入口为 Disconnect。
        /// </summary>
        void Disconnect();
        /// <summary>
        /// 配置当前对象或组合入口；对应入口为 ConfigureAxis。
        /// </summary>
        bool ConfigureAxis(int axis);
        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetMotionProfile。
        /// </summary>
        bool SetMotionProfile(int axis, float speed, float acceleration, float deceleration);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MoveAbsolute。
        /// </summary>
        bool MoveAbsolute(int axis, long pulse);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MoveRelative。
        /// </summary>
        bool MoveRelative(int axis, long pulse);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Jog。
        /// </summary>
        bool Jog(int axis, int direction);
        /// <summary>
        /// 请求取消当前操作；对应入口为 Cancel。
        /// </summary>
        bool Cancel(int axis, int mode);
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Datum。
        /// </summary>
        bool Datum(int axis, int mode);
        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetCommandPosition。
        /// </summary>
        bool SetCommandPosition(int axis, float pulse);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetCommandPosition。
        /// </summary>
        bool TryGetCommandPosition(int axis, out float pulse);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetFeedbackPosition。
        /// </summary>
        bool TryGetFeedbackPosition(int axis, out float pulse);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetAxisStatus。
        /// </summary>
        bool TryGetAxisStatus(int axis, out int status);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetIdle。
        /// </summary>
        bool TryGetIdle(int axis, out int idle);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetInput。
        /// </summary>
        bool TryGetInput(int index, out uint value);
        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetOutput。
        /// </summary>
        bool TryGetOutput(int index, out uint value);
        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetOutput。
        /// </summary>
        bool SetOutput(int index, uint value);
    }

    /// <summary>通过 zauxdll 调用真实控制器的生产适配器。</summary>
    internal sealed class ZMotionNativeTransport : IZMotionTransport
    {
        private IntPtr _handle;

        public bool IsConnected => _handle != IntPtr.Zero;

        /// <summary>
        /// 发起连接操作；对应入口为 Connect。
        /// 正文中的关键协作调用包括 Disconnect、ZAux_OpenEth；具体条件和先后次序见方法体。
        /// </summary>
        public bool Connect(string ipAddress, uint port)
        {
            Disconnect();
            var result = ZAux.ZAux_OpenEth(ipAddress, port, out _handle);
            if (result == 0 && _handle != IntPtr.Zero)
                return true;

            _handle = IntPtr.Zero;
            return false;
        }

        /// <summary>
        /// 执行断开连接操作；对应入口为 Disconnect。
        /// 正文中的关键协作调用包括 ZAux_Close；具体条件和先后次序见方法体。
        /// </summary>
        public void Disconnect()
        {
            if (_handle == IntPtr.Zero)
                return;
            ZAux.ZAux_Close(_handle);
            _handle = IntPtr.Zero;
        }

        /// <summary>
        /// 配置当前对象或组合入口；对应入口为 ConfigureAxis。
        /// 正文中的关键协作调用包括 ZAux_Direct_SetUnits；具体条件和先后次序见方法体。
        /// </summary>
        public bool ConfigureAxis(int axis)
            => ZAux.ZAux_Direct_SetUnits(_handle, axis, 1) == 0;

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetMotionProfile。
        /// 正文中的关键协作调用包括 ZAux_Direct_SetSpeed、ZAux_Direct_SetAccel、ZAux_Direct_SetDecel；具体条件和先后次序见方法体。
        /// </summary>
        public bool SetMotionProfile(int axis, float speed, float acceleration, float deceleration)
            => ZAux.ZAux_Direct_SetSpeed(_handle, axis, speed) == 0 &&
               ZAux.ZAux_Direct_SetAccel(_handle, axis, acceleration) == 0 &&
               ZAux.ZAux_Direct_SetDecel(_handle, axis, deceleration) == 0;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MoveAbsolute。
        /// 正文中的关键协作调用包括 ZAux_Direct_Single_Amove；具体条件和先后次序见方法体。
        /// </summary>
        public bool MoveAbsolute(int axis, long pulse)
            => ZAux.ZAux_Direct_Single_Amove(_handle, axis, pulse) == 0;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MoveRelative。
        /// 正文中的关键协作调用包括 ZAux_Direct_Single_Move；具体条件和先后次序见方法体。
        /// </summary>
        public bool MoveRelative(int axis, long pulse)
            => ZAux.ZAux_Direct_Single_Move(_handle, axis, pulse) == 0;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Jog。
        /// 正文中的关键协作调用包括 ZAux_Direct_Single_Vmove；具体条件和先后次序见方法体。
        /// </summary>
        public bool Jog(int axis, int direction)
            => ZAux.ZAux_Direct_Single_Vmove(_handle, axis, direction) == 0;

        /// <summary>
        /// 请求取消当前操作；对应入口为 Cancel。
        /// 正文中的关键协作调用包括 ZAux_Direct_Single_Cancel；具体条件和先后次序见方法体。
        /// </summary>
        public bool Cancel(int axis, int mode)
            => ZAux.ZAux_Direct_Single_Cancel(_handle, axis, mode) == 0;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Datum。
        /// 正文中的关键协作调用包括 ZAux_Direct_Single_Datum；具体条件和先后次序见方法体。
        /// </summary>
        public bool Datum(int axis, int mode)
            => ZAux.ZAux_Direct_Single_Datum(_handle, axis, mode) == 0;

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetCommandPosition。
        /// 正文中的关键协作调用包括 ZAux_Direct_SetDpos；具体条件和先后次序见方法体。
        /// </summary>
        public bool SetCommandPosition(int axis, float pulse)
            => ZAux.ZAux_Direct_SetDpos(_handle, axis, pulse) == 0;

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetCommandPosition。
        /// 正文中的关键协作调用包括 ZAux_Direct_GetDpos；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryGetCommandPosition(int axis, out float pulse)
        {
            pulse = 0;
            return ZAux.ZAux_Direct_GetDpos(_handle, axis, ref pulse) == 0;
        }

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetFeedbackPosition。
        /// 正文中的关键协作调用包括 ZAux_Direct_GetMpos；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryGetFeedbackPosition(int axis, out float pulse)
        {
            pulse = 0;
            return ZAux.ZAux_Direct_GetMpos(_handle, axis, ref pulse) == 0;
        }

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetAxisStatus。
        /// 正文中的关键协作调用包括 ZAux_Direct_GetAxisStatus；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryGetAxisStatus(int axis, out int status)
        {
            status = 0;
            return ZAux.ZAux_Direct_GetAxisStatus(_handle, axis, ref status) == 0;
        }

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetIdle。
        /// 正文中的关键协作调用包括 ZAux_Direct_GetIfIdle；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryGetIdle(int axis, out int idle)
        {
            idle = 0;
            return ZAux.ZAux_Direct_GetIfIdle(_handle, axis, ref idle) == 0;
        }

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetInput。
        /// 正文中的关键协作调用包括 ZAux_Direct_GetIn；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryGetInput(int index, out uint value)
        {
            value = 0;
            return ZAux.ZAux_Direct_GetIn(_handle, index, ref value) == 0;
        }

        /// <summary>
        /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryGetOutput。
        /// 正文中的关键协作调用包括 ZAux_Direct_GetOp；具体条件和先后次序见方法体。
        /// </summary>
        public bool TryGetOutput(int index, out uint value)
        {
            value = 0;
            return ZAux.ZAux_Direct_GetOp(_handle, index, ref value) == 0;
        }

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetOutput。
        /// 正文中的关键协作调用包括 ZAux_Direct_SetOp；具体条件和先后次序见方法体。
        /// </summary>
        public bool SetOutput(int index, uint value)
            => ZAux.ZAux_Direct_SetOp(_handle, index, value) == 0;

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 Disconnect；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose() => Disconnect();
    }
}

