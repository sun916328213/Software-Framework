using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    internal class ImageQueryViewModel : ObservableObject
    {
        private readonly ImageQueryService Service;

        public ImageQueryModel Datas => Service.Model;
 
        public RelayCommand QueryCommand { get; }
        public RelayCommand FilterCommand { get; }
        public RelayCommand SaveCommand { get; }


        public ImageQueryViewModel(ImageQueryService model)
        {
            Service = model;

            QueryCommand = new RelayCommand(() =>
            {
                Service.Query_ImageInfo();

            });

            FilterCommand = new RelayCommand(() =>
            {
                Service.Filter_ImageInfo();

            });

            SaveCommand = new RelayCommand( async() =>
            {
                await Service.Save();
            });
        }
    }
}