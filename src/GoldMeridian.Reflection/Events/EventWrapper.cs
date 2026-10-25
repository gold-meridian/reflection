using System;

namespace GoldMeridian.Reflection;

/// <summary>
///     A bound event exposing <see langword="add"/>/<see langword="remove"/>
///     operations.
/// </summary>
public readonly struct EventWrapper<THandler>
    where THandler : Delegate
{
    private readonly Action<THandler?>? add;
    private readonly Action<THandler?>? remove;
    private readonly IEventAccessors<THandler>? reference;
    private readonly object? target;

    public EventWrapper(Action<THandler?> add, Action<THandler?> remove)
    {
        ArgumentNullException.ThrowIfNull(add);
        ArgumentNullException.ThrowIfNull(remove);
        this.add = add;
        this.remove = remove;
        reference = null;
        target = null;
    }

    internal EventWrapper(IEventAccessors<THandler> reference, object? target)
    {
        this.reference = reference;
        this.target = target;
        add = null;
        remove = null;
    }

    public void Add(THandler? handler)
    {
        if (reference is not null)
        {
            reference.Add(target, handler);
        }
        else if (add is not null)
        {
            add(handler);
        }
        else
        {
            throw new InvalidOperationException("The event wrapper is uninitialized.");
        }
    }

    public void Remove(THandler? handler)
    {
        if (reference is not null)
        {
            reference.Remove(target, handler);
        }
        else if (remove is not null)
        {
            remove(handler);
        }
        else
        {
            throw new InvalidOperationException("The event wrapper is uninitialized.");
        }
    }

    public void operator += (THandler? handler)
    {
        Add(handler);
    }

    public void operator -= (THandler? handler)
    {
        Remove(handler);
    }

    public static EventWrapper<THandler> operator +(EventWrapper<THandler> wrapper, THandler? handler)
    {
        wrapper.Add(handler);
        return wrapper;
    }

    public static EventWrapper<THandler> operator -(EventWrapper<THandler> wrapper, THandler? handler)
    {
        wrapper.Remove(handler);
        return wrapper;
    }
}

internal interface IEventAccessors<in THandler> where THandler : Delegate
{
    void Add(object? target, THandler? handler);

    void Remove(object? target, THandler? handler);
}
