using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GoldMeridian.Reflection;

/// <summary>
///     Reflection utilities for working with events.
/// </summary>
public static class Events
{
    public static EventWrapper<THandler> Wrap<T, THandler>(T? target, string name, bool allowParameterDowncasts = false)
        where THandler : Delegate
    {
        return Get<T, THandler>(name, allowParameterDowncasts).Wrap(target);
    }

    public static bool TryGet<T, THandler>(string name, [NotNullWhen(returnValue: true)] out EventReference<T, THandler>? eventReference, bool allowParameterDowncasts = false)
        where THandler : Delegate
    {
        ArgumentNullException.ThrowIfNull(name);

        if (typeof(T).GetEvent(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy) is { } eventInfo)
        {
            eventReference = EventReference<T, THandler>.Create(eventInfo, allowParameterDowncasts);
            return true;
        }

        eventReference = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EventReference<T, THandler> Get<T, THandler>(string name, bool allowParameterDowncasts = false)
        where THandler : Delegate
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!TryGet<T, THandler>(name, out var eventReference, allowParameterDowncasts))
        {
            throw new MissingMemberException($"Event '{name}' not found on type '{typeof(T).FullName}'");
        }

        return eventReference;
    }
}
