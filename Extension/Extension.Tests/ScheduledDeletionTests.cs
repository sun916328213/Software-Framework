using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Extension.IO.Retention;
using Xunit;

namespace Extension.Tests
{
    public class ScheduledDeletionTests
    {
        private static string CreateDateFolder(TempWorkspace workspace, DateTime date, bool withFile = false)
        {
            var day = workspace.Combine(date.Year.ToString(), date.Month.ToString(), date.Day.ToString());
            var product = Path.Combine(day, "P1");
            Directory.CreateDirectory(product);
            if (withFile) File.WriteAllText(Path.Combine(product, "image.bin"), "payload");
            return day;
        }

        [Fact]
        public async Task Missing_root_returns_empty_result()
        {
            using var workspace = new TempWorkspace(nameof(Missing_root_returns_empty_result));
            var deleted = await ScheduledDeletion.DeleteExpiredFiles(workspace.Combine("missing"), 30, 0);
            Assert.Empty(deleted);
        }

        [Fact]
        public async Task Expired_day_is_deleted_recursively()
        {
            using var workspace = new TempWorkspace(nameof(Expired_day_is_deleted_recursively));
            var day = CreateDateFolder(workspace, new DateTime(2020, 1, 1), withFile: true);
            var deleted = await ScheduledDeletion.DeleteExpiredFiles(workspace.Root, 30, 0);
            Assert.Equal(day, Assert.Single(deleted));
            Assert.False(Directory.Exists(day));
        }

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

        [Fact]
        public void Invalid_arguments_fail_before_background_work_starts()
        {
            Assert.Throws<ArgumentException>(() => { _ = ScheduledDeletion.DeleteExpiredFiles(" ", 1, 0); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = ScheduledDeletion.DeleteExpiredFiles("x", -1, 0); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = ScheduledDeletion.DeleteExpiredFiles("x", 1, -1); });
        }

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