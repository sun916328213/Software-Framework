using DataManager.SQL;
using Packing_Machine.Data.Model;
using Packing_Machine.Business.Station;
using WPFControl.ProgramControl.Services;

namespace Packing_Machine.Data.Services
{
    /// <summary>
    /// 产能统计服务：任一工站检测结果非 OK（NG 或 ERROR）即判定为不良品。
    /// </summary>
    public class PackingCapacityStatisticsService : CapacityStatisticsService<CSQLData>
    {
        public PackingCapacityStatisticsService(SQLManager sqlManager) : base(sqlManager)
        {
        }

        protected override bool IsNG(CSQLData data)
        {
            if (data?.StationInfo == null) return true;

            var s = data.StationInfo;
            return IsNG(s.Load)
                || IsNG(s.ReadID)
                || IsNG(s.Accessory)
                || IsNG(s.Label)
                || IsNG(s.HeatSeal)
                || IsNG(s.ALDetect);
        }

        private static bool IsNG(Result r) => r != null && r.CheckResult != EPLCResult.OK;
    }
}