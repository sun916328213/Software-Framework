// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：RoiEditorViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Controls、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、System.Collections.ObjectModel、Vision.Core.Geometry、Vision.Core.Roi。导入不代表所有类型都会在运行时加载。
using Vision.Controls;
// ─────────────────────────────────────────────────────────────────────────────
// P8 —— ROI 编辑器 VM（计划 §P8 迁移 ROI Editor + §P7 ROI 跟随 Fixture 落地到 UI）。
//
// 操作对象是 Core 的 Roi（P7 实体）：增删改 + 整体跟随 Fixture，全是纯逻辑，
// 不依赖 HObject/显示窗口。显示窗口的叠加绘制仍走 Ctr_Halcon（HALCON 特有能力，
// 按 §P8 以 namespace 组织，不混入本 VM）。
// ─────────────────────────────────────────────────────────────────────────────

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Vision.Core.Geometry;
using Vision.Core.Roi;

#nullable enable

namespace Vision.Controls.ViewModels
{
    /// <summary>
    /// ROI 列表编辑器：增删、选中、平移、缩放、跟随 Fixture。
    /// </summary>
    public sealed partial class RoiEditorViewModel : ObservableObject
    {
        /// <summary>ROI 列表（绑定 ItemsControl）。</summary>
        public ObservableCollection<Roi> Rois { get; } = new();

        /// <summary>当前选中（可为 null）。</summary>
        [ObservableProperty]
        private Roi? _selected;

        /// <summary>列表数量（绑定标题/计数）。</summary>
        public int Count => Rois.Count;

        /// <summary>
        /// 执行添加操作；对应入口为 AddDefaultRectangle。
        /// 正文中的关键协作调用包括 AddRectangle；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        public void AddDefaultRectangle() => AddRectangle(32, 32, 100, 60);

        /// <summary>
        /// 执行添加操作；对应入口为 AddDefaultCircle。
        /// 正文中的关键协作调用包括 AddCircle；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        public void AddDefaultCircle() => AddCircle(32, 32, 60);

        /// <summary>添加一个轴对齐矩形并选中。</summary>
        public Roi AddRectangle(double centerX, double centerY, double width, double height)
        {
            var roi = Roi.Rectangle(centerX, centerY, width, height);
            Rois.Add(roi);
            Selected = roi;
            OnPropertyChanged(nameof(Count));
            return roi;
        }

        /// <summary>添加一个圆并选中。</summary>
        public Roi AddCircle(double centerX, double centerY, double diameter)
        {
            var roi = Roi.Circle(centerX, centerY, diameter);
            Rois.Add(roi);
            Selected = roi;
            OnPropertyChanged(nameof(Count));
            return roi;
        }

        /// <summary>删除选中（无选中时无操作）。</summary>
        [RelayCommand]
        public void RemoveSelected()
        {
            if (Selected == null)
                return;
            Rois.Remove(Selected);
            Selected = null;
            OnPropertyChanged(nameof(Count));
        }

        /// <summary>平移选中（图像像素）。</summary>
        public bool MoveSelected(double dx, double dy)
        {
            if (Selected == null)
                return false;
            Selected.Pose = new Pose2D(Selected.Pose.X + dx, Selected.Pose.Y + dy, Selected.Pose.AngleDeg);
            OnPropertyChanged(nameof(Selected));
            return true;
        }

        /// <summary>缩放选中（宽高；≤0 拒绝）。</summary>
        public bool ResizeSelected(double width, double height)
        {
            if (Selected == null || width <= 0 || height <= 0)
                return false;
            Selected.Size = new Size2D(width, height);
            OnPropertyChanged(nameof(Selected));
            return true;
        }

        /// <summary>
        /// 全部 ROI 跟随 Fixture（§P7 语义；原位替换，保持列表顺序与选中）。
        /// </summary>
        public void FollowFixture(Pose2D oldFixture, Pose2D newFixture)
        {
            var selectedIndex = Selected == null ? -1 : Rois.IndexOf(Selected);
            for (var i = 0; i < Rois.Count; i++)
                Rois[i] = Rois[i].FollowFixture(oldFixture, newFixture);
            Selected = selectedIndex >= 0 ? Rois[selectedIndex] : null;
            OnPropertyChanged(nameof(Selected));
        }

        /// <summary>清空列表与选中。</summary>
        [RelayCommand]
        public void Clear()
        {
            Rois.Clear();
            Selected = null;
            OnPropertyChanged(nameof(Count));
        }
    }
}
