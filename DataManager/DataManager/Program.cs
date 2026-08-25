using DataManager.Excel;
using DataManager.Excel.Definition;
using DataManager.Excel.MiniExcel;
using DataManager.Log;
using DataManager.Log.Definition;
using DataManager.SQL;
using DataManager.SQL.Definition;
using DataManager.SQL.EFCore;
using Extension.Core.ConvertEx;
using Extension.DesignPattern.SingletonPattern;
using Extension.Struct;
using Microsoft.Extensions.DependencyInjection;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Text;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace DataManager
{
    public class box1
    {
        [Key]
        public int Id { get; set; }
 
        public DateTime Time { get; set; } = DateTime.Now;
    }

    public class box2
    {
        [Key]
        public int Id { get; set; }
        public DateTime Time { get; set; } = DateTime.Now;
    }


    /// <summary>
    /// 测试实体类
    /// </summary>
    public class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
        public DateTime CreateTime { get; set; } = DateTime.Now;
    }


    class Program
    {

        static async Task Main(string[] args)
        {
            SQLTest();
        }

        #region SQL测试
        public static void SQLTest()
        {
            var param1 = new SQLParam()
            {
                ServerIP = "localhost",
                Port = "5432",
                UserName = "postgres",
                Password = "postgres",
                DBName = "test1",
                DBType = ESQLType.PostgreSQL,
                ToolType = ESQLToolType.EFCore
            };

            var param2 = new SQLParam()
            {
                ServerIP = "localhost",
                Port = "5432",
                UserName = "postgres",
                Password = "postgres",
                DBName = "test11",
                DBType = ESQLType.PostgreSQL,
                ToolType = ESQLToolType.EFCore
            };

            // 使用SQLManager的泛型Create方法创建对应类型的实例
            SQLManager sQLManager = new SQLManager();
             sQLManager.Create<box1>(param1);
           sQLManager.Create<box2>(param2);
            ISQL<box1> db1 = (ISQL<box1>)sQLManager[0];
            ISQL<box2> db2 = (ISQL<box2>)sQLManager[1];
            db1.Connect();
            db2.Connect();
            db1.Add(new box1() { Id = 12 });
            db2.Add(new box2() { Id = 13 });

            db1.DeleteAll ();

            db1.Add (new box1() { Id = 11 });
            db1.Delete (x => x.Id == 11);

            List<box1> boxes = new List<box1>();
            boxes.Add(new box1() { Id = 12 });
            boxes.Add(new box1() { Id = 13 });
            boxes.Add(new box1() { Id = 14 });
            db1.AddRange (boxes);

            var d1 = db1.Query ();
            var d2 = db1.Query (s => s.Id == 12).ToList();

            box1 box1 = new box1() { Id = 13 };
            db1.Update (s => s.Id == 13, box1);

            db1.DeleteAll ();
            for (int index = 1; index < 1000000; index++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                box1 box11 = new box1() { Id = index };
                db1.Add(box11);
                stopwatch.Stop();

                Console.WriteLine($"{index}:{stopwatch.ElapsedMilliseconds}");
            }
            Console.ReadLine();
        }

 
        #endregion

        #region Log测试

        public static async Task LogTest()
        {
              ILog logmanager = Singleton.GetInstance<LogManager>().Create(new LogParam
            {
                Name = "Log",
                FilePath = "C:\\Data\\Log",
                LogType = ELogType.SeriLog,
            });

            for (int i = 0; i < 100; i++)
            {
                if (i % 4 == 0)
                    logmanager.Add($"测试Log写入{i}");
                else if (i % 4 == 1)
                    logmanager.Add($"测试Log写入{i}", ELogLevel.Warning);
                else if (i % 4 == 2)
                    logmanager.Add($"测试Log写入{i}", ELogLevel.SysError);
                else if (i % 4 == 3)
                    logmanager.Add($"测试Log写入{i}", ELogLevel.Error);
            }

            var d1 = logmanager.Query(DateTime.Now, DateTime.Now);

            var d2 = await logmanager.QueryAsync(DateTime.Now, DateTime.Now);

            var d3 = logmanager.Query_Multi(DateTime.Now, DateTime.Now);

            var d4 = await logmanager.Query_MultiAsync(DateTime.Now, DateTime.Now);
        }

        #endregion

        #region Json测试
        public static void JsonTest()
        {
            List<box1> box1s = new List<box1>();
            box1s.Add(new box1());
            box1s.Add(new box1());



        }
        #endregion

        #region Excel测试
        public static async Task  ExcelTest()
        {
            IExcel excelManager = Singleton.GetInstance<ExcelManager>().Create(new  ExcelParam
            {
                FilePath = "C:\\Data\\Excel",
                FileName = "test1.xlsx",
                ToolType = ExcelType.EPPlus,
            });

      

            var d1 = new List<box1>();
            for (int index = 0; index < 100; index++)
            {
                d1.Add(new box1 { Id = index, Time = DateTime.Now });
            }


            excelManager.Write(d1);
 
            var d2 = excelManager.Query<box1>();
            var d3 = excelManager.Query<box1>( s=>s.Id>50);

        }

        
        #endregion

    }
}
