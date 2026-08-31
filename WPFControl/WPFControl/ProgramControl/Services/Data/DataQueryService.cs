using DataManager.SQL;
using Prism.Ioc;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WPFControl.Helper;
using WPFControl.ProgramControl.Models;
using WPFCustomControls.Window.Views;
using Extension.Core.ControlEx;
using System;
using WPFControl.Method;

namespace WPFControl.ProgramControl.Services
{
    public class DataQueryService<T, TDTO> where T : ISQLDataBase
    {
        internal readonly SQLManager SQLManager;

        internal readonly DataQueryModel<T, TDTO> Model;

        ISQL<T> Service_SQL => (ISQL<T>)SQLManager[0];

        public DataQueryService(IContainerProvider container)
        {
            SQLManager = container.Resolve<SQLManager>();
            Model = container.Resolve<DataQueryModel<T, TDTO>>();
        }

        /// <summary>
        /// 查询数据库数据
        /// </summary>
        /// <returns></returns>
        public void Query_DB()
        {
            Func<IEnumerable<T>> func = () =>
            {
                IEnumerable<T> data;

                var end = Model.DateTime_End.AddDays(1).Date;
                var start = Model.DateTime_Start.Date;
               
                if (string.IsNullOrEmpty(Model.ID))
                    data = Service_SQL.Query(s => s.Time.Date >= start && s.Time.Date <= end);

                else
                    data = Service_SQL.Query(s => s.Time.Date >= start && s.Time.Date <= end && s.ID == Model.ID);

                if (data != null && data.Count() > 0)
                {
                    Task.Run(() => Model.Datas = new ObservableCollection<T>(data));
                }
                return data;
            };

            TryCatchHelper.TryExecute(() => func.UIBeginInvoke());
        }

        /// <summary>
        /// 保存数据 CSV格式
        /// </summary>
        public async Task Save_DB()
        {
            await TryCatchHelper.TryExecuteAsync(async () =>
            {
                QuerySave querySave = new QuerySave();
                await querySave.QuerySaveCSVAsync(Model.DatasDTO);
            });
        }
    }
}
