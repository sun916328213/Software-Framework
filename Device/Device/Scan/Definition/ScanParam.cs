using CommunicationNew.Protocols.Serial;

namespace Device.Scan.Definition
{
   

    public class ScanParam: SerialParam
    {
        public string Name {  get; set; }
 
        public EScanType ScanType { get; set; }= EScanType.新大陆;
 
    }
}
