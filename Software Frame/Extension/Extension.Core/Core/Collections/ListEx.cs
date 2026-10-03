// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：ListEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.Collections
{
    /// <summary>
    /// ListEx 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// </summary>
    public static class ListEx
    {
        // 比较两个列表是相等（忽略顺序）
        public static bool Equal<T>(this List<T> list1, List<T> list2)
        {
            // 第一步：如果数量不同，直接不相等
            if (list1.Count != list2.Count)
                return false;

            // 第二步：统计每个元素的出现次数，然后比较计数是否一致
            var countDict1 = list1.GroupBy(x => x)
                                  .ToDictionary(g => g.Key, g => g.Count());

            var countDict2 = list2.GroupBy(x => x)
                                  .ToDictionary(g => g.Key, g => g.Count());

            // 比较两个字典的键和值是否完全一致
            return countDict1.Count == countDict2.Count &&
                   countDict1.All(kv =>
                       countDict2.TryGetValue(kv.Key, out int count2) &&
                       kv.Value == count2);
        }
    }
}
