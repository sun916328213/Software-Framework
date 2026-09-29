using System.Runtime.InteropServices;

namespace MotionControl.Runtime.Vendors.ADLINK
{
    /// <summary>
    /// 凌华APS函数库P/Invoke声明（APS168x64.dll / APS168.dll）
    /// <para>仅封装本项目用到的接口，签名依据《APS Function Library Reference》</para>
    /// </summary>
    internal static class APS168
    {
        /// <summary>
        /// 库名占位符，经NativeLibrary解析器按进程位数重定向到APS168x64.dll(64位)或APS168.dll(32位)
        /// </summary>
        private const string DllName = "APS168";

        static APS168()
        {
            NativeLibrary.SetDllImportResolver(typeof(APS168).Assembly, Resolve);
        }

        private static IntPtr Resolve(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName != DllName)
                return IntPtr.Zero;
            var name = Environment.Is64BitProcess ? "APS168x64.dll" : "APS168.dll";
            return NativeLibrary.TryLoad(name, out var handle) ? handle : IntPtr.Zero;
        }

        #region 系统与初始化
        /// <summary>初始化板卡（BoardID_InBits传入待初始化板卡号位掩码）</summary>
        [DllImport(DllName)]
        public static extern int APS_initial(ref int BoardID_InBits, int Mode);

        /// <summary>关闭所有板卡</summary>
        [DllImport(DllName)]
        public static extern int APS_close();

        /// <summary>加载MotionCreatorPro2导出的XML配置文件（须在APS_initial之前调用）</summary>
        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern int APS_load_param_from_file(string pXMLFile);

        /// <summary>获取指定板卡的起始全局轴ID与轴总数</summary>
        [DllImport(DllName)]
        public static extern int APS_get_first_axisId(int Board_ID, ref int StartAxisID, ref int TotalAxisNum);
        #endregion

        #region 轴参数
        /// <summary>设置轴参数</summary>
        [DllImport(DllName)]
        public static extern int APS_set_axis_param(int Axis_ID, int AXS_Param_No, int AXS_Param);

        /// <summary>读取轴参数</summary>
        [DllImport(DllName)]
        public static extern int APS_get_axis_param(int Axis_ID, int AXS_Param_No, ref int AXS_Param);
        #endregion

        #region 单轴运动
        /// <summary>绝对运动</summary>
        [DllImport(DllName)]
        public static extern int APS_absolute_move(int Axis_ID, int Position, int Max_Speed);

        /// <summary>相对运动</summary>
        [DllImport(DllName)]
        public static extern int APS_relative_move(int Axis_ID, int Distance, int Max_Speed);

        /// <summary>速度运动（正负号决定方向），用于点动</summary>
        [DllImport(DllName)]
        public static extern int APS_velocity_move(int Axis_ID, int Max_Speed);

        /// <summary>启动回零（回零参数由PRA_HOME_*轴参数决定）</summary>
        [DllImport(DllName)]
        public static extern int APS_home_move(int Axis_ID);

        /// <summary>减速停止</summary>
        [DllImport(DllName)]
        public static extern int APS_stop_move(int Axis_ID);

        /// <summary>立即停止（急停）</summary>
        [DllImport(DllName)]
        public static extern int APS_emg_stop(int Axis_ID);

        /// <summary>伺服使能/失能（1使能 0失能）</summary>
        [DllImport(DllName)]
        public static extern int APS_set_servo_on(int Axis_ID, int Servo_On);
        #endregion

        #region 状态监控
        /// <summary>获取指令位置（规划位置，脉冲）</summary>
        [DllImport(DllName)]
        public static extern int APS_get_command(int Axis_ID, ref int Command);

        /// <summary>设置指令位置（用于位置清零）</summary>
        [DllImport(DllName)]
        public static extern int APS_set_command(int Axis_ID, int Command);

        /// <summary>获取反馈位置（编码器位置，脉冲）</summary>
        [DllImport(DllName)]
        public static extern int APS_get_position(int Axis_ID, ref int Position);

        /// <summary>设置反馈位置（用于位置清零）</summary>
        [DllImport(DllName)]
        public static extern int APS_set_position(int Axis_ID, int Position);

        /// <summary>获取运动状态位（MTS_xxx，见APS_MotionStatus常量）</summary>
        [DllImport(DllName)]
        public static extern int APS_motion_status(int Axis_ID);

        /// <summary>获取运动IO状态位（MIO_xxx，见APS_IoStatus常量）</summary>
        [DllImport(DllName)]
        public static extern int APS_motion_io_status(int Axis_ID);
        #endregion

        #region DIO
        /// <summary>按组写数字输出（DO_Group为组号，DO_Data为该组32位数据）</summary>
        [DllImport(DllName)]
        public static extern int APS_write_d_output(int Board_ID, int DO_Group, int DO_Data);

        /// <summary>按组读数字输出当前状态</summary>
        [DllImport(DllName)]
        public static extern int APS_read_d_output(int Board_ID, int DO_Group, ref int DO_Data);

        /// <summary>按组读数字输入</summary>
        [DllImport(DllName)]
        public static extern int APS_read_d_input(int Board_ID, int DI_Group, ref int DI_Data);
        #endregion

        #region 常量：轴参数编号（PRA_xxx）
        /// <summary>回零模式</summary>
        public const int PRA_HOME_MODE = 0x10;
        /// <summary>回零方向（0正向 1负向）</summary>
        public const int PRA_HOME_DIR = 0x11;
        /// <summary>回零加减速</summary>
        public const int PRA_HOME_ACC = 0x13;
        /// <summary>回零起始速度</summary>
        public const int PRA_HOME_VS = 0x14;
        /// <summary>回零最大速度</summary>
        public const int PRA_HOME_VM = 0x15;
        /// <summary>运动加速度（pulse/ss）</summary>
        public const int PRA_ACC = 0x21;
        /// <summary>运动减速度（pulse/ss）</summary>
        public const int PRA_DEC = 0x22;
        /// <summary>最大运行速度（pulse/s）</summary>
        public const int PRA_MAX_VELOCITY = 0x31;
        #endregion

        #region 常量：运动状态位（APS_motion_status返回值）
        /// <summary>最大速度恒速中</summary>
        public const int MTS_VM = 1 << 1;
        /// <summary>加速中</summary>
        public const int MTS_ACC = 1 << 2;
        /// <summary>减速中</summary>
        public const int MTS_DEC = 1 << 3;
        /// <summary>运动完成</summary>
        public const int MTS_MDN = 1 << 5;
        /// <summary>回零运动中</summary>
        public const int MTS_HMV = 1 << 6;
        /// <summary>单轴运动中</summary>
        public const int MTS_SMV = 1 << 7;
        /// <summary>直线插补中</summary>
        public const int MTS_LIP = 1 << 8;
        /// <summary>圆弧插补中</summary>
        public const int MTS_CIP = 1 << 9;
        /// <summary>点表运动中</summary>
        public const int MTS_PMV = 1 << 11;
        /// <summary>点动中</summary>
        public const int MTS_JOG = 1 << 15;
        /// <summary>异常停止</summary>
        public const int MTS_ASTP = 1 << 16;

        /// <summary>判定"运动中"的状态位集合</summary>
        public const int MTS_MOVING_MASK = MTS_VM | MTS_ACC | MTS_DEC | MTS_HMV | MTS_SMV | MTS_LIP | MTS_CIP | MTS_PMV | MTS_JOG;
        #endregion

        #region 常量：运动IO状态位（APS_motion_io_status返回值）
        /// <summary>伺服报警</summary>
        public const int MIO_ALM = 1 << 0;
        /// <summary>正硬限位</summary>
        public const int MIO_PEL = 1 << 1;
        /// <summary>负硬限位</summary>
        public const int MIO_MEL = 1 << 2;
        /// <summary>原点信号</summary>
        public const int MIO_ORG = 1 << 3;
        /// <summary>紧急停止输入</summary>
        public const int MIO_EMG = 1 << 4;
        /// <summary>到位信号</summary>
        public const int MIO_INP = 1 << 6;
        /// <summary>正软限位</summary>
        public const int MIO_SPEL = 1 << 11;
        /// <summary>负软限位</summary>
        public const int MIO_SMEL = 1 << 12;
        #endregion

        /// <summary>错误码：无错误。APS全部函数返回0表示成功</summary>
        public const int ERR_NoError = 0;
        /// <summary>错误码：卡已初始化过（视为成功）</summary>
        public const int ERR_DeviceAlreadyInitialed = -7;
    }
}

