namespace DPLibrary_Halcon_F48.Base
{
    public enum DisplayModel
    {
        None = 0,
        Image = 1 << 0,
        Image_ROI = 1 << 1 | 1 << 0,
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
