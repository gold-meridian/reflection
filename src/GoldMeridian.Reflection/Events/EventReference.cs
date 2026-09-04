using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GoldMeridian.Reflection;

public sealed partial class EventReference<TTarget, THandler>
    where THandler : Delegate
{
    private delegate void Accessor(TTarget target, THandler handler);

    public EventInfo Info { get; }

    public Type HandlerType => typeof(THandler);

    private readonly Accessor? add;
    private readonly Accessor? remove;

    private EventReference(
        EventInfo info,
        Accessor? add,
        Accessor? remove
    )
    {
        Info = info;
        this.add = add;
        this.remove = remove;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(TTarget target, THandler handler)
    {
        if (add is null)
        {
            return false;
        }

        add.Invoke(target, handler);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TTarget target, THandler handler)
    {
        add?.Invoke(target, handler);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRemove(TTarget target, THandler handler)
    {
        if (remove is null)
        {
            return false;
        }

        remove.Invoke(target, handler);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Remove(TTarget target, THandler handler)
    {
        remove?.Invoke(target, handler);
    }

    public static EventReference<TTarget, THandler> Create(EventInfo info)
    {
        if (info is null)
        {
            throw new ArgumentNullException(nameof(info));
        }

        if (info.EventHandlerType != typeof(THandler))
        {
            throw new ArgumentException($"Event handler type '{info.EventHandlerType}' does not match expected type '{typeof(THandler)}'.", nameof(info));
        }

        var add = info.GetAddMethod(nonPublic: true);
        var remove = info.GetRemoveMethod(nonPublic: true);

        return new EventReference<TTarget, THandler>(
            info,
            add?.CreateDelegate(typeof(Accessor)) as Accessor,
            remove?.CreateDelegate(typeof(Accessor)) as Accessor
        );
    }
}
