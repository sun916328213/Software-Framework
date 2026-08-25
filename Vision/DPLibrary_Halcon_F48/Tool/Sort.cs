using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace DPLibrary_Halcon_F48.Tool
{
    internal class Sort
    {
        /// <summary>
        /// 按旋转后的坐标排序
        /// 先从上到下（Y坐标）分行，行内从左到右（X坐标）排序
        /// </summary>
        /// <param name="results">待排序的结果列表</param>
        /// <param name="yTolerance">Y坐标容差（默认20像素）</param>
        /// <returns>排序后的结果列表</returns>
        static internal List<(HROI_Null region, string type, string code, Point center, double r, double quality)> 
            SortByRotatedCoordinates(
                List<(HROI_Null region, string type, string code, Point center, double r, double quality)> results,
                double yTolerance = 20.0)
        {
            // 1. 计算所有条码的中心点（作为旋转中心）
            double centerX = results.Average(r => r.center.X);
            double centerY = results.Average(r => r.center.Y);
            
            // 2. 计算旋转后的坐标（以群组中心为旋转中心）
            var rotatedData = results
                .Select(item =>
                {
                    // 将角度从度转换为弧度
                    double angleRad = item.r * Math.PI / 180.0;

                    // 旋转矩阵
                    double cosA = Math.Cos(angleRad);
                    double sinA = Math.Sin(angleRad);

                    // 平移到原点
                    double translatedX = item.center.X - centerX;
                    double translatedY = item.center.Y - centerY;

                    // 旋转
                    double rotatedX = translatedX * cosA - translatedY * sinA;
                    double rotatedY = translatedX * sinA + translatedY * cosA;

                    // 平移回原位置
                    rotatedX += centerX;
                    rotatedY += centerY;

                    return new { Item = item, RotatedX = rotatedX, RotatedY = rotatedY };
                })
                .ToList();

            // 3. 按Y坐标排序（从上到下）
            var sortedByY = rotatedData.OrderBy(x => x.RotatedY).ToList();
            
            // 4. 分组：Y坐标相近的为一行（容差yTolerance）
            // 关键改进：与行内最后一个元素比较，而不是平均值
            var rows = new List<List<dynamic>>();
            
            foreach (var item in sortedByY)
            {
                // 如果没有任何行，创建第一行
                if (rows.Count == 0)
                {
                    rows.Add(new List<dynamic> { item });
                }
                else
                {
                    // 取最后一行的最后一个元素进行比较
                    var lastRow = rows[rows.Count - 1];
                    var lastElement = lastRow[lastRow.Count - 1];
                    double lastY = (double)lastElement.RotatedY;
                    
                    if (Math.Abs(item.RotatedY - lastY) <= yTolerance)
                    {
                        // 加入最后一行
                        lastRow.Add(item);
                    }
                    else
                    {
                        // 创建新行
                        rows.Add(new List<dynamic> { item });
                    }
                }
            }
            
            // 5. 对每一行内按X坐标排序（从左到右），然后按行顺序输出
            var sortedResults = new List<(HROI_Null region, string type, string code, Point center, double r, double quality)>();
            
            foreach (var row in rows)
            {
                // 行内按X坐标排序（从左到右）
                var sortedRow = row.OrderBy((dynamic r) => (double)r.RotatedX).ToList();
                foreach (var item in sortedRow)
                {
                    sortedResults.Add(((dynamic)item).Item);
                }
            }
            
            return sortedResults;
        }
    }
}
