using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DPLibrary_Base_F48.Tools
{
    public interface IToolResult:IDisposable
    {
        bool IsFailed { get; set; }

        string ToString();
    }
}
