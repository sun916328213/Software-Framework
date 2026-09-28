using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.Core
{
    public interface ILog_DP
    {
        event Action<DateTime, string> OnLog;
        event Action<DateTime, string> OnLogError;
    }
}
