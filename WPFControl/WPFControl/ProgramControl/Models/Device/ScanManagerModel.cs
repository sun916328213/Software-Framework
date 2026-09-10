using Device.Light;
using Device.Light.Definition;
using Device.RFID;
using Device.RFID.Definition;
using Device.Scan.Definition;
using WPFControl.CustomControl.Models;
using WPFCustomControls.Controls;

namespace WPFControl.ProgramControl.Models
{
    public class ScanManagerModel : ConfigManagerModel<ScanParam >
    {
        public ScanManagerModel()
        {
            Name = "Scan";
            FilePath = "Config//";
            FileName = "ScanParam.json";
        }
    }
}