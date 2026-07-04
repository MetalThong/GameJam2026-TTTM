using System;
using System.Collections.Generic;
using UnityEngine;

public static class EventBus
{
    private static readonly Dictionary<Type, List<Delegate>> _type2Subscribers = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForPlayMode()
    {
        Clear();
    }

    public static void Subscribe<TEvent>(Action<TEvent> handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        Type eventType = typeof(TEvent);
        if (!_type2Subscribers.TryGetValue(eventType, out List<Delegate> handlers))
        {
            handlers = new List<Delegate>();
            _type2Subscribers[eventType] = handlers;
        }

        if (!handlers.Contains(handler))
        {
            handlers.Add(handler);
        }
    }

    public static void Unsubscribe<TEvent>(Action<TEvent> handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        Type eventType = typeof(TEvent);
        if (!_type2Subscribers.TryGetValue(eventType, out List<Delegate> handlers))
        {
            return;
        }

        handlers.Remove(handler);
        if (handlers.Count == 0)
        {
            _type2Subscribers.Remove(eventType);
        }
    }

    public static void Publish<TEvent>(TEvent eventData)
    {
        Type eventType = typeof(TEvent);
        if (!_type2Subscribers.TryGetValue(eventType, out List<Delegate> handlers) || handlers.Count == 0)
        {
            return;
        }

        Delegate[] snapshot = handlers.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            try
            {
                ((Action<TEvent>)snapshot[i]).Invoke(eventData);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    public static void Clear()
    {
        _type2Subscribers.Clear();
    }
}
