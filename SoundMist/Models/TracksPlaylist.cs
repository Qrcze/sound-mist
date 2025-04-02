using System;
using System.Collections.Generic;
using System.Linq;

namespace SoundMist.Models
{
    public class TracksPlaylist
    {
        public enum Changetype
        {
            Added,
            Removed,
            Cleared,
            Shuffled,
        }

        public event Action<Changetype, IEnumerable<Track>>? ListChanged;

        private readonly List<Track> _items = [];
        private readonly List<Track> _originalQueue = [];
        private readonly object listLock = new();

        public int Count
        {
            get
            {
                lock (listLock)
                {
                    return _items.Count;
                }
            }
        }

        private int _position;

        public void Clear()
        {
            lock (listLock)
            {
                _items.Clear();
                _originalQueue.Clear();
                _position = 0;
            }

            ListChanged?.Invoke(Changetype.Cleared, []);
        }

        public Track? GetLastTrack()
        {
            lock (listLock)
                return _items.LastOrDefault();
        }

        public void Add(Track track)
        {
            lock (listLock)
                _items.Add(track);

            ListChanged?.Invoke(Changetype.Added, [track]);
        }

        public void AddRange(IEnumerable<Track> tracks)
        {
            if (!tracks.Any())
                return;

            lock (listLock)
            {
                _items.AddRange(tracks);
                _originalQueue.AddRange(tracks);
            }

            ListChanged?.Invoke(Changetype.Added, tracks);
        }

        public bool TryGetCurrent(out Track current)
        {
            current = null!;
            lock (listLock)
            {
                if (_items.Count == 0)
                    return false;

                current = _items[_position];
            }
            return true;
        }

        public bool TryMoveForward(out Track nextTrack)
        {
            nextTrack = null!;
            lock (listLock)
            {
                if (_items.Count == 0)
                    return false;
                if (_position + 1 >= _items.Count)
                    return false;

                nextTrack = _items[++_position];
            }
            return true;
        }

        public bool TryMoveBack(out Track previousTrack)
        {
            previousTrack = null!;
            lock (listLock)
            {
                if (_items.Count == 0)
                    return false;
                if (_position - 1 < 0)
                    return false;

                previousTrack = _items[--_position];
            }
            return true;
        }

        public void ChangeShuffle(bool shuffle)
        {
            lock (listLock)
            {
                if (shuffle)
                {
                    for (int i = _position + 1; i < _items.Count; i++)
                    {
                        int newIndex = Globals.Random.Next(_position + 1, _items.Count);
                        if (i != newIndex)
                            (_items[i], _items[newIndex]) = (_items[newIndex], _items[i]);
                    }
                }
                else
                {
                    var currTrack = _items[_position];
                    _items.Clear();
                    _items.AddRange(_originalQueue);
                    _position = _items.IndexOf(currTrack);
                }

                ListChanged?.Invoke(Changetype.Shuffled, _items);
            }
        }

        public void RemoveAll(Predicate<Track> match)
        {
            lock (listLock)
            {
                var itemsRemoved = _items.Where(x => match(x)).ToArray();

                var nextValidTrack = _items.Skip(_position).FirstOrDefault(x => !match(x));

                _items.RemoveAll(match);
                _originalQueue.RemoveAll(match);

                if (nextValidTrack == null)
                    _position = 0;
                else
                    _position = _items.IndexOf(nextValidTrack);

                ListChanged?.Invoke(Changetype.Removed, itemsRemoved);
            }
        }
    }
}