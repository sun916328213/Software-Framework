using Vision.Core.Imaging;

#nullable enable
namespace Vision.Controls.Services;

public sealed record VisionProjectDocument(string Json, VisionImage? Image = null, bool IsPackage = false);
