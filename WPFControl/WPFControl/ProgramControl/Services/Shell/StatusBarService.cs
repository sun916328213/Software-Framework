using WPFControl.Helper;
using WPFControl.ProgramControl.Models;

namespace WPFControl.ProgramControl.Services
{
    public class StatusBarService
    {
        internal StatusBarModel Model { get; set; } = new StatusBarModel();

        public void Update(CStatusBar pStatusBar)
        {
            TryCatchHelper.TryExecute(() =>
            {
                Model.StatusBar = pStatusBar;
            });
        }
    }
}
