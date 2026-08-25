using CommunityToolkit.Mvvm.ComponentModel;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;
using System;

namespace WPFControl.ProgramControl.ViewModels
{
    public class StatusBarViewModel  
    {
        private readonly StatusBarService Service;

         

        public CStatusBar StatusBar=> Service.Model.StatusBar;

 
        public StatusBarViewModel(StatusBarService model)
        {
            Service = model;

        
        }
    }
}