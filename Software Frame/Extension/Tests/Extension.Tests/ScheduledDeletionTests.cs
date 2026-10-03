// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ScheduledDeletionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.IO、System.Reflection、System.Threading.Tasks、Extension.IO.Retention。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Extension.IO.Retention;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// ScheduledDeletionTests 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// </summary>
    public class ScheduledDeletionTests
    {
        /// <summary>
        /// 验证 CreateDateFolder 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Combine、ToString、CreateDirectory、WriteAllText；具体条件和先后次序见方法体。
        /// </summary>
        private static string CreateDateFolder(TempWorkspace workspace, DateTime date, bool withFile = false)
        {
            var day = workspace.Combine(date.Year.ToString(), date.Month.ToString(), date.Day.ToString());
            var product = Path.Combine(day, "P1");
            Directory.CreateDirectory(product);
            if (withFile) File.WriteAllText(Path.Combine(product, "image.bin"), "payload");
            return day;
        }

        /// <summary>
        /// 验证 Missing_root_returns_empty_result 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 nameof、DeleteExpiredFiles、Combine、Empty；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task Missing_root_returns_empty_result()
        {
            using var workspace = new TempWorkspace(nameof(Missing_root_returns_empty_result));
            var deleted = await ScheduledDeletion.DeleteExpiredFiles(workspace.Combine("missing"), 30, 0);
            Assert.Empty(deleted);
        }

        /// <summary>
        /// 验证 Expired_day_is_deleted_recursively 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 nameof、CreateDateFolder、DeleteExpiredFiles、Equal、Single；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task Expired_day_is_deleted_recursively()
        {
            using var workspace = new TempWorkspace(nameof(Expired_day_is_deleted_recursively));
            var day = CreateDateFolder(workspace, new DateTime(2020, 1, 1), withFile: true);
            var deleted = await ScheduledDeletion.DeleteExpiredFiles(workspace.Root, 30, 0);
            Assert.Equal(day, Assert.Single(deleted));
            Assert.False(Directory.Exists(day));
        }

        /// <summary>
        /// 验证 Expiration_boundary_is_exclusive 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 nameof、CreateDateFolder、AddDays、DeleteExpiredFiles、True；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task Expiration_boundary_is_exclusive()
        {
            using var workspace = new TempWorkspace(nameof(Expiration_boundary_is_exclusive));
            var edge = CreateDateFolder(workspace, DateTime.Today.AddDays(-30));
            var older = CreateDateFolder(workspace, DateTime.Today.AddDays(-31));
            var deleted = await ScheduledDeletion.DeleteExpiredFiles(workspace.Root, 30, 0);
            Assert.True(Directory.Exists(edge));
            Assert.False(Directory.Exists(older));
            Assert.Equal(older, Assert.Single(deleted));
        }

        /// <summary>
        /// 验证 Non_date_numeric_tree_is_preserved 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 nameof、Combine、CreateDirectory、DeleteExpiredFiles、Empty；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task Non_date_numeric_tree_is_preserved()
        {
            using var workspace = new TempWorkspace(nameof(Non_date_numeric_tree_is_preserved));
            var numeric = workspace.Combine("1", "2", "3");
            Directory.CreateDirectory(numeric);
            var deleted = await ScheduledDeletion.DeleteExpiredFiles(workspace.Root, 30, 0);
            Assert.Empty(deleted);
            Assert.True(Directory.Exists(numeric));
        }

        /// <summary>
        /// 验证 Invalid_calendar_date_is_preserved 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 nameof、Combine、CreateDirectory、DeleteExpiredFiles、Empty；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task Invalid_calendar_date_is_preserved()
        {
            using var workspace = new TempWorkspace(nameof(Invalid_calendar_date_is_preserved));
            var invalid = workspace.Combine("2025", "13", "45");
            Directory.CreateDirectory(invalid);
            var deleted = await ScheduledDeletion.DeleteExpiredFiles(workspace.Root, 30, 0);
            Assert.Empty(deleted);
            Assert.True(Directory.Exists(invalid));
        }

        /// <summary>
        /// 验证 Disk_pressure_uses_saturating_byte_accounting 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 nameof、CreateDateFolder、AddDays、DeleteExpiredFiles、Equal；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task Disk_pressure_uses_saturating_byte_accounting()
        {
            using var workspace = new TempWorkspace(nameof(Disk_pressure_uses_saturating_byte_accounting));
            var first = CreateDateFolder(workspace, DateTime.Today);
            var second = CreateDateFolder(workspace, DateTime.Today.AddDays(-1));
            var deleted = await ScheduledDeletion.DeleteExpiredFiles(workspace.Root, 3650, long.MaxValue);
            Assert.Equal(new[] { second, first }, deleted);
            Assert.False(Directory.Exists(first));
            Assert.False(Directory.Exists(second));
        }

        /// <summary>
        /// 验证 Invalid_arguments_fail_before_background_work_starts 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Throws、DeleteExpiredFiles；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Invalid_arguments_fail_before_background_work_starts()
        {
            Assert.Throws<ArgumentException>(() => { _ = ScheduledDeletion.DeleteExpiredFiles(" ", 1, 0); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = ScheduledDeletion.DeleteExpiredFiles("x", -1, 0); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = ScheduledDeletion.DeleteExpiredFiles("x", 1, -1); });
        }

        /// <summary>
        /// 验证 Public_interface_has_one_operation_and_legacy_types_are_absent 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 GetMethods、Equal、nameof、Single、Null；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Public_interface_has_one_operation_and_legacy_types_are_absent()
        {
            var methods = typeof(ScheduledDeletion)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Assert.Equal(nameof(ScheduledDeletion.DeleteExpiredFiles), Assert.Single(methods).Name);
            Assert.Null(typeof(ScheduledDeletion).GetMethod("DeleteExpiredFiles1", BindingFlags.Public | BindingFlags.Static));
            Assert.Null(typeof(ScheduledDeletion).Assembly.GetType("Extension.IO.Retention.Legacy.ScheduledDeletionEx"));
            Assert.Null(typeof(ScheduledDeletion).Assembly.GetType("Extension.IO.Retention.DateFolderInfo"));
        }
    }
}