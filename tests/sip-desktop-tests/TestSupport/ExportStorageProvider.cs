using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Platform.Storage;

namespace Clc.Sip.Desktop.Tests.TestSupport;

internal sealed class TestStorageProviderFactory : IStorageProviderFactory
{
    internal static ConditionalWeakTable<TopLevel, ExportStorageProvider> Windows { get; } = new();

    // Returning null lets TopLevel fall back to the headless provider for other windows.
    public IStorageProvider CreateProvider(TopLevel topLevel) =>
        Windows.TryGetValue(topLevel, out var provider) ? provider.CreateProvider() : null!;
}

internal sealed class ExportStorageProvider
{
    public List<FilePickerSaveOptions> Saves { get; } = [];
    public TaskCompletionSource<IStorageFile?> Result { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public IStorageProvider CreateProvider() => StorageProxy.Create<IStorageProvider>((name, args) =>
    {
        if (name != nameof(IStorageProvider.SaveFilePickerAsync)) { throw new NotSupportedException(name); }
        Saves.Add((FilePickerSaveOptions)args![0]!);
        return Result.Task;
    });
}

// Avalonia marks storage interfaces as NotClientImplementable. Proxy them only in tests,
// leaving production file selection on the normal TopLevel.StorageProvider path.
public class StorageProxy : DispatchProxy
{
    private Func<string, object?[]?, object?> invoke = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => invoke(method!.Name, args);

    internal static T Create<T>(Func<string, object?[]?, object?> invoke) where T : class
    {
        var proxy = Create<T, StorageProxy>();
        ((StorageProxy)(object)proxy).invoke = invoke;
        return proxy;
    }

    internal static IStorageFile File(string path) => Create<IStorageFile>((name, _) =>
        name == nameof(IStorageFile.OpenWriteAsync)
            ? Task.FromResult<Stream>(System.IO.File.OpenWrite(path)) : throw new NotSupportedException(name));
}
