using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.SystemEx
{
    public static class ProcessEx
    {
        public static  void CloseProcess(string processName)
        {
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
