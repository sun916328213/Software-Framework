// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：CapacityStatisticsFields。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.ProgramControl.Models。导入不代表所有类型都会在运行时加载。
using WPFControls.ProgramControl.Models;

namespace WPFControls.Integration.Queries;

/// <summary>
/// CapacityStatisticsFields 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
/// </summary>
public static class CapacityStatisticsFields
{
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 OKAt。
        /// </summary>
        public static int OKAt(CapacityStatisticsData d, int hour) => hour switch
        {
            0 => d.OKCOunt1, 1 => d.OKCOunt2, 2 => d.OKCOunt3, 3 => d.OKCOunt4,
            4 => d.OKCOunt5, 5 => d.OKCOunt6, 6 => d.OKCOunt7, 7 => d.OKCOunt8,
            8 => d.OKCOunt9, 9 => d.OKCOunt10, 10 => d.OKCOunt11, 11 => d.OKCOunt12,
            12 => d.OKCOunt13, 13 => d.OKCOunt14, 14 => d.OKCOunt15, 15 => d.OKCOunt16,
            16 => d.OKCOunt17, 17 => d.OKCOunt18, 18 => d.OKCOunt19, 19 => d.OKCOunt20,
            20 => d.OKCOunt21, 21 => d.OKCOunt22, 22 => d.OKCOunt23, 23 => d.OKCOunt24,
            _ => 0
        };

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 NGAt。
        /// </summary>
        public static int NGAt(CapacityStatisticsData d, int hour) => hour switch
        {
            0 => d.NGCount1, 1 => d.NGCount2, 2 => d.NGCount3, 3 => d.NGCount4,
            4 => d.NGCount5, 5 => d.NGCount6, 6 => d.NGCount7, 7 => d.NGCount8,
            8 => d.NGCount9, 9 => d.NGCount10, 10 => d.NGCount11, 11 => d.NGCount12,
            12 => d.NGCount13, 13 => d.NGCount14, 14 => d.NGCount15, 15 => d.NGCount16,
            16 => d.NGCount17, 17 => d.NGCount18, 18 => d.NGCount19, 19 => d.NGCount20,
            20 => d.NGCount21, 21 => d.NGCount22, 22 => d.NGCount23, 23 => d.NGCount24,
            _ => 0
        };

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetOK。
        /// </summary>
        public static void SetOK(CapacityStatisticsData d, int hour, int value)
        {
            switch (hour)
            {
                case 0: d.OKCOunt1 = value; break;
                case 1: d.OKCOunt2 = value; break;
                case 2: d.OKCOunt3 = value; break;
                case 3: d.OKCOunt4 = value; break;
                case 4: d.OKCOunt5 = value; break;
                case 5: d.OKCOunt6 = value; break;
                case 6: d.OKCOunt7 = value; break;
                case 7: d.OKCOunt8 = value; break;
                case 8: d.OKCOunt9 = value; break;
                case 9: d.OKCOunt10 = value; break;
                case 10: d.OKCOunt11 = value; break;
                case 11: d.OKCOunt12 = value; break;
                case 12: d.OKCOunt13 = value; break;
                case 13: d.OKCOunt14 = value; break;
                case 14: d.OKCOunt15 = value; break;
                case 15: d.OKCOunt16 = value; break;
                case 16: d.OKCOunt17 = value; break;
                case 17: d.OKCOunt18 = value; break;
                case 18: d.OKCOunt19 = value; break;
                case 19: d.OKCOunt20 = value; break;
                case 20: d.OKCOunt21 = value; break;
                case 21: d.OKCOunt22 = value; break;
                case 22: d.OKCOunt23 = value; break;
                case 23: d.OKCOunt24 = value; break;
            }
        }

        /// <summary>
        /// 设置当前实现管理的值或状态；对应入口为 SetNG。
        /// </summary>
        public static void SetNG(CapacityStatisticsData d, int hour, int value)
        {
            switch (hour)
            {
                case 0: d.NGCount1 = value; break;
                case 1: d.NGCount2 = value; break;
                case 2: d.NGCount3 = value; break;
                case 3: d.NGCount4 = value; break;
                case 4: d.NGCount5 = value; break;
                case 5: d.NGCount6 = value; break;
                case 6: d.NGCount7 = value; break;
                case 7: d.NGCount8 = value; break;
                case 8: d.NGCount9 = value; break;
                case 9: d.NGCount10 = value; break;
                case 10: d.NGCount11 = value; break;
                case 11: d.NGCount12 = value; break;
                case 12: d.NGCount13 = value; break;
                case 13: d.NGCount14 = value; break;
                case 14: d.NGCount15 = value; break;
                case 15: d.NGCount16 = value; break;
                case 16: d.NGCount17 = value; break;
                case 17: d.NGCount18 = value; break;
                case 18: d.NGCount19 = value; break;
                case 19: d.NGCount20 = value; break;
                case 20: d.NGCount21 = value; break;
                case 21: d.NGCount22 = value; break;
                case 22: d.NGCount23 = value; break;
                case 23: d.NGCount24 = value; break;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 SumOK。
        /// 正文中的关键协作调用包括 Sum、Range、OKAt；具体条件和先后次序见方法体。
        /// </summary>
        public static int SumOK(CapacityStatisticsData d)
            => Enumerable.Range(0, 24).Sum(h => OKAt(d, h));

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 SumNG。
        /// 正文中的关键协作调用包括 Sum、Range、NGAt；具体条件和先后次序见方法体。
        /// </summary>
        public static int SumNG(CapacityStatisticsData d)
            => Enumerable.Range(0, 24).Sum(h => NGAt(d, h));


}
