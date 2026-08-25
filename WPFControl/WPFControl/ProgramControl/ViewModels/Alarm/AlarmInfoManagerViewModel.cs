using Prism.Ioc;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    internal class AlarmInfoManagerViewModel
    {
        private readonly AlarmInfoManagerService Service;

        public AlarmInfoManagerModel Datas => Service.Model;

        public Action SaveCallbackCommand { get; }

        public AlarmInfoManagerViewModel(IContainerProvider container)
        {
             Service = container.Resolve<AlarmInfoManagerService>();

            SaveCallbackCommand = (async () =>
            {
              await  Service.Save();
            });
        }
 
    }
}
