using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;


namespace WPFControl.ProgramControl.ViewModels
{
    public class DataQueryViewModel<T, TDTO> : ObservableObject where T : ISQLDataBase
    {
        // 通过构造函数注入Model_DataQuery
        public readonly DataQueryService<T, TDTO> Service;

        public DataQueryModel<T, TDTO> Datas => Service.Model;


        public RelayCommand QueryCommand { get; }
        public IAsyncRelayCommand SaveCommand { get; }


        // 构造函数注入
        public DataQueryViewModel(DataQueryService<T, TDTO> model)
        {
            Service = model;

            QueryCommand = new RelayCommand(() =>
            {
                Service.Query_DB();
            });

            SaveCommand = new AsyncRelayCommand(async () =>
            {
              await  Service.Save_DB();
            });
        }
    }
}