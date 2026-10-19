using SecsGem.Core.Order;

namespace SecsGem.Runtime.Order.S9;

/// <summary>S9F1：设备 ID 不识别，B[10] MHEAD，W=0。</summary>
[SecsOrder("S9F1")]
internal sealed class S9F1 : S9HeaderError { }

/// <summary>S9F3：Stream 不识别，B[10] MHEAD，W=0。</summary>
[SecsOrder("S9F3")]
internal sealed class S9F3 : S9HeaderError { }

/// <summary>S9F5：Function 不识别，B[10] MHEAD，W=0。</summary>
[SecsOrder("S9F5")]
internal sealed class S9F5 : S9HeaderError { }

/// <summary>S9F7：数据非法，B[10] MHEAD，W=0。</summary>
[SecsOrder("S9F7")]
internal sealed class S9F7 : S9HeaderError { }

/// <summary>S9F11：数据过长，B[10] MHEAD，W=0；阈值由项目定义。</summary>
[SecsOrder("S9F11")]
internal sealed class S9F11 : S9HeaderError { }
