using Prism.Ioc;

namespace Secs.Variable
{
    public class SVID : VIDBase
    {
        public SVID( )  
        {
        }

        public  override string FileName { get  ; set  ; }= "SVID.csv";
    }
}
