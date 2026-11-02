using System.Text.Json.Serialization;

namespace Extension.Security.Licensing;

[JsonSerializable(typeof(LicenseInfo))]
internal partial class LicenseJsonContext : JsonSerializerContext
{
}
