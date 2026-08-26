using System.Runtime.InteropServices;

namespace MotionControl.Vendors.Leadshine
{
    /// <summary>
    /// 雷赛LTDMC函数库P/Invoke声明（LTDMC.dll）
    /// <para>仅封装本项目用到的接口，适用于DMC3000等脉冲系列控制卡</para>
    /// <para>关键参数语义（依据雷赛官方手册及示例）：</para>
    /// <para>- dmc_pmove的posi_mode：0=相对运动，1=绝对运动</para>
    /// <para>- dmc_stop的stop_mode：0=减速停止，1=立即停止(急停)</para>
    /// <para>- dmc_vmove的dir：0=正方向，1=负方向</para>
    /// <para>- dmc_check_done返回值：0=运动中，1=已停止(到位)</para>
    /// <para>- dmc_get_home_result的state：1=回零完成，0=回零未完成</para>
    /// <para>- dmc_write_sevon_pin的on_off：SEVON信号低电平有效，0=使能，1=失能</para>
    /// </summary>
    internal static class LTDMC
    {
        private const string DllName = "LTDMC.dll";
        private const CallingConvention CC = CallingConvention.StdCall;

        #region 板卡初始化与配置
        /// <summary>初始化控制卡，返回已识别的卡数（&lt;=0表示失败或无卡）</summary>
        [DllImport(DllName, EntryPoint = "dmc_board_init", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_board_init();

        /// <summary>关闭控制卡</summary>
        [DllImport(DllName, EntryPoint = "dmc_board_close", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_board_close();

        /// <summary>读取控制卡信息列表（卡号是否存在）</summary>
        [DllImport(DllName, EntryPoint = "dmc_get_CardInfList", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_get_CardInfList(ref ushort CardNum, uint[] CardTypeList, ushort[] CardIdList);

        /// <summary>读取指定卡的轴数</summary>
        [DllImport(DllName, EntryPoint = "dmc_get_total_axes", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_get_total_axes(ushort CardNo, ref uint TotalAxis);
        #endregion

        #region 单轴运动
        /// <summary>设置速度曲线（Min_Vel起始速度、Max_Vel最高速度，单位pulse/s；Tacc/Tdec加减速时间，单位s；stop_vel停止速度pulse/s）</summary>
        [DllImport(DllName, EntryPoint = "dmc_set_profile", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_set_profile(ushort CardNo, ushort axis, double Min_Vel, double Max_Vel, double Tacc, double Tdec, double stop_vel);

        /// <summary>点位运动：posi_mode 0=相对，1=绝对</summary>
        [DllImport(DllName, EntryPoint = "dmc_pmove", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_pmove(ushort CardNo, ushort axis, int Dist, ushort posi_mode);

        /// <summary>连续速度运动（点动）：dir 0=正方向，1=负方向</summary>
        [DllImport(DllName, EntryPoint = "dmc_vmove", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_vmove(ushort CardNo, ushort axis, ushort dir);

        /// <summary>单轴停止：stop_mode 0=减速停止，1=立即停止</summary>
        [DllImport(DllName, EntryPoint = "dmc_stop", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_stop(ushort CardNo, ushort axis, ushort stop_mode);

        /// <summary>紧急停止本卡所有轴</summary>
        [DllImport(DllName, EntryPoint = "dmc_emg_stop", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_emg_stop(ushort CardNo);
        #endregion

        #region 回零
        /// <summary>
        /// 设置回原点模式
        /// </summary>
        /// <param name="home_dir">回零方向：0=负向，1=正向</param>
        /// <param name="vel">回零速度（pulse/s）</param>
        /// <param name="mode">回零模式：0一次回零 1一次回零加回找 2二次回零 3原点加同向EZ 5原点加反向EZ 6原点锁存…</param>
        /// <param name="EZ_count">锁存源：0锁存指令位置，1锁存编码器位置</param>
        [DllImport(DllName, EntryPoint = "dmc_set_homemode", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_set_homemode(ushort CardNo, ushort axis, ushort home_dir, double vel, ushort mode, ushort EZ_count);

        /// <summary>设置遇限位反找使能</summary>
        [DllImport(DllName, EntryPoint = "dmc_set_home_el_return", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_set_home_el_return(ushort CardNo, ushort axis, ushort enable);

        /// <summary>设置回零偏移量：enable=1时回零完成后自动偏移position（pulse）</summary>
        [DllImport(DllName, EntryPoint = "dmc_set_home_position", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_set_home_position(ushort CardNo, ushort axis, ushort enable, double position);

        /// <summary>启动回零</summary>
        [DllImport(DllName, EntryPoint = "dmc_home_move", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_home_move(ushort CardNo, ushort axis);

        /// <summary>读取回零执行状态：state 1=回零完成，0=回零未完成</summary>
        [DllImport(DllName, EntryPoint = "dmc_get_home_result", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_get_home_result(ushort CardNo, ushort axis, ref ushort state);
        #endregion

        #region 状态监控
        /// <summary>读取指定轴运动状态：返回0=运动中，1=已停止</summary>
        [DllImport(DllName, EntryPoint = "dmc_check_done", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_check_done(ushort CardNo, ushort axis);

        /// <summary>
        /// 读取指定轴运动信号状态字
        /// </summary>
        /// <returns>bit0:ALM报警 bit1:EL+正限位 bit2:EL-负限位 bit3:EMG急停 bit4:ORG原点（电平状态，详见对应型号手册）</returns>
        [DllImport(DllName, EntryPoint = "dmc_axis_io_status", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern uint dmc_axis_io_status(ushort CardNo, ushort axis);

        /// <summary>当前指令位置（pulse）</summary>
        [DllImport(DllName, EntryPoint = "dmc_get_position", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern int dmc_get_position(ushort CardNo, ushort axis);

        /// <summary>设置指令位置（用于位置清零）</summary>
        [DllImport(DllName, EntryPoint = "dmc_set_position", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_set_position(ushort CardNo, ushort axis, int current_position);

        /// <summary>编码器反馈位置（pulse）</summary>
        [DllImport(DllName, EntryPoint = "dmc_get_encoder", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern int dmc_get_encoder(ushort CardNo, ushort axis);

        /// <summary>设置编码器计数值（用于位置清零）</summary>
        [DllImport(DllName, EntryPoint = "dmc_set_encoder", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_set_encoder(ushort CardNo, ushort axis, int encoder_value);
        #endregion

        #region IO与伺服使能
        /// <summary>读取输入点位电平</summary>
        [DllImport(DllName, EntryPoint = "dmc_read_inbit", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_read_inbit(ushort CardNo, ushort bitno);

        /// <summary>读取输出点当前电平</summary>
        [DllImport(DllName, EntryPoint = "dmc_read_outbit", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_read_outbit(ushort CardNo, ushort bitno);

        /// <summary>写输出点电平：on_off 0=低电平，1=高电平</summary>
        [DllImport(DllName, EntryPoint = "dmc_write_outbit", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_write_outbit(ushort CardNo, ushort bitno, ushort on_off);

        /// <summary>输出SEVON伺服使能信号：低电平有效，0=使能，1=失能</summary>
        [DllImport(DllName, EntryPoint = "dmc_write_sevon_pin", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short dmc_write_sevon_pin(ushort CardNo, ushort axis, ushort on_off);
        #endregion

        #region 常量：轴IO状态字位定义（dmc_axis_io_status返回值）
        /// <summary>伺服报警</summary>
        public const uint LTS_IO_ALM = 1u << 0;
        /// <summary>正硬限位</summary>
        public const uint LTS_IO_PEL = 1u << 1;
        /// <summary>负硬限位</summary>
        public const uint LTS_IO_MEL = 1u << 2;
        /// <summary>急停输入</summary>
        public const uint LTS_IO_EMG = 1u << 3;
        /// <summary>原点信号电平</summary>
        public const uint LTS_IO_ORG = 1u << 4;
        #endregion
    }
}
