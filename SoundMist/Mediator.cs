using System;
using System.Collections.Generic;

namespace SoundMist
{
    public enum MediatorEvent
    {
        OpenSettings,
        OpenTrackInfo,
        OpenUserInfo,
        OpenPlaylistInfo
    }

    public class Mediator
    {
        public static Mediator Default { get; } = new();

        private readonly Dictionary<MediatorEvent, List<Action<object?>>> _items = [];

        public void Register(MediatorEvent @event, Action<object?> action)
        {
            if (_items.TryGetValue(@event, out var actionList))
                actionList.Add(action);
            else
                _items[@event] = [action];
        }

        public void Invoke(MediatorEvent @event, object? value = null)
        {
            if (_items.TryGetValue(@event, out var actions))
                foreach (var action in actions)
                    action(value);
        }
    }
}