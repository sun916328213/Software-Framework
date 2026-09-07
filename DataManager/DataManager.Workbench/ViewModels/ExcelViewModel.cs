using DataManager.Excel;
using DataManager.Excel.Definition;
using DataManager.Workbench.Models;
using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace DataManager.Workbench.ViewModels
{
    /// <summary>
    /// Excel 测试页：通过 DataManager 的 ExcelManager / IExcel 完成全部 Excel 操作
    /// </summary>
    public class ExcelViewModel : BindableBase
    {
        private readonly ExcelManager _excelManager;
        private IExcel _excel;

        public ExcelViewModel(ExcelManager excelManager)
        {
            _excelManager = excelManager;

            ApplyCommand = new DelegateCommand(() => Run(() =>
            {
                if (_excel != null)
                    _excelManager.Remove(_excel);

                Directory.CreateDirectory(ExcelFilePath);
                _excel = _excelManager.Create(new ExcelParam
                {
                    Name = "workbench",
                    FilePath = ExcelFilePath,
                    FileName = ExcelFileName,
                    HasHeader = HasHeader,
                    ToolType = SelectedExcelType,
                });
                Log($"✓ Excel 实例已创建: {SelectedExcelType} → {ExcelFileName}");
            }));

            Write10Command = new DelegateCommand(() => RunWork("覆盖写入 10 条", () =>
            {
                EnsureExcel().Write(Sample(10));
                QueryAll();
            }));

            Add1Command = new DelegateCommand(() => RunWork("追加 1 条", () =>
            {
                EnsureExcel().Add(Sample(1)[0]);
                QueryAll();
            }), () => IsEPPlus).ObservesProperty(() => SelectedExcelType);

            Add10Command = new DelegateCommand(() => RunWork("追加 10 条", () =>
            {
                EnsureExcel().AddRange(Sample(10));
                QueryAll();
            }), () => IsEPPlus).ObservesProperty(() => SelectedExcelType);

            QueryAllCommand = new DelegateCommand(() => RunWork("查询全部", () => QueryAll()));

            QueryConditionCommand = new DelegateCommand(() => RunWork("条件查询 Age>25", () =>
            {
                var data = EnsureExcel().Query<ExcelPerson>(x => x.Age > 25).ToList();
                Results = new ObservableCollection<ExcelPerson>(data);
            }));

            UpdateSelectedCommand = new DelegateCommand(() => RunWork("更新选中行", () =>
            {
                var sel = SelectedEntity ?? throw new InvalidOperationException("请先选中一行数据");
                EnsureExcel().Update<ExcelPerson>(x => x.Name == sel.Name, new ExcelPerson
                {
                    Name = sel.Name,
                    Age = sel.Age + 1,
                    CreateTime = DateTime.Now,
                });
                QueryAll();
            }), () => SelectedEntity != null && IsEPPlus).ObservesProperty(() => SelectedEntity).ObservesProperty(() => SelectedExcelType);

            OpenFolderCommand = new DelegateCommand(() => Run(() =>
            {
                Process.Start(new ProcessStartInfo { FileName = ExcelFilePath, UseShellExecute = true });
            }));
        }

        // ---------- 配置 ----------
        public Array ExcelTypes { get; } = Enum.GetValues(typeof(ExcelType)).Cast<ExcelType>()
            .Where(x => x != ExcelType.NPOI).ToArray();

        public string ExcelFilePath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "excel");
        public string ExcelFileName { get; set; } = "workbench.xlsx";

        private bool _hasHeader = true;
        public bool HasHeader
        {
            get => _hasHeader;
            set => SetProperty(ref _hasHeader, value);
        }

        private ExcelType _selectedExcelType = ExcelType.MiniExcel;
        public ExcelType SelectedExcelType
        {
            get => _selectedExcelType;
            set => SetProperty(ref _selectedExcelType, value);
        }

        private bool IsEPPlus => SelectedExcelType == ExcelType.EPPlus;

        // ---------- 结果 ----------
        private ObservableCollection<ExcelPerson> _results = new();
        public ObservableCollection<ExcelPerson> Results
        {
            get => _results;
            set => SetProperty(ref _results, value);
        }

        private ExcelPerson _selectedEntity;
        public ExcelPerson SelectedEntity
        {
            get => _selectedEntity;
            set => SetProperty(ref _selectedEntity, value);
        }

        private string _outputText = string.Empty;
        public string OutputText
        {
            get => _outputText;
            set => SetProperty(ref _outputText, value);
        }

        // ---------- 命令 ----------
        public DelegateCommand ApplyCommand { get; }
        public DelegateCommand Write10Command { get; }
        public DelegateCommand Add1Command { get; }
        public DelegateCommand Add10Command { get; }
        public DelegateCommand QueryAllCommand { get; }
        public DelegateCommand QueryConditionCommand { get; }
        public DelegateCommand UpdateSelectedCommand { get; }
        public DelegateCommand OpenFolderCommand { get; }

        // ---------- 私有方法 ----------
        private IExcel EnsureExcel()
        {
            if (_excel == null)
                ApplyCommand.Execute();
            return _excel ?? throw new InvalidOperationException("Excel 初始化失败");
        }

        private static List<ExcelPerson> Sample(int count) => Enumerable.Range(1, count)
            .Select(i => new ExcelPerson
            {
                Name = $"人员{i}",
                Age = 20 + (i * 3) % 30,
                CreateTime = DateTime.Today.AddDays(-i),
            })
            .ToList();

        private void QueryAll()
        {
            var data = EnsureExcel().Query<ExcelPerson>().ToList();
            Results = new ObservableCollection<ExcelPerson>(data);
        }

        private void Log(string msg) => OutputText = $"[{DateTime.Now:HH:mm:ss}] {msg}\n" + OutputText;

        private Task RunWork(string title, Action action) => Task.Run(() =>
        {
            try
            {
                action();
                Log("✓ " + title);
            }
            catch (Exception ex)
            {
                Log($"✗ {title}: {ex.Message}");
            }
        });

        private void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log($"✗ {ex.Message}");
            }
        }
    }
}
