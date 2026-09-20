using Extension.Core.Collections;
using Prism.Ioc;

namespace Secs.Variable
{
    public class DVID : VIDBase
    {
        public DVID()
        {
        }
        public override string FileName { get; set; } = "DVID.csv";

    }
}
