namespace DataManager.Tests.Excel
{
    /// <summary>
    /// Excel 测试实体
    /// </summary>
    public class PersonRecord
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public DateTime CreateTime { get; set; } = new(2026, 1, 1);
    }
}
