using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GoldMeridian.Reflection;

public sealed partial class EventReference<TTarget, THandler>
    where THandler : Delegate
{
    private delegate void Accessor(TTarget? target, THandler handler);

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
    public bool TryAdd(TTarget? target, THandler handler)
    {
        if (add is null)
        {
            return false;
        }

        add.Invoke(target, handler);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TTarget? target, THandler handler)
    {
        add?.Invoke(target, handler);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRemove(TTarget? target, THandler handler)
    {
        if (remove is null)
        {
            return false;
        }

        remove.Invoke(target, handler);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Remove(TTarget? target, THandler handler)
    {
        remove?.Invoke(target, handler);
    }

    public static EventReference<TTarget, THandler> Create(EventInfo info)
    {
        if (info is null)
        {
            throw new ArgumentNullException(nameof(info));
        }

        /*
        if (info.EventHandlerType != typeof(THandler))
        {
            throw new ArgumentException($"Event handler type '{info.EventHandlerType}' does not match expected type '{typeof(THandler)}'.", nameof(info));
        }
        */

        if (info.EventHandlerType is null)
        {
            throw new ArgumentException($"Event handler type '{info.EventHandlerType}' is null.", nameof(info));
        }

        if (typeof(THandler).GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance) is not { } handlerInvoke)
        {
            throw new ArgumentException($"Stubbed handler type '{typeof(THandler)}' does not have an 'Invoke' method.", nameof(info));
        }

        var add = info.GetAddMethod(nonPublic: true);
        var remove = info.GetRemoveMethod(nonPublic: true);

        /*
        Debug.Assert(add?.IsStatic == remove?.IsStatic);
        var isStatic = add?.IsStatic ?? remove?.IsStatic;
        if (!isStatic.HasValue)
        {
            throw new ArgumentException($"Event handler '{info.Name}' had no add or remove method.", nameof(info));
        }
        */

        // Ensure the actual type is compatible, otherwise throw.  Sort of sucks
        // that we have to use this API for this, but whatever.

        if (add?.GetParameters().LastOrDefault() is { } addParam)
        {
            handlerInvoke.CreateDelegate(
                addParam.ParameterType,
                target: null
            );
        }

        if (remove?.GetParameters().LastOrDefault() is { } removeParam)
        {
            handlerInvoke.CreateDelegate(
                removeParam.ParameterType,
                target: null
            );
        }

        return new EventReference<TTarget, THandler>(
            info,
            CreateAccessor(add),
            CreateAccessor(remove)
        );
    }

    private static Accessor? CreateAccessor(MethodInfo? method)
    {
        if (method is null)
        {
            return null;
        }

        if (method.IsStatic)
        {
            // Static methods need a wrapper to ignore the instance parameter.
            return (_, handler) => method.Invoke(null, [handler]);
        }

        try
        {
            return (Accessor)Delegate.CreateDelegate(typeof(Accessor), method);
        }
        catch
        {
            // Unfortunate slow path when the delegates aren't identical.
            return (instance, handler) => method.Invoke(instance, [handler]);
        }
    }
}
