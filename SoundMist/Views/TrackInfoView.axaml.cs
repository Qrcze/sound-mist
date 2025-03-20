using Avalonia.Controls;
using SoundMist.ViewModels;

namespace SoundMist.Views;

public partial class TrackInfoView : UserControl
{
    public TrackInfoView()
    {
        InitializeComponent();
        DataContext = App.GetService<TrackInfoViewModel>();
    }
}