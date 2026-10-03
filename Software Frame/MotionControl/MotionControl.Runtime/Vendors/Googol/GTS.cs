// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：GTS、TTrapPrm、TJogPrm、THomePrm、THomeStatus。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Runtime.InteropServices。导入不代表所有类型都会在运行时加载。
using System.Runtime.InteropServices;

namespace MotionControl.Runtime.Vendors.Googol
{
    /// <summary>
    /// 固高GTS系列函数库P/Invoke声明（gts.dll）
    /// <para>签名依据官方gts.h；轴号/规划器号从1开始，位掩码=1&lt;&lt;(轴号-1)</para>
    /// <para>关键语义：</para>
    /// <para>- GT_GetSts返回状态字bit10(0x400)=规划速度为零（轴停止）</para>
    /// <para>- GT_Stop(mask,option)：option 0=平滑停止，1=紧急停止</para>
    /// </summary>
    internal static class GTS
    {
        private const string DllName = "gts.dll";
        private const CallingConvention CC = CallingConvention.StdCall;

        #region 系统与初始化
        [DllImport(DllName, EntryPoint = "GT_Open", CallingConvention = CC)]
        public static extern short GT_Open(short channel, short param);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 GT_Close。
        /// </summary>
        [DllImport(DllName, EntryPoint = "GT_Close", CallingConvention = CC)]
        public static extern short GT_Close();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 GT_Reset。
        /// </summary>
        [DllImport(DllName, EntryPoint = "GT_Reset", CallingConvention = CC)]
        public static extern short GT_Reset();

        /// <summary>下载配置文件（gts.cfg，由固高配置工具生成），加载后需GT_ClrSts生效</summary>
        [DllImport(DllName, EntryPoint = "GT_LoadConfig", CharSet = CharSet.Ansi, CallingConvention = CC)]
        public static extern short GT_LoadConfig(string pFile);

        /// <summary>清除轴状态（驱动器报警、跟随误差越限、限位触发标志）</summary>
        [DllImport(DllName, EntryPoint = "GT_ClrSts", CallingConvention = CC)]
        public static extern short GT_ClrSts(short axis, short count);
        #endregion

        #region 伺服使能与运动
        [DllImport(DllName, EntryPoint = "GT_AxisOn", CallingConvention = CC)]
        public static extern short GT_AxisOn(short axis);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 GT_AxisOff。
        /// </summary>
        [DllImport(DllName, EntryPoint = "GT_AxisOff", CallingConvention = CC)]
        public static extern short GT_AxisOff(short axis);

        /// <summary>将轴设为点位(梯形)运动模式</summary>
        [DllImport(DllName, EntryPoint = "GT_PrfTrap", CallingConvention = CC)]
        public static extern short GT_PrfTrap(short profile);

        /// <summary>将轴设为点动(速度)模式</summary>
        [DllImport(DllName, EntryPoint = "GT_PrfJog", CallingConvention = CC)]
        public static extern short GT_PrfJog(short profile);

        /// <summary>设置点位模式运动参数</summary>
        [DllImport(DllName, EntryPoint = "GT_SetTrapPrm", CallingConvention = CC)]
        public static extern short GT_SetTrapPrm(short profile, ref TTrapPrm pPrm);

        /// <summary>设置点动模式运动参数</summary>
        [DllImport(DllName, EntryPoint = "GT_SetJogPrm", CallingConvention = CC)]
        public static extern short GT_SetJogPrm(short profile, ref TJogPrm pPrm);

        /// <summary>设置目标位置（pulse）</summary>
        [DllImport(DllName, EntryPoint = "GT_SetPos", CallingConvention = CC)]
        public static extern short GT_SetPos(short profile, int pos);

        /// <summary>设置目标速度（pulse/s，点动时负值表示负方向）</summary>
        [DllImport(DllName, EntryPoint = "GT_SetVel", CallingConvention = CC)]
        public static extern short GT_SetVel(short profile, double vel);

        /// <summary>启动运动（mask按位对应各轴：1<<(轴号-1)）</summary>
        [DllImport(DllName, EntryPoint = "GT_Update", CallingConvention = CC)]
        public static extern short GT_Update(long mask);

        /// <summary>停止指定轴（mask）：option 0=平滑停止，1=紧急停止</summary>
        [DllImport(DllName, EntryPoint = "GT_Stop", CallingConvention = CC)]
        public static extern short GT_Stop(long mask, long option);
        #endregion

        #region 状态与位置监控
        /// <summary>读取轴状态字（bit1驱动报警 bit3正限位 bit4负限位 bit10规划速度为零等，以所用型号手册为准）</summary>
        [DllImport(DllName, EntryPoint = "GT_GetSts", CallingConvention = CC)]
        public static extern short GT_GetSts(short axis, out int pSts, short count, IntPtr pClock);

        /// <summary>读取规划位置（pulse）</summary>
        [DllImport(DllName, EntryPoint = "GT_GetAxisPrfPos", CallingConvention = CC)]
        public static extern short GT_GetAxisPrfPos(short axis, out double pValue, short count, IntPtr pClock);

        /// <summary>读取编码器位置（pulse）</summary>
        [DllImport(DllName, EntryPoint = "GT_GetAxisEncPos", CallingConvention = CC)]
        public static extern short GT_GetAxisEncPos(short axis, out double pValue, short count, IntPtr pClock);

        /// <summary>轴位置清零（指令+编码器同步清零）</summary>
        [DllImport(DllName, EntryPoint = "GT_ZeroPos", CallingConvention = CC)]
        public static extern short GT_ZeroPos(short axis, short count);

        /// <summary>设置规划位置（用于位置修正/回零偏移）</summary>
        [DllImport(DllName, EntryPoint = "GT_SetPrfPos", CallingConvention = CC)]
        public static extern short GT_SetPrfPos(short profile, int prfPos);
        #endregion

        #region 回零（Smart Home）
        [DllImport(DllName, EntryPoint = "GT_GoHome", CallingConvention = CC)]
        public static extern short GT_GoHome(short axis, ref THomePrm pHomePrm);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 GT_GetHomeStatus。
        /// </summary>
        [DllImport(DllName, EntryPoint = "GT_GetHomeStatus", CallingConvention = CC)]
        public static extern short GT_GetHomeStatus(short axis, ref THomeStatus pHomeStatus);
        #endregion

        #region IO
        /// <summary>写输出点位：doType用MC_GPO(12)，value 0/1</summary>
        [DllImport(DllName, EntryPoint = "GT_SetDoBit", CallingConvention = CC)]
        public static extern short GT_SetDoBit(short doType, short doIndex, short value);

        /// <summary>读输入口整组电平：diType用MC_GPI(4)</summary>
        [DllImport(DllName, EntryPoint = "GT_GetDi", CallingConvention = CC)]
        public static extern short GT_GetDi(short diType, ref long pValue);

        /// <summary>读输出口整组当前电平：doType用MC_GPO(12)</summary>
        [DllImport(DllName, EntryPoint = "GT_GetDo", CallingConvention = CC)]
        public static extern short GT_GetDo(short doType, ref long pValue);
        #endregion

        #region 结构体
        /// <summary>点位(梯形)运动参数</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct TTrapPrm
        {
            public double acc;         // 加速度 pulse/ms²
            public double dec;         // 减速度 pulse/ms²
            public double velStart;    // 起跳速度 pulse/s
            public short smoothTime;   // 平滑时间 ms
        }

        /// <summary>点动运动参数</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct TJogPrm
        {
            public double acc;
            public double dec;
            public double smooth;
        }

        /// <summary>回零参数（Smart Home）</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct THomePrm
        {
            public short mode;                  // 回零模式（见HOME_MODE_xxx常量）
            public short moveDir;               // 搜索启动方向
            public short indexDir;              // Index搜索方向
            public short edge;                  // 捕获沿
            public short triggerIndex;          // 触发器索引，-1表示与轴号对应
            public short pad1_0, pad1_1, pad1_2;// 保留
            public double velHigh;              // 搜索原点速度
            public double velLow;               // 搜索Index速度
            public double acc;
            public double dec;
            public short smoothTime;
            public short pad2_0, pad2_1, pad2_2;// 保留
            public int homeOffset;              // 原点偏移
            public int searchHomeDistance;      // 原点最大搜索距离，0不限制
            public int searchIndexDistance;     // Index最大搜索距离，0不限制
            public int escapeStep;              // 脱离步长
            public int pad3_0, pad3_1;          // 保留
        }

        /// <summary>回零状态</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct THomeStatus
        {
            public short run;       // 运行标志
            public short stage;     // 执行阶段（HOME_STAGE_END=100表示完成）
            public short error;     // 错误码（HOME_ERROR_NONE=0）
            public short pad1;
            public int capturePos;  // 捕获位置
            public int targetPos;   // 目标位置
        }
        #endregion

        #region 常量：轴状态字（GT_GetSts返回值，具体以所用型号手册为准）
        /// <summary>驱动器报警</summary>
        public const int GTS_STS_ALARM = 1 << 1;
        /// <summary>正限位触发</summary>
        public const int GTS_STS_POS_LIMIT = 1 << 3;
        /// <summary>负限位触发</summary>
        public const int GTS_STS_NEG_LIMIT = 1 << 4;
        /// <summary>规划速度为零（轴处于停止态）</summary>
        public const int GTS_STS_ZERO_VEL = 1 << 10;
        /// <summary>伺服使能开启状态</summary>
        public const int GTS_STS_SERVO_ON = 1 << 11;
        #endregion

        #region 常量：回零模式（THomePrm.mode）
        /// <summary>限位开关回零</summary>
        public const short HOME_MODE_LIMIT = 10;
        /// <summary>限位+原点回零</summary>
        public const short HOME_MODE_LIMIT_HOME = 11;
        /// <summary>原点开关回零</summary>
        public const short HOME_MODE_HOME = 20;
        /// <summary>原点+Index回零</summary>
        public const short HOME_MODE_HOME_INDEX = 22;
        /// <summary>Index回零</summary>
        public const short HOME_MODE_INDEX = 30;
        /// <summary>回零完成阶段值（THomeStatus.stage）</summary>
        public const short HOME_STAGE_END = 100;
        /// <summary>回零无错误（THomeStatus.error）</summary>
        public const short HOME_ERROR_NONE = 0;
        #endregion

        #region 常量：IO类型索引
        /// <summary>通用数字输入类型</summary>
        public const short MC_GPI = 4;
        /// <summary>通用数字输出类型</summary>
        public const short MC_GPO = 12;
        #endregion
    }
}

