using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using ScriptEditor.Models;

namespace ScriptEditor.Services;

/// <summary>Source-compatible execution entry points while callers migrate to the P3 session API.</summary>
public static class ScriptItemExecutionExtensions
{
    private sealed class Binding
    {
        public required ScriptEngine Engine;
        public required PropertyChangedEventHandler Handler;
    }

    private static readonly ConditionalWeakTable<ScriptItem, Binding> Bindings = new();

    public static Task<bool> ExecuteAsync(this ScriptItem item) => GetEngine(item).RunAsync();
    public static Task LoopRunAsync(this ScriptItem item) => GetEngine(item).LoopRunAsync();
    public static Task PreCompileAsync(this ScriptItem item) => GetEngine(item).PreCompileAsync(item.Code);
    public static void Terminate(this ScriptItem item)
    {
        if (Bindings.TryGetValue(item, out var binding)) binding.Engine.Terminate();
    }
    public static void StopLoop(this ScriptItem item)
    {
        if (Bindings.TryGetValue(item, out var binding)) binding.Engine.StopLoop();
    }
    public static void DisposeExecution(this ScriptItem item)
    {
        if (Bindings.TryGetValue(item, out var binding))
        {
            binding.Engine.PropertyChanged -= binding.Handler;
            Bindings.Remove(item);
        }
        ScriptItemRuntime.Shared.Release(item);
    }

    private static ScriptEngine GetEngine(ScriptItem item)
    {
        var binding = Bindings.GetValue(item, script =>
        {
            var engine = ScriptItemRuntime.Shared.GetOrCreate(script);
            PropertyChangedEventHandler handler = (_, e) => MirrorState(script, engine, e.PropertyName);
            engine.PropertyChanged += handler;
            return new Binding { Engine = engine, Handler = handler };
        });
        binding.Engine.LoopInterval = item.LoopInterval;
        return binding.Engine;
    }

    private static void MirrorState(ScriptItem item, ScriptEngine engine, string? propertyName)
    {
        void Apply()
        {
            switch (propertyName)
            {
                case nameof(ScriptEngine.OutputText): item.OutputText = engine.OutputText; break;
                case nameof(ScriptEngine.StatusText): item.StatusText = engine.StatusText; break;
                case nameof(ScriptEngine.IsRunning): item.IsRunning = engine.IsRunning; break;
                case nameof(ScriptEngine.IsLooping): item.IsLooping = engine.IsLooping; break;
                case nameof(ScriptEngine.ExecutionTime): item.ExecutionTime = engine.ExecutionTime; break;
            }
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess()) dispatcher.InvokeAsync(Apply);
        else Apply();
    }
}
