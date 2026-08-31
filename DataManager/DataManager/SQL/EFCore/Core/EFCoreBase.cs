using DataManager.SQL.Definition;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace DataManager.SQL.EFCore
{
    public abstract class EFCoreBase<T> : DbContext, ISQL<T> where T : class
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured && SQLParam != null)
            {
                string con;

                switch (SQLParam.DBType)
                {
                    case ESQLType.MySQL:
                        con = $"server={SQLParam.ServerIP};port={SQLParam.Port};user={SQLParam.UserName};password={SQLParam.Password};database={SQLParam.DBName}";
                        optionsBuilder.UseMySQL(con);
                        break;

                    case ESQLType.PostgreSQL:
                        con = $"server={SQLParam.ServerIP} ;port= {SQLParam.Port} ;username= {SQLParam.UserName};password={SQLParam.Password};database={SQLParam.DBName}";
                        optionsBuilder.UseNpgsql(con);
                        break;
                }
            }
            base.OnConfiguring(optionsBuilder);
        }

        // 重写 OnModelCreating 方法以支持分表
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 直接添加单个类型
            modelBuilder.Entity<T>();
            base.OnModelCreating(modelBuilder);
        }
 
        /// <summary>
        /// 数据库参数
        /// </summary>
        public virtual SQLParam SQLParam { get; set; }

        /// <summary>
        /// 名称 
        /// </summary>
        public virtual string Name { get; set; }


        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (value) Connect();
                else DisConnect();
            }
        }


        public virtual void Connect()
        {
            Database.EnsureCreated();
            _isConnected = true;
        }

        public virtual void DisConnect()
        {
            _isConnected = false;
        }

        public virtual void Add(T data)
        {
            Set<T>().Add(data);
            SaveChangesOptimized();
        }

        public virtual async Task AddAsync(T data) 
        {
            await Set<T>().AddAsync(data);
            SaveChangesOptimized();
        }

        public virtual void AddRange(IEnumerable<T> datas)
        {
            Set<T>().AddRange(datas);
            SaveChangesOptimized();
        }

        public virtual async Task AddRangeAsync(IEnumerable<T> datas) 
        {
           await Set<T>().AddRangeAsync(datas);
            SaveChangesOptimized();
        }

        public virtual void Delete(Expression<Func<T, bool>> predicate) 
        {
            var table = Set<T>();
            var entity = table.FirstOrDefault(predicate);
            if (entity != null)
            {
                table.Remove(entity);
                SaveChanges();
            }
        }

        public virtual async Task DeleteAsync(Expression<Func<T, bool>> predicate)
        {
            await Task.Run(() => Delete(predicate));
        }

        public virtual void DeleteAll() 
        {
            var table = Set<T>();
            IEnumerable<T> allEntities = table;
            table.RemoveRange(allEntities);
            SaveChanges();
        }

        public virtual async Task DeleteAllAsync() 
        {
            await Task.Run(DeleteAll);
        }

        public virtual IEnumerable<T> Query()
        {
            var table = Set<T>();
            return table.AsNoTracking().AsEnumerable();
        }

        public virtual async Task<IEnumerable<T>> QueryAsync()
        {
           return await Task.Run(Query);
        }

        public virtual IEnumerable<T> Query(Expression<Func<T, bool>> predicate)
        {
            var table = Set<T>();
            return table.Where(predicate).AsNoTracking().ToList();
        }

        public virtual async Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate)
        {
            return await Task.Run(() => Query(predicate));
        }

        public virtual void Update(Expression<Func<T, bool>> predicate, T updatedata) 
        {
            var table = Set<T>();
            var entity = table.FirstOrDefault(predicate);
            if (entity != null)
            {
                var properties = typeof(T).GetProperties().Where(p => p.CanWrite);
                foreach (var property in properties)
                {
                    var newValue = property.GetValue(updatedata);
                    property.SetValue(entity, newValue);
                }

                SaveChanges();
            }
        }

        public virtual async Task UpdateAsync(Expression<Func<T, bool>> predicate, T updatedata) 
        {
            await Task.Run(() => Update(predicate, updatedata));
        }

        /// <summary>
        /// 高性能保存更改
        /// </summary>
        public void SaveChangesOptimized()
        {
            try
            {
                // 临时禁用自动检测更改以提高性能
                ChangeTracker.AutoDetectChangesEnabled = false;

                // 保存更改
                base.SaveChanges();

                // 重新启用自动检测更改
                ChangeTracker.AutoDetectChangesEnabled = true;

                // 定期清理 ChangeTracker，避免长时间运行后性能下降
                if (ChangeTracker.Entries().Count() > 100) // 当跟踪实体超过1000个时清理
                {
                    DetachEntities();
                }
            }
            catch (Exception ex)
            {
                // 重新启用自动检测更改
                ChangeTracker.AutoDetectChangesEnabled = true;

                // 记录异常信息
                throw new Exception($"保存数据时发生错误: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 分离所有实体以释放内存
        /// </summary>
        private void DetachEntities()
        {
            // 问题：在遍历集合的同时修改集合会导致"集合已修改；可能无法执行枚举操作"错误
            // 解决方案：先将集合作为列表，然后再遍历
            var entries = ChangeTracker.Entries().ToList();
            foreach (var entry in entries)
            {
                if (entry.Entity != null)
                {
                    entry.State = EntityState.Detached;
                }
            }
        }
    }
}
