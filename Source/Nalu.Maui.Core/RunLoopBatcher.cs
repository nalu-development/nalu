#if IOS || MACCATALYST
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using CoreFoundation;

namespace Nalu;

[SuppressMessage("ReSharper", "InconsistentNaming")]
internal partial class RunLoopBatcher : IDisposable
{
    // Define the delegate for the observer callback
    public delegate void CFRunLoopObserverCallback(IntPtr observer, uint activity, IntPtr info);

    [LibraryImport(DllImportResolver.CoreFoundationLibrary)]
    public static partial IntPtr CFRunLoopObserverCreate(
        IntPtr allocator,
        uint activities,
        [MarshalAs(UnmanagedType.Bool)] bool repeats,
        long order,
        CFRunLoopObserverCallback callback,
        IntPtr info);

    [LibraryImport(DllImportResolver.CoreFoundationLibrary)]
    public static partial void CFRunLoopAddObserver(IntPtr rl, IntPtr observer, IntPtr mode);

    [LibraryImport(DllImportResolver.CoreFoundationLibrary)]
    public static partial void CFRunLoopObserverInvalidate(IntPtr observer);

    // Constants
    public const uint KCFRunLoopBeforeWaiting = 1 << 5;
    public static readonly IntPtr KCFRunLoopDefaultMode = DllImportResolver.GetConstant("kCFRunLoopDefaultMode");

    private CFRunLoopObserverCallback? _callbackDelegate;
    private IntPtr _observerHandle;

    public RunLoopBatcher(Action onBeforeWaiting)
    {
        // Create the observer
        _callbackDelegate = (_, _, _) => { onBeforeWaiting(); };

        _observerHandle = CFRunLoopObserverCreate(
            IntPtr.Zero,
            KCFRunLoopBeforeWaiting,
            false, // Do not repeat
            0,
            _callbackDelegate,
            IntPtr.Zero
        );

        // Attach to the main run loop
        CFRunLoopAddObserver(CFRunLoop.Main.Handle, _observerHandle, KCFRunLoopDefaultMode);
    }

    public void Dispose()
    {
        if (_observerHandle != IntPtr.Zero)
        {
            CFRunLoopObserverInvalidate(_observerHandle);
            _observerHandle = IntPtr.Zero;
        }

        _callbackDelegate = null;
    }
}

// Helper to get constants like kCFRunLoopDefaultMode
internal static class DllImportResolver
{
    public const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "dlsym")]
    private static extern IntPtr Dlsym(IntPtr handle, string symbol);

    public static IntPtr GetConstant(string name)
    {
        // Get the handle for the CoreFoundation framework
        var libHandle = Dlfcn.dlopen(CoreFoundationLibrary, 0); 
        var symbolPtr = Dlsym(libHandle, name);
        
        // Symbols in CF are often pointers to CFStringRefs, 
        // so we dereference to get the actual IntPtr required by the CFRunLoop API
        return Marshal.ReadIntPtr(symbolPtr);
    }
}

// You will also need a simple Dlfcn wrapper for the handle
internal static class Dlfcn
{
    [DllImport("/usr/lib/libSystem.dylib")]
    public static extern IntPtr dlopen(string path, int mode);
}
#endif
