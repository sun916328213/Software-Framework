using System;
using ScriptEditor.Tests;

public static class Program
{
    [STAThread]
    public static int Main()
    {
        Console.WriteLine("=== ScriptEngine globals 注入测试 ===");
        try
        {
            ScriptEngineGlobalsTest.RunSelfContained();
            Console.WriteLine("=== 测试通过 ===");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"=== 测试失败: {ex} ===");
            return 1;
        }
    }
}
