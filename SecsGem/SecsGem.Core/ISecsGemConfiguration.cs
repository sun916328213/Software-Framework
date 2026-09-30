namespace SecsGem.Core;

/// <summary>供配置界面读取和保存 GEM 报表、事件定义的契约。</summary>
public interface ISecsGemConfiguration
{
    IReadOnlyList<SecsGemReportDefinition> LoadReports();
    IReadOnlyList<SecsGemEventDefinition> LoadEvents();
    void SaveReports();
    void SaveEvents();
}

public sealed record SecsGemReportDefinition(string Id, string Name, string VariableIds);

public sealed record SecsGemEventDefinition(string Id, string Name, string ReportIds, bool Enabled);
