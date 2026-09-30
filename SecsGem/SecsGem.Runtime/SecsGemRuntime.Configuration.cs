using SecsGem.Core;

namespace SecsGem.Runtime;

public partial class SecsGemRuntime : ISecsGemConfiguration
{
    public IReadOnlyList<SecsGemReportDefinition> LoadReports()
    {
        _vidManager.ReportID.Load();
        return _vidManager.ReportID.GetDatas()
            .Select(x => new SecsGemReportDefinition(x.ID, x.Name, x.VID_Collection))
            .ToArray();
    }

    public IReadOnlyList<SecsGemEventDefinition> LoadEvents()
    {
        _vidManager.CEID.Load();
        return _vidManager.CEID.GetDatas()
            .Select(x => new SecsGemEventDefinition(x.ID, x.Name, x.ReportID_Collection, x.Enable))
            .ToArray();
    }

    public void SaveReports() => _vidManager.ReportID.Save();

    public void SaveEvents() => _vidManager.CEID.Save();
}
