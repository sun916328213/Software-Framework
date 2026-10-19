using SecsGem.Core.Order;

namespace SecsGem.Runtime.Order.S9;

/// <summary>S9F1：设备 ID 不识别。</summary>
/// <remarks>
/// 指令格式示例：配置 Device ID=0，却收到 Device ID=1 的 S1F1 W。
/// <code><![CDATA[
/// 'S9F1'                          // 设备→Host，W=0，无应答
/// <B [10] 0x00 0x01 0x81 0x01 0x00 0x00 0x12 0x34 0x56 0x78> // MHEAD：原错误接收报头，不含长度前缀
/// .
/// ]]></code>
/// 字节 0–1：错误 Device ID=1；2：W=1/S=1；3：F=1；4–5：PType/SType=0；6–9：原 System Bytes=0x12345678。
/// 报头必须取自该消息，不能照抄例值；SDK 自动检查发送，Runtime 不重复报错。
/// </remarks>
[SecsOrder("S9F1")]
internal sealed class S9F1 : S9HeaderError { }

/// <summary>S9F3：Stream 不识别。</summary>
/// <remarks>
/// 指令格式示例：收到未注册任何处理器的 S12F1 W。
/// <code><![CDATA[
/// 'S9F3'                          // 设备→Host，W=0，无应答
/// <B [10] 0x00 0x00 0x8C 0x01 0x00 0x00 0x12 0x34 0x56 0x78> // MHEAD：原 S12F1 W 的十字节报头
/// .
/// ]]></code>
/// 字节 0–1：Device ID=0；2：0x8C 为 W=1/S=12；3：F=1；4–5：PType/SType=0；6–9：原 System Bytes。
/// 必须保留实际原报头；未知 Function 不使用本消息，而使用 S9F5。
/// </remarks>
[SecsOrder("S9F3")]
internal sealed class S9F3 : S9HeaderError { }

/// <summary>S9F5：Function 不识别。</summary>
/// <remarks>
/// 指令格式示例：Stream 1 已支持，但收到未注册的 S1F251 W。
/// <code><![CDATA[
/// 'S9F5'                          // 设备→Host，W=0，无应答
/// <B [10] 0x00 0x00 0x81 0xFB 0x00 0x00 0x12 0x34 0x56 0x78> // MHEAD：原 S1F251 W 的十字节报头
/// .
/// ]]></code>
/// 字节 0–1：Device ID=0；2：W=1/S=1；3：0xFB 为 F=251；4–5：PType/SType=0；6–9：原 System Bytes。
/// 必须取自实际接收消息，不用数据非法 S9F7 代替未知 Function。
/// </remarks>
[SecsOrder("S9F5")]
internal sealed class S9F5 : S9HeaderError { }

/// <summary>S9F7：数据非法。</summary>
/// <remarks>
/// 指令格式示例：S7F19 W 本应无正文，Host 却携带 L[0]，因此报正文非法。
/// <code><![CDATA[
/// 'S9F7'                          // 设备→Host，W=0，无应答
/// <B [10] 0x00 0x00 0x87 0x13 0x00 0x00 0x12 0x34 0x56 0x78> // MHEAD：原 S7F19 W 的十字节报头
/// .
/// ]]></code>
/// 字节 0–1：Device ID=0；2：W=1/S=7；3：0x13 为 F=19；4–5：PType/SType=0；6–9：原 System Bytes。
/// 原消息 W=0/1 都可报告，报头须保留其真实 W 位；收到对端 S9 时不再发送本错误，避免循环。
/// </remarks>
[SecsOrder("S9F7")]
internal sealed class S9F7 : S9HeaderError { }

/// <summary>S9F11：数据过长。</summary>
/// <remarks>
/// 指令格式示例：实际收到的 S7F3 W 超过宿主配置的完整 SECS-II 正文上限。
/// <code><![CDATA[
/// 'S9F11'                         // 设备→Host，W=0，无应答
/// <B [10] 0x00 0x00 0x87 0x03 0x00 0x00 0x12 0x34 0x56 0x78> // MHEAD：该次超限 S7F3 W 的原报头
/// .
/// ]]></code>
/// 字节 0–1：Device ID=0；2：W=1/S=7；3：F=3；4–5：PType/SType=0；6–9：原 System Bytes。
/// 原报头按实际消息填写，不包含长度前缀；在业务处理前拒绝，不覆盖旧配方，不把缓冲区大小当作上限。
/// </remarks>
[SecsOrder("S9F11")]
internal sealed class S9F11 : S9HeaderError { }
