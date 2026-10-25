using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GoldMeridian.Reflection;

/// <summary>
///     Finds events by name and exposes typed subscription operations.
/// </summary>
public static class Events
{
    /// <summary>
    ///     Finds an event and binds it to a target for use with <c>+=</c> and
    ///     <c>-=</c>.
    /// </summary>
    /// <typeparam name="T">The type on which to look up the event.</typeparam>
    /// <typeparam name="THandler">The handler delegate type.</typeparam>
    /// <param name="target">
    ///     The event source.  Ignored for static events.
    /// </param>
    /// <param name="name">
    ///     <inheritdoc cref="Get{T, THandler}" path="/param[@name='name']/node()"/>
    /// </param>
    /// <param name="allowParameterDowncasts">
    ///     <inheritdoc cref="Get{T, THandler}" path="/param[@name='allowParameterDowncasts']/node()"/>
    /// </param>
    /// <inheritdoc cref="Get{T, THandler}" path="/exception"/>
    public static EventWrapper<THandler> Wrap<T, THandler>(T? target, string name, bool allowParameterDowncasts = false)
        where THandler : Delegate
    {
        return Get<T, THandler>(name, allowParameterDowncasts).Wrap(target);
    }

    /// <summary>
    ///     Finds a public or non-public event by name, returning
    ///     <see langword="false"/> if it is absent.
    /// </summary>
    /// <inheritdoc cref="Get{T, THandler}" path="/typeparam"/>
    /// <param name="name">
    ///     <inheritdoc cref="Get{T, THandler}" path="/param[@name='name']/node()"/>
    /// </param>
    /// <param name="allowParameterDowncasts">
    ///     <inheritdoc cref="Get{T, THandler}" path="/param[@name='allowParameterDowncasts']/node()"/>
    /// </param>
    /// <param name="eventReference">
    ///     The event reference, or <see langword="null"/> if no event was
    ///     found.
    /// </param>
    /// <remarks>
    ///     Invalid event or handler types still throw an exception.
    /// </remarks>
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

    /// <summary>
    ///     Finds a public or non-public event by name and returns a typed
    ///     reference to it.
    /// </summary>
    /// <typeparam name="T">The type on which to look up the event.</typeparam>
    /// <typeparam name="THandler">
    ///     A delegate type compatible with the event's handler signature.
    /// </typeparam>
    /// <param name="name">
    ///     The event name, preferably supplied with <c>nameof</c>.
    /// </param>
    /// <param name="allowParameterDowncasts">
    ///     Allows narrower handler parameter types.  Incompatible arguments throw when the event is raised.
    /// </param>
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
