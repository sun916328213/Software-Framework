namespace MotionControl.Runtime.Vendors.ZMotion
{
    /// <summary>正运动控制器原生 SDK seam；实现负责持有和释放原生句柄。</summary>
    internal interface IZMotionTransport : IDisposable
    {
        bool IsConnected { get; }
        bool Connect(string ipAddress, uint port);
        void Disconnect();
        bool ConfigureAxis(int axis);
        bool SetMotionProfile(int axis, float speed, float acceleration, float deceleration);
        bool MoveAbsolute(int axis, long pulse);
        bool MoveRelative(int axis, long pulse);
        bool Jog(int axis, int direction);
        bool Cancel(int axis, int mode);
        bool Datum(int axis, int mode);
        bool SetCommandPosition(int axis, float pulse);
        bool TryGetCommandPosition(int axis, out float pulse);
        bool TryGetFeedbackPosition(int axis, out float pulse);
        bool TryGetAxisStatus(int axis, out int status);
        bool TryGetIdle(int axis, out int idle);
        bool TryGetInput(int index, out uint value);
        bool TryGetOutput(int index, out uint value);
        bool SetOutput(int index, uint value);
    }

    /// <summary>通过 zauxdll 调用真实控制器的生产适配器。</summary>
    internal sealed class ZMotionNativeTransport : IZMotionTransport
    {
        private IntPtr _handle;

        public bool IsConnected => _handle != IntPtr.Zero;

        public bool Connect(string ipAddress, uint port)
        {
            Disconnect();
            var result = ZAux.ZAux_OpenEth(ipAddress, port, out _handle);
            if (result == 0 && _handle != IntPtr.Zero)
                return true;

            _handle = IntPtr.Zero;
            return false;
        }

        public void Disconnect()
        {
            if (_handle == IntPtr.Zero)
                return;
            ZAux.ZAux_Close(_handle);
            _handle = IntPtr.Zero;
        }

        public bool ConfigureAxis(int axis)
            => ZAux.ZAux_Direct_SetUnits(_handle, axis, 1) == 0;

        public bool SetMotionProfile(int axis, float speed, float acceleration, float deceleration)
            => ZAux.ZAux_Direct_SetSpeed(_handle, axis, speed) == 0 &&
               ZAux.ZAux_Direct_SetAccel(_handle, axis, acceleration) == 0 &&
               ZAux.ZAux_Direct_SetDecel(_handle, axis, deceleration) == 0;

        public bool MoveAbsolute(int axis, long pulse)
            => ZAux.ZAux_Direct_Single_Amove(_handle, axis, pulse) == 0;

        public bool MoveRelative(int axis, long pulse)
            => ZAux.ZAux_Direct_Single_Move(_handle, axis, pulse) == 0;

        public bool Jog(int axis, int direction)
            => ZAux.ZAux_Direct_Single_Vmove(_handle, axis, direction) == 0;

        public bool Cancel(int axis, int mode)
            => ZAux.ZAux_Direct_Single_Cancel(_handle, axis, mode) == 0;

        public bool Datum(int axis, int mode)
            => ZAux.ZAux_Direct_Single_Datum(_handle, axis, mode) == 0;

        public bool SetCommandPosition(int axis, float pulse)
            => ZAux.ZAux_Direct_SetDpos(_handle, axis, pulse) == 0;

        public bool TryGetCommandPosition(int axis, out float pulse)
        {
            pulse = 0;
            return ZAux.ZAux_Direct_GetDpos(_handle, axis, ref pulse) == 0;
        }

        public bool TryGetFeedbackPosition(int axis, out float pulse)
        {
            pulse = 0;
            return ZAux.ZAux_Direct_GetMpos(_handle, axis, ref pulse) == 0;
        }

        public bool TryGetAxisStatus(int axis, out int status)
        {
            status = 0;
            return ZAux.ZAux_Direct_GetAxisStatus(_handle, axis, ref status) == 0;
        }

        public bool TryGetIdle(int axis, out int idle)
        {
            idle = 0;
            return ZAux.ZAux_Direct_GetIfIdle(_handle, axis, ref idle) == 0;
        }

        public bool TryGetInput(int index, out uint value)
        {
            value = 0;
            return ZAux.ZAux_Direct_GetIn(_handle, index, ref value) == 0;
        }

        public bool TryGetOutput(int index, out uint value)
        {
            value = 0;
            return ZAux.ZAux_Direct_GetOp(_handle, index, ref value) == 0;
        }

        public bool SetOutput(int index, uint value)
            => ZAux.ZAux_Direct_SetOp(_handle, index, value) == 0;

        public void Dispose() => Disconnect();
    }
}

