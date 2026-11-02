namespace Extension.Security.Licensing;

/// <summary>产品标识区分大小写。默认标识仅供底层 API/测试使用，正式交付必须明确配置。</summary>
public static class LicenseProduct
{
    public const string DefaultId = "SoftwareFrame.Default";

    public static bool IsValid(string? value) => value is { Length: >= 1 and <= 64 } &&
        char.IsAsciiLetterOrDigit(value[0]) &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');

    public static void Validate(string? value)
    {
        if (!IsValid(value))
            throw new ArgumentException("产品标识必须以英文字母或数字开头，长度 1–64，仅允许英文字母、数字、点、下划线和短横线。", nameof(value));
    }
}
