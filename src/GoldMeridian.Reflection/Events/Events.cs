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
    public static bool TryGet<T, THandler>(string name, [NotNullWhen(returnValue: true)] out EventReference<T, THandler>? eventReference)
        where THandler : Delegate
    {
        ArgumentNullException.ThrowIfNull(name);

        if (typeof(T).GetEvent(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy) is { } eventInfo)
        {
            eventReference = EventReference<T, THandler>.Create(eventInfo);
            return true;
        }

        eventReference = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EventReference<T, THandler> Get<T, THandler>(string name)
        where THandler : Delegate
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!TryGet<T, THandler>(name, out var eventReference))
        {
            throw new MissingMemberException($"Event '{name}' not found on type '{typeof(T).FullName}'");
        }

        return eventReference;
    }
}
