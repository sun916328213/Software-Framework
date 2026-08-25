using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    public class BulletinBoardViewModel 
    {
        private readonly BulletinBoardService Service;

        public BulletinBoardModel Datas => Service.Model;

        public StatisicsModel StatisicsModel => Service.StatisticService.Model;

     

        public BulletinBoardViewModel(IContainerProvider container)
        {
            Service = container.Resolve<BulletinBoardService>();
        }
         
    }
}
