using Avalonia;
using Avalonia.Controls;

namespace SoundMist.Controls;

public class SettingsOption : ContentControl
{
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<SettingsOption, string>(nameof(Header));

    public string Header
    {
        get => this.GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }
}