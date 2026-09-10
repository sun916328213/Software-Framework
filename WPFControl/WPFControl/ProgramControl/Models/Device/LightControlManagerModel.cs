using Device.Light;
using Device.Light.Definition;
using Device.RFID;
using Device.RFID.Definition;
using WPFControl.CustomControl.Models;
using WPFCustomControls.Controls;

namespace WPFControl.ProgramControl.Models
{
    public class LightControlManagerModel : ManagerModel<LightParam>
    {
        public LightControlManagerModel()
        {
            Name = "LightControl";
            FilePath = "Config//";
            FileName = "LightControlParam.json";
        }

        private ILightControl _LightControl;
        public ILightControl CurLightControl
        {
            get => _LightControl;
            set=>SetProperty(ref _LightControl, value);
        }


        private LightParam _selectedItem;

        public LightParam SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }
 
    }
}