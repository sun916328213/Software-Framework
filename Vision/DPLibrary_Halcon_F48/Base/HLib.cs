namespace DPLibrary_Halcon_F48.Base
{
    public enum DisplayModel
    {
        None = 0,
        Image = 1 << 0,
        // ⚠️ MeasurePath(1<<5) 不能省：Ctr_HWindow_DP.ResetDisp 用
        // DisplayLayers.HasFlag(MeasurePath) 决定 DispMeasure()/DeDispMeasure()。
        // 缺这一位时，选本档 → 卡尺（MeasurePaths）在图上直接消失；
        // 且 DisplayModelToLayersConverter.Convert 会把它归回 Image_ROI，
        // 导致「切到 Image ROI 后卡尺再也出不来」。
        Image_ROI = 1 << 1 | 1 << 0 | 1 << 5,
        ROI = Image_ROI,
        Result = 1 << 4 | 1 << 3 | 1 << 2 |  1 << 0
    }

    [Flags]
    public enum DisplayLayers
    {
        None = 0,
        Image = 1 << 0,
        ROI = 1 << 1,
        Region = 1 << 2,
        XLD = 1 << 3,
        Message = 1 << 4,
        MeasurePath = 1 << 5
    }


}
