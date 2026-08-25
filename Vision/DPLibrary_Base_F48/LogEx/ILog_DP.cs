using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DPLibrary_Base_F48
{
    public interface ILog_DP
    {
        event Action<DateTime, string> OnLog;
        event Action<DateTime, string> OnLogError;
    }
}
