namespace Device.Controls.Models;

/// <summary>
/// 驱动下拉选项：Value 为驱动标识字符串，DisplayName 为界面显示名。
/// </summary>
public sealed record CameraDriverOption(string Value, string DisplayName);
