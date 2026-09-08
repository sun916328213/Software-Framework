using System.Reflection;

var asm = typeof(Prism.Dialogs.DialogService).Assembly;
var module = asm.GetModules()[0];

// 从 DisplayClass6_0 的 Loaded handler 拿到 InitializeListener 的 token
var t6 = asm.GetTypes().First(t => t.Name == "<>c__DisplayClass6_0");
var loaded = t6.GetMethod("<ConfigureDialogWindowEvents>b__1", BindingFlags.NonPublic | BindingFlags.Instance)!;
var loadedIl = loaded.GetMethodBody()!.GetILAsByteArray();
// 0x73 newobj / 0x28 call / 0x6F callvirt 都带 4 字节 token，扫描找 InitializeListener
MethodBase? init = null;
for (int i = 0; i + 5 <= loadedIl.Length; i++)
{
    if (loadedIl[i] is 0x28 or 0x6F or 0x73)
    {
        var tok = BitConverter.ToInt32(loadedIl, i + 1);
        var mi = module.ResolveMethod(tok);
        if (mi?.Name == "InitializeListener")
        {
            init = mi;
            break;
        }
    }
}

Console.WriteLine($"InitializeListener resolved: {init?.DeclaringType?.FullName}.{init?.Name} static={init?.IsStatic} pub={init?.IsPublic}");
Console.WriteLine($"  params: {string.Join(", ", init?.GetParameters().Select(p => p.ParameterType.FullName + " " + p.Name) ?? [])}");

if (init is not null)
{
    var il = init.GetMethodBody()!.GetILAsByteArray();
    Console.WriteLine($"===== InitializeListener IL ({il.Length} bytes) =====");
    int i = 0;
    while (i < il.Length)
    {
        byte op = il[i];
        switch (op)
        {
            case 0x28: case 0x6F: case 0x73: case 0x33: // call callvirt newobj and obj?
                int tok = BitConverter.ToInt32(il, i + 1);
                try
                {
                    var mi = module.ResolveMethod(tok);
                    Console.WriteLine($"  [{i:X3}] {(op switch { 0x28 => "call", 0x6F => "callvirt", 0x73 => "newobj", _ => "calli" })} {mi?.DeclaringType?.FullName}::{mi?.Name}({string.Join(",", mi?.GetParameters().Select(p => p.ParameterType.Name)) ?? ""})");
                }
                catch (Exception ex) { Console.WriteLine($"  [{i:X3}] token 0x{tok:X8} fail {ex.Message}"); }
                i += 5; break;
            case 0x72:
                try { Console.WriteLine($"  [{i:X3}] ldstr \"{module.ResolveString(BitConverter.ToInt32(il, i + 1))}\""); }
                catch { }
                i += 5; break;
            case 0x7B: case 0x7C: case 0x7D: case 0x7E: case 0x7F: case 0x80: case 0x8C: case 0x8D: case 0xA3: case 0xA5:
                tok = BitConverter.ToInt32(il, i + 1);
                try
                {
                    var fi = module.ResolveField(tok);
                    Console.WriteLine($"  [{i:X3}] {(op switch
                    {
                        0x7B => "ldfld", 0x7C => "ldflda", 0x7D => "stfld",
                        0x7E => "ldsfld", 0x7F => "stsfld", 0x8C => "ldflda?",
                        0x8D => "ldsflda?", 0xA3 => "newobj?",
                        _ => "stobj"
                    })} {fi?.FieldType.Name} {fi?.DeclaringType?.Name}::{fi?.Name} (private={fi?.IsPrivate} internal={fi?.IsAssembly})");
                }
                catch (Exception ex) { Console.WriteLine($"  [{i:X3}] field token 0x{tok:X8} fail: {ex.Message}"); }
                i += 5; break;
            default:
                Console.WriteLine($"  [{i:X3}] op 0x{op:X2} {op switch
                {
                    0x00 => "nop", 0x02 => "ldarg.0", 0x03 => "ldarg.1", 0x14 => "ldnull",
                    0x25 => "dup", 0x26 => "pop", 0x2A => "ret",
                    0x6F => "callvirt", _ => ""
                }}");
                i++; break;
        }
    }
}
