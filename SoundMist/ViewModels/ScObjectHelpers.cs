using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using SoundMist.Helpers;
using SoundMist.Models;
using SoundMist.Models.Audio;
using SoundMist.Models.SoundCloud;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SoundMist.ViewModels
{
    internal class ScObjectHelpers
    {
        private static readonly IDatabase _database = null!;
        private static readonly ILogger _logger = null!;
        private static readonly IMusicPlayer _musicPlayer = null!;
        private static readonly SoundCloudQueries _queries = null!;

        static ScObjectHelpers()
        {
            _database = App.GetService<IDatabase>();
            _logger = App.GetService<ILogger>();
            _musicPlayer = App.GetService<IMusicPlayer>();
            _queries = App.GetService<SoundCloudQueries>();
        }

        public static void OpenAboutPage(object? sender)
        {
            var c = sender as Control;
            var item = c.FindLogicalAncestorOfType<ListBoxItem>()?.DataContext;
            OpenScObjectView(item);
        }

        public static void OpenScObjectView(object? item)
        {
            if (item is User user)
            {
                _database.AddUser(user);
                Mediator.Default.Invoke(MediatorEvent.OpenUserInfo, user);
            }
            else if (item is Track track)
            {
                _database.AddTrack(track);
                Mediator.Default.Invoke(MediatorEvent.OpenTrackInfo, track);
            }
            else if (item is Playlist playlist)
            {
                _database.AddPlaylist(playlist);
                Mediator.Default.Invoke(MediatorEvent.OpenPlaylistInfo, playlist);
            }
            else
            {
                _logger.Error($"Tried opening an unhandled SoundCloud object: {item.GetType()}");
                NotificationManager.Show(new("Unhandled object type", "Please check the logs for further info.", Avalonia.Controls.Notifications.NotificationType.Error));
            }
        }

        public static void Playlist_ViewMore(object? sender)
        {
            var c = sender as Control;
            var item = c.FindLogicalAncestorOfType<ListBoxItem>()?.DataContext as Playlist ?? throw new InvalidCastException($"expected a {nameof(Playlist)} in the ListBoxItem's DataContext");

            Mediator.Default.Invoke(MediatorEvent.OpenPlaylistInfo, item);
        }

        public static void PlaylistItem_AboutUser(object? sender)
        {
            var c = sender as Control;
            var item = c.FindLogicalAncestorOfType<ListBoxItem>();
            if (item?.DataContext is Playlist playlist)
                OpenScObjectView(playlist.User);
        }

        public static void TrackItem_AboutUser(object? sender)
        {
            var c = sender as Control;
            var item = c.FindLogicalAncestorOfType<ListBoxItem>();
            if (item?.DataContext is Track track)
                OpenScObjectView(track.User!);
        }

        public static async Task ListBox_DoubleTapped_PlaylistItem(object? sender)
        {
            if (sender is not ListBox listBox)
                return;

            var parent = listBox.FindAncestorOfType<ListBoxItem>() ?? throw new NullReferenceException("Couldn't find the playlist track's parent ListBoxItem");
            if (parent.DataContext is not Playlist playlist)
                throw new ArgumentException("Clicked item in playlist is not actually within a playlist ListBox");

            await PlayFromPlaylist(playlist, listBox.SelectedIndex);
        }

        public static async Task PlayFromPlaylist(Playlist playlist, int selectedIndex)
        {
            IEnumerable<Track> tracks = playlist.FirstFiveTracks;
            if (playlist.Tracks.Count > 5)
            {
                try
                {
                    var trackIds = playlist.Tracks.Except(tracks).Where(x => x.User is null).Select(x => x.Id);
                    var restOfTracks = await _queries.GetTracksById(trackIds);
                    if (restOfTracks != null && restOfTracks.Count != 0)
                        tracks = tracks.Concat(restOfTracks);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed retrieving the tracks from IDs, {ex.Message}");
                    NotificationManager.Show(new("Failed playing playlist", "Please check the logs for further info.", Avalonia.Controls.Notifications.NotificationType.Error));
                }
            }

            if (tracks.Any())
                await _musicPlayer.LoadNewQueue(tracks.Skip(selectedIndex));
        }
    }
}