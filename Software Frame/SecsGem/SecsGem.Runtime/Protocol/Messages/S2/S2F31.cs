// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S2F31。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core.Order、SecsGem.Runtime.Order、Secs4Net、System、System.Globalization、System.Linq、System.Runtime.InteropServices。导入不代表所有类型都会在运行时加载。
using SecsGem.Core.Order;
using SecsGem.Runtime.Order;
using Secs4Net;
using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace SecsGem.Runtime.Order.S2
{
    /// <summary>
    /// 日期时间设置 Date and Time Set H->E（E5 S2F31，请求体就是 <c>TIME</c>），回 S2F32 <c>TIACK</c>。
    /// <para>
    /// TIACK：0=OK、1=ERROR。格式不认识、或设置系统时间失败，都回 1。
    /// 设备端就跑在 PC 上，对时直接落在 Windows 本地时钟（原生 <c>SetLocalTime</c>，不经上层包装），
    /// 于是 S2F17、S6F1 的 STIME 天然跟着对上。
    /// </para>
    /// <para>
    /// 设置系统时间要求进程令牌启用 <c>SeSystemtimePrivilege</c>。管理员令牌里这条特权是**存在但默认禁用**的，
    /// 所以这里每次都先显式启用一次（<see cref="EnableSystemTimePrivilege"/>）再调用 Win32；
    /// 非管理员令牌里根本没有该特权，启用不会成功，<c>SetLocalTime</c> 会以
    /// ERROR_PRIVILEGE_NOT_HELD(1314) 失败——错误码记进 SEMILog，Host 侧只看到 TIACK=1。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 指令格式示例：设置本机 Windows 时间，有系统级副作用，仅作报文说明，不自动执行。
    /// <code><![CDATA[
    /// 'S2F31'W                        // Host→设备，修改 Windows 本地时间
    /// <A [16] '2026010212345600'>      // TIME：yyyyMMddHHmmssff，不套 List
    /// .
    /// 'S2F32'                         // 设备→Host，对时应答
    /// <B [1] 0x00>                    // TIACK：0=成功，1=格式/权限/系统调用失败
    /// .
    /// ]]></code>
    /// 请求为裸 ASCII TIME，不套 List；TIACK=0 设置成功、1 格式/权限/系统调用失败。实际支持的时间格式见 TimeFormats。
    /// </remarks>
    [SecsOrder("S2F31")]
    class S2F31 : SecsOrderBase
    {
        /// <summary>
        /// 认可的 TIME 写法：本设备发出用 16 位 <c>yyyyMMddHHmmssff</c>，
        /// 同时接受 GEM 常见的 12 位 <c>yyMMddHHmmss</c> 与带毫秒的 17 位写法（各家 Host 不统一）。
        /// </summary>
        private static readonly string[] TimeFormats =
        {
            "yyyyMMddHHmmssff", "yyyyMMddHHmmssfff", "yyyyMMddHHmmss",
            "yyMMddHHmmssff",   "yyMMddHHmmssfff",   "yyMMddHHmmss",
        };

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 SecsItemToList、FirstOrDefault、TryParseExact、EnableSystemTimePrivilege、SetLocalTime、B；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage)
        {
            try
            {
                // 请求体是裸 ASCII 项（不是列表），SecsItemToList 会把它当单元素列表平铺出来。
                var time = SecsItemToList(pSecsMessage.SecsItem)?.FirstOrDefault();

                if (!DateTime.TryParseExact(time, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    return Item.B(1); //TIACK=1：格式不认识

                // TIME 就是设备本地时间，直接用 SetLocalTime，不必再经 TimeZoneInfo 换算。
                var local = new SYSTEMTIME
                {
                    wYear = (ushort)parsed.Year,
                    wMonth = (ushort)parsed.Month,
                    wDay = (ushort)parsed.Day,
                    wHour = (ushort)parsed.Hour,
                    wMinute = (ushort)parsed.Minute,
                    wSecond = (ushort)parsed.Second,
                    wMilliseconds = (ushort)parsed.Millisecond,
                };

                // 先尽力启用特权：管理员令牌里这条特权"存在但默认禁用"，不启用则必然失败。
                // 非管理员令牌里根本没有它，启用会返回 1300（ERROR_NOT_ALL_ASSIGNED）——
                // 把这个码一并写进日志，现场才能区分"没以管理员/服务运行"与"Win32 其他失败"。
                int privilegeCode = EnableSystemTimePrivilege();

                if (SetLocalTime(ref local))
                    return Item.B(0); //TIACK=0：已设置

                int win32 = Marshal.GetLastWin32Error();
                string cause = privilegeCode == ERROR_NOT_ALL_ASSIGNED
                    ? "令牌里没有 SeSystemtimePrivilege（未以管理员/服务身份运行，且运行账号未被授予\"更改系统时间\"用户权限）"
                    : $"内嵌的 AdjustTokenPrivileges 返回 {privilegeCode}";
                SecsGemRuntime?.RaiseErrorPublic($"S2F31 设置系统时间失败：Win32 {win32}；{cause}；TIACK=1");
                return Item.B(1);
            }
            catch (Exception ex)
            {
                return Fail(ex);  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
        }

        /// <summary>
        /// 尽力为当前进程令牌启用 <c>SeSystemtimePrivilege</c>，返回 <c>AdjustTokenPrivileges</c> 的 Win32 错误码：
        /// 0 = 启用成功；1300(ERROR_NOT_ALL_ASSIGNED) = 令牌里根本没有这条特权。失败**不抛**。
        /// <para>
        /// 注意 <c>AdjustTokenPrivileges</c> 在令牌缺特权时是"返回 true 但 GetLastError=1300"，
        /// 所以必须显式读错误码，否则现场分不清"没以管理员运行"和"启用失败"。
        /// </para>
        /// </summary>
        private static int EnableSystemTimePrivilege()
        {
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY | TOKEN_ADJUST_PRIVILEGES, out IntPtr token))
                return Marshal.GetLastWin32Error();

            try
            {
                if (!LookupPrivilegeValue(null, SeSystemtimePrivilegeName, out LUID luid))
                    return Marshal.GetLastWin32Error();

                // 只带一条特权：native 的 TOKEN_PRIVILEGES 是 DWORD + LUID_AND_ATTRIBUTES[1] 的定长展开。
                var privileges = new TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Luid = luid,
                    Attributes = SE_PRIVILEGE_ENABLED,
                };

                // 不关心旧状态，PreviousState/ReturnLength 传 null。
                AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero);
                return Marshal.GetLastWin32Error();
            }
            finally
            {
                CloseHandle(token);
            }
        }

        private const string SeSystemtimePrivilegeName = "SeSystemtimePrivilege";
        private const uint TOKEN_QUERY = 0x0008;
        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
        private const int ERROR_NOT_ALL_ASSIGNED = 1300;

        /// <summary>Win32 SYSTEMTIME：字段类型与顺序必须与 native 一致（P/Invoke 按内存布局封送）。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEMTIME
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;   // 设置时由系统忽略
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }

        /// <summary>Win32 LUID（本地唯一标识，这里用来指认特权）。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        /// <summary>Win32 TOKEN_PRIVILEGES，定长展开成"1 条特权"。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID Luid;
            public uint Attributes;
        }

        /// <summary>设置本地系统时间；失败返回 false，错误码用 <see cref="Marshal.GetLastWin32Error"/> 取。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetLocalTime(ref SYSTEMTIME lpSystemTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LookupPrivilegeValue(string systemName, string name, out LUID luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(
            IntPtr tokenHandle,
            bool disableAllPrivileges,
            ref TOKEN_PRIVILEGES newState,
            uint bufferLength,
            IntPtr previousState,
            IntPtr returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
