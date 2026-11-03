using Vision.Core.Imaging;

#nullable enable
namespace Vision.Controls.Services;

public interface IVisionProjectFileStore
{
    string Read(string path);
    void Write(string path, string json);
    VisionProjectDocument ReadProject(string path) => new(Read(path));
    void WriteProject(string path, string json, VisionImage? image)
    {
        if (image != null) throw new NotSupportedException("当前工程存储不支持图像快照，不能静默丢弃图像。");
        Write(path, json);
    }
}
