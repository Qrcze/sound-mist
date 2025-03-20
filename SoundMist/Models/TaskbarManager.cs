#if OS_WINDOWS

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SoundMist.Models;

internal class TaskbarManager
{
    private static ITaskbarList3 _instance = null!;
    private static nint _ownerHandle;

    public static ITaskbarList3 Instance
    {
        get
        {
            _instance ??= (ITaskbarList3)new CTaskbarList();
            return _instance;
        }
    }

    static IntPtr OwnerHandle
    {
        get
        {
            if (_ownerHandle == IntPtr.Zero)
            {
                Process currentProcess = Process.GetCurrentProcess();
                if (currentProcess == null || currentProcess.MainWindowHandle == IntPtr.Zero)
                    return IntPtr.Zero;

                _ownerHandle = currentProcess.MainWindowHandle;
            }

            return _ownerHandle;
        }
    }

    public static void SetProgressValue(int value, int maxValue)
    {
        if (OwnerHandle == IntPtr.Zero)
            return;
        Instance.SetProgressValue(OwnerHandle, (ulong)value, (ulong)maxValue);
    }

    public static void SetProgressState(TaskbarProgressBarStatus state)
    {
        if (OwnerHandle == IntPtr.Zero)
            return;
        Instance.SetProgressState(OwnerHandle, state);
    }
}

public enum TaskbarProgressBarStatus
{
    NoProgress = 0,
    Indeterminate = 0x1,
    Normal = 0x2,
    Error = 0x4,
    Paused = 0x8
}

[ComImport]
[Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITaskbarList3
{
    [PreserveSig]
    void HrInit();

    [PreserveSig]
    void AddTab(IntPtr hwnd);

    [PreserveSig]
    void DeleteTab(IntPtr hwnd);

    [PreserveSig]
    void ActivateTab(IntPtr hwnd);

    [PreserveSig]
    void SetActiveAlt(IntPtr hwnd);

    [PreserveSig]
    void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

    [PreserveSig]
    void SetProgressValue(IntPtr hwnd, UInt64 ullCompleted, UInt64 ullTotal);

    [PreserveSig]
    void SetProgressState(IntPtr hwnd, TaskbarProgressBarStatus state);
}

[ComImport]
[Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
[ClassInterface(ClassInterfaceType.None)]
internal class CTaskbarList
{
}

#endif