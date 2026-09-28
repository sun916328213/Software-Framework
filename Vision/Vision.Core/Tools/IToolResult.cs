using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.Core.Tools
{
    public interface IToolResult:IDisposable
    {
        bool IsFailed { get; set; }

        string ToString();
    }
}
