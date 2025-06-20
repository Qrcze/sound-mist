#if !OS_LINUX //Linux now has MPRIS taking care of media keys
using SharpHook;
using SharpHook.Native;
using System;

namespace SoundMist
{
    public static class KeyboardHook
    {
        private static SimpleGlobalHook _hook;

        public static event Action? PrevTrackTriggered;

        public static event Action? NextTrackTriggered;

        public static event Action? PlayPausedTriggered;

        public static event Action? PlayTriggered;

        public static event Action? PauseTriggered;

        internal static void Run()
        {
            _hook = new SimpleGlobalHook(GlobalHookType.Keyboard, null, true);
            _hook.KeyPressed += KeyPressed;
            _hook.RunAsync();
        }

        private static void KeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            switch (e.Data.KeyCode)
            {
                case KeyCode.VcPause:
                    e.SuppressEvent = true;
                    PauseTriggered?.Invoke();
                    break;

                case KeyCode.VcMediaPlay:
                    e.SuppressEvent = true;
                    PlayPausedTriggered?.Invoke();
                    break;

                case KeyCode.VcMediaPrevious:
                    e.SuppressEvent = true;
                    PrevTrackTriggered?.Invoke();
                    break;

                case KeyCode.VcMediaNext:
                    e.SuppressEvent = true;
                    NextTrackTriggered?.Invoke();
                    break;
            }
        }
    }
}
#endif