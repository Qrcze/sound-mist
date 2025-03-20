using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SoundMist
{
    public class InterceptKeys
    {
        //179 - play/pause
        // 177-back 176-forward
        public static readonly Dictionary<int, Action> Shortcuts = new()
        {
            //play-pause
            { 179, PlayPause },
            //previous track
            { 177, PreviousTrack },
            //next track
            { 176, NextTrack },
        };

        private static void NextTrack()
        {
            Debug.Print("Next track key pressed");
            NextTrackTriggered?.Invoke();
        }

        private static void PreviousTrack()
        {
            Debug.Print("Previous track key pressed");
            PrevTrackTriggered?.Invoke();
        }

        private static void PlayPause()
        {
            Debug.Print("Play/Pause key pressed");
            PlayPausedTriggered?.Invoke();
        }

        public static event Action? PrevTrackTriggered;

        public static event Action? NextTrackTriggered;

        public static event Action? PlayPausedTriggered;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static readonly LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        public static void Start()
        {
            _hookID = SetHook(_proc);
        }

        public static void End()
        {
            UnhookWindowsHookEx(_hookID);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using Process curProcess = Process.GetCurrentProcess();
            using ProcessModule curModule = curProcess.MainModule;

            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                GetModuleHandle(curModule.ModuleName), 0);
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);

                if (wParam == (IntPtr)WM_KEYDOWN)
                    HandleMediaKeyPress(vkCode);
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static void HandleMediaKeyPress(int vkCode)
        {
            if (Shortcuts.TryGetValue(vkCode, out var shortcut))
                shortcut();
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}