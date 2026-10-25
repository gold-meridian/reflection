using System;

namespace GoldMeridian.Reflection;

/// <summary>
///     An event wrapper supporting <c>+=</c> and <c>-=</c>, including through
///     properties.
/// </summary>
/// <typeparam name="THandler">The handler delegate type.</typeparam>
/// <remarks>
///     Construct a wrapper or obtain one from
///     <see cref="Events.Wrap{T, THandler}"/> before use.
/// </remarks>
public readonly struct EventWrapper<THandler>
    where THandler : Delegate
{
    private readonly Action<THandler?>? add;
    private readonly Action<THandler?>? remove;
    private readonly IEventAccessors<THandler>? reference;
    private readonly object? target;

    /// <summary>
    ///     Wraps callbacks that subscribe and unsubscribe handlers.
    /// </summary>
    /// <param name="add">
    ///     A callback such as <c>handler =&gt; source.Changed += handler</c>.
    /// </param>
    /// <param name="remove">
    ///     A callback such as <c>handler =&gt; source.Changed -= handler</c>.
    /// </param>
    /// <remarks>
    ///     Use <see cref="Events.Wrap{T, THandler}"/> when handler types need
    ///     adaptation.
    /// </remarks>
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

    /// <summary>
    ///     Adds a handler to the wrapped event.
    /// </summary>
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

    /// <summary>
    ///     Removes a handler from the wrapped event.
    /// </summary>
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

    /// <inheritdoc cref="Add"/>
    public void operator += (THandler? handler)
    {
        Add(handler);
    }

    /// <inheritdoc cref="Remove"/>
    public void operator -= (THandler? handler)
    {
        Remove(handler);
    }

    /// <summary>
    ///     Adds a handler to the wrapped event and returns the wrapper.
    /// </summary>
    public static EventWrapper<THandler> operator +(EventWrapper<THandler> wrapper, THandler? handler)
    {
        wrapper.Add(handler);
        return wrapper;
    }

    /// <summary>
    ///     Removes a handler from the wrapped event and returns the wrapper.
    /// </summary>
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
