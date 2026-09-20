using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Platform.Processes
{
    public static class ProcessEx
    {
        public static  void CloseProcess(string processName)
        {
            // ⛔ 序 27（2026-09-18 修）：`Process.GetProcessesByName(null)` **既不抛异常、也不返回空数组**，
            // 而是返回**一批无关进程** ⇒ 紧接着的 `process.Kill()` 就真的执行了
            //（实测以 `Win32Exception: 拒绝访问` 收场，说明 Kill 确实被调用；以管理员身份运行时后果不可逆）。
            // 空字符串实测同样返回一批进程。⇒ 非法进程名必须在入口挡住，绝不能让它走到 Kill。
            if (processName == null)
                throw new ArgumentNullException(nameof(processName),
                    "进程名不能为 null：Process.GetProcessesByName(null) 会返回一批无关进程并逐个 Kill。");

            if (processName.Length == 0)
                throw new ArgumentException(
                    "进程名不能为空字符串：Process.GetProcessesByName(\"\") 同样会返回一批无关进程并逐个 Kill。",
                    nameof(processName));

            // 获取所有同名进程（进程名不含.exe）
            Process[] processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                return;
            }
            // 遍历并终止进程
            foreach (var process in processes)
            {
                process.Kill(); // 强制终止
                // 可选：等待进程退出（超时5秒）
                process.WaitForExit(1);
            }
        }
    }
}
