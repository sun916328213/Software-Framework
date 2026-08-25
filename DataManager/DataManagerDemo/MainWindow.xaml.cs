using DataManager.SQL;
using DataManager.SQL.Definition;
using DataManager.SQL.Dapper;
using DataManager.SQL.SqlSugarSQL;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;

namespace DataManagerDemo
{
    [SugarTable("testentity_{year}{month}{day}")]
    [SqlSugar.SplitTable(SqlSugar.SplitType.Day)]
    [DapperSplitTable(DataManager.SQL.Dapper.SplitType.Day)]
    public class TestEntity
    {
        [SugarColumn(IsPrimaryKey = true)]
        [Key]
        public long Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
        [SugarColumn(ColumnDataType = "timestamp without time zone")]
        public DateTime? CreateTime { get; set; }
    }

    public partial class MainWindow : Window
    {
        SQLManager sQLManager = new SQLManager();
        ISQL<TestEntity> EFCore => (ISQL<TestEntity>)sQLManager[0];
        ISQL<TestEntity> SQLSugra => (ISQL<TestEntity>)sQLManager[1];
        DapperBase<TestEntity> DapperDb => (DapperBase<TestEntity>)sQLManager[2];

        public MainWindow()
        {
            InitializeComponent();
            
            // 初始化EFCore
            sQLManager.Create<TestEntity>(new SQLParam
            {
                ServerIP = "localhost",
                Port = "5432",
                UserName = "postgres",
                Password = "postgres",
                DBName = "EFCore",
                DBType = ESQLType.PostgreSQL,
                ToolType = ESQLToolType.EFCore
            });
            
            // 初始化SQLSugar
            sQLManager.Create<TestEntity>(new SQLParam
            {
                ServerIP = "localhost",
                Port = "5432",
                UserName = "postgres",
                Password = "postgres",
                DBName = "SQLSugra",
                DBType = ESQLType.PostgreSQL,
                ToolType = ESQLToolType.SqlSugar
            });
            
            // 初始化Dapper
            sQLManager.Create<TestEntity>(new SQLParam
            {
                ServerIP = "localhost",
                Port = "5432",
                UserName = "postgres",
                Password = "postgres",
                DBName = "Dapper",
                DBType = ESQLType.PostgreSQL,
                ToolType = ESQLToolType.Dapper
            });
        }

        #region EFCore核心功能
        private void EFCore_Connect(object sender, RoutedEventArgs e)
        {
            try
            {
                EFCore.Connect();
                Log("✓ EFCore 连接成功");
            }
            catch (Exception ex)
            {
                Log($"✗ EFCore 连接失败: {ex.Message}");
            }
        }

        private void EFCore_Add(object sender, RoutedEventArgs e)
        {
            try
            {
                var entity = new TestEntity { Id = DateTime.Now.Ticks, Name = "EFCore测试", Age = 25 };
                EFCore.Add(entity);
                Log($"✓ EFCore 添加成功: {entity.Name}");
            }
            catch (Exception ex)
            {
                Log($"✗ EFCore 添加失败: {ex.Message}");
            }
        }

        private void EFCore_Delete(object sender, RoutedEventArgs e)
        {
            try
            {
                EFCore.Delete(x => x.Id == 1);
                Log("✓ EFCore 删除成功");
            }
            catch (Exception ex)
            {
                Log($"✗ EFCore 删除失败: {ex.Message}");
            }
        }

        private void EFCore_Query(object sender, RoutedEventArgs e)
        {
            try
            {
                var data = EFCore.Query().ToList();
                Log($"✓ EFCore 查询到 {data.Count} 条数据");
                foreach (var item in data.Take(5))
                {
                    Log($"  - ID:{item.Id}, Name:{item.Name}, Age:{item.Age}");
                }
            }
            catch (Exception ex)
            {
                Log($"✗ EFCore 查询失败: {ex.Message}");
            }
        }

        private void EFCore_Update(object sender, RoutedEventArgs e)
        {
            try
            {
                var entity = new TestEntity { Id = 1, Name = "EFCore更新", Age = 30 };
                EFCore.Update(x => x.Id == 1, entity);
                Log("✓ EFCore 更新成功");
            }
            catch (Exception ex)
            {
                Log($"✗ EFCore 更新失败: {ex.Message}");
            }
        }

        private void EFCore_AddRange(object sender, RoutedEventArgs e)
        {
            try
            {
                var entities = new List<TestEntity>
                {
                    new TestEntity { Id = DateTime.Now.Ticks, Name = "批量测试1", Age = 21 },
                    new TestEntity { Id = DateTime.Now.Ticks + 1, Name = "批量测试2", Age = 22 },
                    new TestEntity { Id = DateTime.Now.Ticks + 2, Name = "批量测试3", Age = 23 }
                };
                EFCore.AddRange(entities);
                Log($"✓ EFCore 批量添加 {entities.Count} 条成功");
            }
            catch (Exception ex)
            {
                Log($"✗ EFCore 批量添加失败: {ex.Message}");
            }
        }

        private void EFCore_QueryByCondition(object sender, RoutedEventArgs e)
        {
            try
            {
                var data = EFCore.Query(x => x.Age > 22).ToList();
                Log($"✓ EFCore 条件查询(Age>22)到 {data.Count} 条数据");
                foreach (var item in data)
                {
                    Log($"  - ID:{item.Id}, Name:{item.Name}, Age:{item.Age}");
                }
            }
            catch (Exception ex)
            {
                Log($"✗ EFCore 条件查询失败: {ex.Message}");
            }
        }
        #endregion

        #region SQLSugar核心功能
        private void SQLSugar_Connect(object sender, RoutedEventArgs e)
        {
            try
            {
                SQLSugra.Connect();
                Log("✓ SQLSugar 连接成功");
            }
            catch (Exception ex)
            {
                Log($"✗ SQLSugar 连接失败: {ex.Message}");
            }
        }

        private void SQLSugar_Add(object sender, RoutedEventArgs e)
        {
            try
            {
                var entity = new TestEntity 
                { 
                    Id = DateTime.Now.Ticks, 
                    Name = "张三", 
                    Age = 20, 
                    CreateTime = DateTime.Now 
                };
                SQLSugra.Add(entity);
                Log($"✓ SQLSugar 添加成功: {entity.Name}");
            }
            catch (Exception ex)
            {
                Log($"✗ SQLSugar 添加失败: {ex.Message}");
            }
        }

        private void SQLSugar_Delete(object sender, RoutedEventArgs e)
        {
            try
            {
                SQLSugra.Delete(x => x.Id == 1);
                Log("✓ SQLSugar 删除成功");
            }
            catch (Exception ex)
            {
                Log($"✗ SQLSugar 删除失败: {ex.Message}");
            }
        }

        private void SQLSugar_Query(object sender, RoutedEventArgs e)
        {
            try
            {
                var data = SQLSugra.Query().ToList();
                Log($"✓ SQLSugar 查询到 {data.Count} 条数据");
                foreach (var item in data.Take(5))
                {
                    Log($"  - ID:{item.Id}, Name:{item.Name}, Age:{item.Age}");
                }
            }
            catch (Exception ex)
            {
                Log($"✗ SQLSugar 查询失败: {ex.Message}");
            }
        }

        private void SQLSugar_Update(object sender, RoutedEventArgs e)
        {
            try
            {
                var entity = new TestEntity { Id = 1, Name = "张三(更新)", Age = 22, CreateTime = DateTime.Now };
                SQLSugra.Update(x => x.Id == 1, entity);
                Log("✓ SQLSugar 更新成功");
            }
            catch (Exception ex)
            {
                Log($"✗ SQLSugar 更新失败: {ex.Message}");
            }
        }

        private void SQLSugar_AddRange(object sender, RoutedEventArgs e)
        {
            try
            {
                var entities = new List<TestEntity>
                {
                    new TestEntity { Id = DateTime.Now.Ticks, Name = "李四", Age = 25, CreateTime = DateTime.Now },
                    new TestEntity { Id = DateTime.Now.Ticks + 1, Name = "王五", Age = 30, CreateTime = DateTime.Now },
                    new TestEntity { Id = DateTime.Now.Ticks + 2, Name = "赵六", Age = 35, CreateTime = DateTime.Now }
                };
                SQLSugra.AddRange(entities);
                Log($"✓ SQLSugar 批量添加 {entities.Count} 条成功");
            }
            catch (Exception ex)
            {
                Log($"✗ SQLSugar 批量添加失败: {ex.Message}");
            }
        }

        private void SQLSugar_QueryByCondition(object sender, RoutedEventArgs e)
        {
            try
            {
                var data = SQLSugra.Query(x => x.Age > 25).ToList();
                Log($"✓ SQLSugar 条件查询(Age>25)到 {data.Count} 条数据");
                foreach (var item in data)
                {
                    Log($"  - ID:{item.Id}, Name:{item.Name}, Age:{item.Age}");
                }
            }
            catch (Exception ex)
            {
                Log($"✗ SQLSugar 条件查询失败: {ex.Message}");
            }
        }
        #endregion

        #region Dapper核心功能
        private void Dapper_Connect(object sender, RoutedEventArgs e)
        {
            try
            {
                DapperDb.Connect();
                Log("✓ Dapper 连接成功");
            }
            catch (Exception ex)
            {
                Log($"✗ Dapper 连接失败: {ex.Message}");
            }
        }

        private void Dapper_Add(object sender, RoutedEventArgs e)
        {
            try
            {
                var entity = new TestEntity
                {
                    Id = DateTime.Now.Ticks,
                    Name = "张三",
                    Age = 25,
                    CreateTime = DateTime.Now
                };
                DapperDb.Add(entity);
                Log($"✓ Dapper 添加成功: {entity.Name}");
            }
            catch (Exception ex)
            {
                Log($"✗ Dapper 添加失败: {ex.Message}");
            }
        }

        private void Dapper_Delete(object sender, RoutedEventArgs e)
        {
            try
            {
                DapperDb.Delete(u => u.Age < 20);
                Log("✓ Dapper 条件删除成功");
            }
            catch (Exception ex)
            {
                Log($"✗ Dapper 删除失败: {ex.Message}");
            }
        }

        private void Dapper_Query(object sender, RoutedEventArgs e)
        {
            try
            {
                var users = DapperDb.Query().ToList();
                Log($"✓ Dapper 查询到 {users.Count} 条数据");
                foreach (var user in users.Take(5))
                {
                    Log($"  - ID={user.Id}, Name={user.Name}, Age={user.Age}");
                }
            }
            catch (Exception ex)
            {
                Log($"✗ Dapper 查询失败: {ex.Message}");
            }
        }

        private void Dapper_Update(object sender, RoutedEventArgs e)
        {
            try
            {
                // 注意：Dapper的分表Update需要先查询到对应分表的数据
                var user = DapperDb.Query(x => x.Id == 1001).FirstOrDefault();
                if (user != null)
                {
                    user.Name = "李四更新";
                    user.Age = 31;
                    DapperDb.Update(user);
                    Log($"✓ Dapper 更新成功: {user.Name}");
                }
                else
                {
                    Log("⚠ 未找到ID=1001的数据");
                }
            }
            catch (Exception ex)
            {
                Log($"✗ Dapper 更新失败: {ex.Message}");
            }
        }

        private void Dapper_AddRange(object sender, RoutedEventArgs e)
        {
            try
            {
                var users = new List<TestEntity>
                {
                    new TestEntity { Id = DateTime.Now.Ticks, Name = "李四", Age = 30, CreateTime = DateTime.Now },
                    new TestEntity { Id = DateTime.Now.Ticks + 1, Name = "王五", Age = 28, CreateTime = DateTime.Now },
                    new TestEntity { Id = DateTime.Now.Ticks + 2, Name = "赵六", Age = 35, CreateTime = DateTime.Now }
                };
                DapperDb.AddRange(users);
                Log($"✓ Dapper 批量添加 {users.Count} 条成功");
            }
            catch (Exception ex)
            {
                Log($"✗ Dapper 批量添加失败: {ex.Message}");
            }
        }

        private void Dapper_QueryByCondition(object sender, RoutedEventArgs e)
        {
            try
            {
                var adults = DapperDb.Query(u => u.Age >= 30).ToList();
                Log($"✓ Dapper 条件查询(Age>=30): {adults.Count} 条");
                foreach (var user in adults)
                {
                    Log($"  - {user.Name} (Age={user.Age})");
                }
            }
            catch (Exception ex)
            {
                Log($"✗ Dapper 条件查询失败: {ex.Message}");
            }
        }

        private void Dapper_GetSplitTables(object sender, RoutedEventArgs e)
        {
            try
            {
                var tables = DapperDb.GetSplitTables();
                Log($"✓ Dapper 共 {tables.Count} 个分表:");
                foreach (var table in tables)
                {
                    Log($"  - {table}");
                }
            }
            catch (Exception ex)
            {
                Log($"✗ Dapper 获取分表失败: {ex.Message}");
            }
        }
        #endregion

        private void Log(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            Console.WriteLine(logMessage.Trim());
            LogTextBox.AppendText(logMessage);
            LogTextBox.ScrollToEnd();
        }
    }
}
