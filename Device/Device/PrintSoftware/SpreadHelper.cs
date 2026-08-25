//using HelpTool.Method;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading.Tasks;

//namespace Device.Print
//{
//    public class SpreadHelper:SingletonHelper<SpreadHelper>
//    {  
//        /// <summary>
//       /// Spread表格信息结构体
//       /// </summary>
//        public struct SSpreadInfo
//        {
//            /// <summary>
//            /// 表格单元格行
//            /// </summary>
//            public int Row;
//            /// <summary>
//            /// 表格单元格列
//            /// </summary>
//            public int Column;
//            /// <summary>
//            /// 表格单元格数据
//            /// </summary>
//            public string Value;

//            public SSpreadInfo(int i, int j, string data)
//            {
//                Row = i; Column = j; Value = data;
//            }
//        }

//        /// <summary>
//        /// 打开
//        /// </summary>
//        /// <param name="pFpSpread"></param>
//        /// <param name="pData"></param>
//        /// <returns></returns>
//        public bool Open(FpSpread pFpSpread, string pData)
//        {
//            try
//            {
//                byte[] array = Encoding.UTF8.GetBytes(pData);
//                using (MemoryStream stream = new MemoryStream(array))
//                {
//                    using (StreamReader reader = new StreamReader(stream))
//                    {
//                        pFpSpread.Open(reader.BaseStream);
//                        return true;
//                    }
//                }
//            }
//            catch { return false; }
//        }

//        /// <summary>
//        /// 获取表格数据
//        /// </summary>
//        /// <param name="values"></param>
//        /// <param name="pFpSpread"></param>
//        /// <returns></returns>
//        public bool GetKeys(out List<SSpreadInfo> values, FpSpread pFpSpread)
//        {
//            values = new List<SSpreadInfo>();
//            try
//            {
//                int row = pFpSpread.Sheets[0].RowCount;
//                int col = pFpSpread.Sheets[0].ColumnCount;

//                for (int i = 0; i < row; i++)
//                {
//                    for (int j = 0; j < col; j++)
//                    {
//                        var data = pFpSpread.Sheets[0].GetValue(i, j);
//                        if (data != null)
//                        {
//                            string str = data.ToString();
//                            if (str != string.Empty && str.Contains("$"))
//                            {
//                                values.Add(new SSpreadInfo(i, j, str));
//                            }
//                        }
//                    }
//                }
//                return true;
//            }
//            catch { return false; }
//        }

//        /// <summary>
//        /// 修改表格数据
//        /// </summary>
//        /// <param name="pDatas">数据集合</param>
//        /// <param name="Keys">表格的Key 以$开头</param>
//        /// <param name="pFpSpread">表格</param>
//        /// <returns></returns>
//        public bool SetValue(Dictionary<string, string> pDatas, List<SSpreadInfo> Keys, FpSpread pFpSpread)
//        {
//            try
//            {
//                // 数据keys开头添加$
//                var datakeys = pDatas.Keys.Select(s => "$" + s);

//                // 构造正则表达式模式
//                var pattern = string.Join("|", datakeys
//                        .OrderByDescending(k => k.Length) // 按长度降序排序
//                         .Select(Regex.Escape)            // 转义特殊字符
//                );

//                foreach (var item in Keys)
//                {
//                    string data = Regex.Replace(item.Value, pattern, MatchData =>
//                    {
//                        var key = MatchData.Value.Replace("$", "");
//                        if (pDatas.TryGetValue(key, out string value))
//                        {
//                            return value;
//                        }
//                        return MatchData.Value;
//                    });
//                    pFpSpread.Sheets[0].SetValue(item.Row, item.Column, data);
//                }
//                return true;
//            }
//            catch { return false; }
//        }

//        /// <summary>
//        /// 打印标签
//        /// </summary>
//        /// <param name="pFpSpread">Spread</param>
//        /// <param name="pPrintName">打印机名称</param>
//        /// <returns></returns>
//        public bool Print(FpSpread pFpSpread, string pPrintName = "")
//        {
//            try
//            {
//                PrintInfo printInfo = new PrintInfo();

//                printInfo.PrintToPdf = false;
//                //打印机名称
//                printInfo.Printer = pPrintName;
//                printInfo.PdfWriteMode = FarPoint.Win.Spread.PdfWriteMode.Append;
//                printInfo.PdfFileName = "";

//                printInfo.Preview = false;    //设置打印预览
//                printInfo.UseSmartPrint = true;     //设置打印自适应
//                printInfo.ShowBorder = false; // 是否显示表单的四周打印边界
//                printInfo.ShowColor = false;  // 是否显示颜色
//                printInfo.ShowColumnHeader = PrintHeader.Hide;  // 是否显示列标题
//                printInfo.ShowGrid = false;  // 是否显示网格线
//                printInfo.ShowPrintDialog = false; // 是否显示打印对话框
//                printInfo.ShowRowHeader = PrintHeader.Hide; // 是否显示行标题
//                printInfo.ShowShadows = false; // 是否打印标题阴影

//                //设置打印范围
//                /printInfo.PrintType = PrintType.All;
//                //printInfo.RowStart = 0;
//                //printInfo.RowEnd = 15;
//                //printInfo.ColStart = 0;
//                //printInfo.ColEnd = 11;

//                pFpSpread.Sheets[0].PrintInfo = printInfo;
//                pFpSpread.PrintSheet(0);
//                return true;
//            }
//            catch { return false; }
//        }
//    }
//}
