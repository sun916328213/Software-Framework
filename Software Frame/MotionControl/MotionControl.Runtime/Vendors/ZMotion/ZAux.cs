// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：ZAux。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Runtime.InteropServices。导入不代表所有类型都会在运行时加载。
using System.Runtime.InteropServices;

namespace MotionControl.Runtime.Vendors.ZMotion
{
    /// <summary>
    /// 正运动ZMotion函数库P/Invoke声明（zauxdll.dll，依赖同目录zmotion.dll）
    /// <para>签名依据《ZMotion PC函数库编程手册V2.1》</para>
    /// <para>关键语义：</para>
    /// <para>- 全部ZAux函数返回0表示成功（int32）</para>
    /// <para>- ZAux_Direct_GetIfIdle返回IDLE：-1=轴运动中，0=轴停止</para>
    /// <para>- DATUM回零模式：3/4原点反离开沿 5/6原点+EZ 8/9碰原点即停 13限位反找原点（详见ZBasic手册DATUM指令）</para>
    /// </summary>
    internal static class ZAux
    {
        private const string DllName = "zauxdll.dll";

        #region 连接
        /// <summary>以太网连接控制器（port默认502）</summary>
        [DllImport(DllName, EntryPoint = "ZAux_OpenEth", CharSet = CharSet.Ansi)]
        public static extern int ZAux_OpenEth(string ipaddr, uint port, out IntPtr phandle);

        /// <summary>断开连接</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Close")]
        public static extern int ZAux_Close(IntPtr handle);
        #endregion

        #region 轴参数
        /// <summary>设置脉冲当量UNITS（每用户单位对应脉冲数；本封装固定为1，即直接以脉冲为单位交互）</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetUnits")]
        public static extern int ZAux_Direct_SetUnits(IntPtr handle, int iaxis, float fValue);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ZAux_Direct_SetSpeed。
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetSpeed")]
        public static extern int ZAux_Direct_SetSpeed(IntPtr handle, int iaxis, float fValue);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ZAux_Direct_SetAccel。
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetAccel")]
        public static extern int ZAux_Direct_SetAccel(IntPtr handle, int iaxis, float fValue);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ZAux_Direct_SetDecel。
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetDecel")]
        public static extern int ZAux_Direct_SetDecel(IntPtr handle, int iaxis, float fValue);
        #endregion

        #region 单轴运动
        /// <summary>单轴绝对运动</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_Single_Amove")]
        public static extern int ZAux_Direct_Single_Amove(IntPtr handle, int iaxis, float fdistance);

        /// <summary>单轴相对运动</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_Single_Move")]
        public static extern int ZAux_Direct_Single_Move(IntPtr handle, int iaxis, float fdistance);

        /// <summary>单轴连续运动（点动）：fdirection 1正方向，-1负方向</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_Single_Vmove")]
        public static extern int ZAux_Direct_Single_Vmove(IntPtr handle, int iaxis, float fdirection);

        /// <summary>取消轴运动：imode 1=取消当前+缓冲，2=立即停止</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_Single_Cancel")]
        public static extern int ZAux_Direct_Single_Cancel(IntPtr handle, int iaxis, int imode);

        /// <summary>启动回零（DATUM指令）：imode见类注释回零模式表</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_Single_Datum")]
        public static extern int ZAux_Direct_Single_Datum(IntPtr handle, int iaxis, int imode);
        #endregion

        #region 状态监控
        /// <summary>读取轴指令位置DPOS</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetDpos")]
        public static extern int ZAux_Direct_GetDpos(IntPtr handle, int iaxis, ref float fValue);

        /// <summary>设置轴指令位置DPOS（用于位置清零/偏移）</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetDpos")]
        public static extern int ZAux_Direct_SetDpos(IntPtr handle, int iaxis, float fValue);

        /// <summary>读取轴反馈位置MPOS</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetMpos")]
        public static extern int ZAux_Direct_GetMpos(IntPtr handle, int iaxis, ref float fValue);

        /// <summary>读取轴是否空闲：返回值piValue -1=运动中，0=停止</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetIfIdle")]
        public static extern int ZAux_Direct_GetIfIdle(IntPtr handle, int iaxis, ref int piValue);

        /// <summary>读取轴状态字AXISSTATUS（位定义以所用固件手册为准）</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetAxisStatus")]
        public static extern int ZAux_Direct_GetAxisStatus(IntPtr handle, int iaxis, ref int piValue);
        #endregion

        #region IO控制
        /// <summary>读取输入点位电平：pValue 0/1</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetIn")]
        public static extern int ZAux_Direct_GetIn(IntPtr handle, int ionum, ref uint pValue);

        /// <summary>写输出点位电平：value 0/1</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetOp")]
        public static extern int ZAux_Direct_SetOp(IntPtr handle, int ionum, uint value);

        /// <summary>读输出点当前电平</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetOp")]
        public static extern int ZAux_Direct_GetOp(IntPtr handle, int ionum, ref uint pValue);
        #endregion

        #region 常量：轴状态字位定义（AXISSTATUS，具体以所用固件手册为准）
        /// <summary>跟随误差超限报警</summary>
        public const int ZMC_STS_FOLLOW_ERROR = 1 << 2;
        /// <summary>正限位触发</summary>
        public const int ZMC_STS_POS_LIMIT = 1 << 3;
        /// <summary>负限位触发</summary>
        public const int ZMC_STS_NEG_LIMIT = 1 << 4;
        /// <summary>急停触发</summary>
        public const int ZMC_STS_EMERGENCY = 1 << 6;

        /// <summary>取消当前运动与缓冲</summary>
        public const int ZMC_CANCEL_ALL = 1;
        /// <summary>立即停止当前运动与缓冲</summary>
        public const int ZMC_CANCEL_IMMEDIATE = 2;

        /// <summary>DATUM回零模式：碰到原点即停</summary>
        public const int ZMC_DATUM_ORIGIN_STOP = 8;
        /// <summary>DATUM回零模式：原点+EZ</summary>
        public const int ZMC_DATUM_ORIGIN_EZ = 5;
        /// <summary>DATUM回零模式：限位反找原点</summary>
        public const int ZMC_DATUM_LIMIT_HOME = 13;
        /// <summary>DATUM(0)：清除轴状态错误</summary>
        public const int ZMC_DATUM_CLEAR_STATUS = 0;
        #endregion
    }
}

