using System;
using System.Collections.Generic;

namespace JulyArch
{
    internal sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new();
        private readonly Dictionary<object, List<(Type type, Delegate handler)>> _ownerMap = new();
        private bool _disposed;

        public void Subscribe<T>(Action<T> handler, object owner)
        {
            if (_disposed) return;

            var type = typeof(T);
            _handlers[type] = _handlers.TryGetValue(type, out var existing)
                ? Delegate.Combine(existing, handler)
                : handler;

            if (!_ownerMap.TryGetValue(owner, out var list))
            {
                list = new List<(Type, Delegate)>();
                _ownerMap[owner] = list;
            }

            list.Add((type, handler));
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (_disposed) return;

            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var existing))
            {
                var result = Delegate.Remove(existing, handler);
                if (result == null)
                    _handlers.Remove(type);
                else
                    _handlers[type] = result;
            }
        }

        public void UnsubscribeAll(object owner)
        {
            if (_disposed) return;
            if (!_ownerMap.TryGetValue(owner, out var list)) return;

            foreach (var (type, handler) in list)
            {
                if (_handlers.TryGetValue(type, out var existing))
                {
                    var result = Delegate.Remove(existing, handler);
                    if (result == null)
                        _handlers.Remove(type);
                    else
                        _handlers[type] = result;
                }
            }

            _ownerMap.Remove(owner);
        }

        public void Publish<T>(T eventData)
        {
            if (_disposed) return;
            if (!_handlers.TryGetValue(typeof(T), out var d) || d is not Action<T> combined)
                return;

            foreach (var handler in combined.GetInvocationList())
            {
                try
                {
                    ((Action<T>)handler).Invoke(eventData);
                }
                catch (Exception e)
                {
                    ArchServices.Logger.LogError(
                        $"[EventBus] Publish<{typeof(T).Name}> subscriber threw: {e}");
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _handlers.Clear();
            _ownerMap.Clear();
        }
    }
}
