using Avalonia.Controls;
using Avalonia.Controls.Notifications;

namespace SoundMist
{
    internal static class NotificationManager
    {
        private static TopLevel _toplevel;

        public static TopLevel Toplevel
        {
            get => _toplevel;
            set
            {
                _toplevel = value;
                if (value != null)
                    _manager = new WindowNotificationManager(value);
            }
        }

        private static INotificationManager? _manager;

        /// <summary>
        /// Shows a toast notification in the top-right corner of the window. Needs to be run on the UI thread.
        /// </summary>
        /// <param name="notification"></param>
        public static void Show(Notification notification)
        {
            _manager?.Show(notification);
        }
    }
}