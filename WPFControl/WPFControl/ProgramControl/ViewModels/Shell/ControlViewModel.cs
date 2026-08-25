using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;
using System.Collections.ObjectModel;

namespace WPFControl.ProgramControl.ViewModels
{
    class ControlViewModel : ObservableObject
    {
        private readonly ControlService Service;


        public ObservableCollection<CControlItem> NavigationItems => Service.ControlItems;


        private CControlItem selectNavigationItems;

        public CControlItem SelectNavigationItems
        {
            get => selectNavigationItems;
            set => SetProperty(ref selectNavigationItems, value);
        }

        public RelayCommand SelectCommand { get; }

        public ControlViewModel(ControlService model)
        {
            this.Service = model;

            SelectCommand = new RelayCommand(() =>
            {
                if (SelectNavigationItems?.Action != null)
                {
                    SelectNavigationItems.Action.Invoke();
                }
            });
        }
    }
}
