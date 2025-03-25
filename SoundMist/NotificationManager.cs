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

        public static void Show(Notification notification)
        {
            _manager?.Show(notification);
        }
    }
}