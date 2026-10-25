using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GoldMeridian.Reflection;

internal sealed class EventHandlerAdapter
{
    private sealed record InvocationAdapter(WeakReference<Delegate> Source, WeakReference<Delegate> Adapted);

    private static readonly ConditionalWeakTable<Type, EventHandlerAdapter> adapters = new();
    private static readonly ConditionalWeakTable<Type, EventHandlerAdapter> downcast_adapters = new();
    private readonly ConditionalWeakTable<Delegate, Delegate> converted = new();
    private readonly ConditionalWeakTable<Delegate, WeakReference<Delegate>>? collectibleConverted;
    private readonly ConditionalWeakTable<Delegate, Delegate>.CreateValueCallback convert;
    private readonly List<InvocationAdapter> invocationAdapters;
    private readonly ParameterDowncastCache? downcasts;

    public Type HandlerType { get; }

    public bool HasVariance { get; }

    private EventHandlerAdapter(Type handlerType, bool allowParameterDowncasts)
    {
        HandlerType = handlerType;
        if (allowParameterDowncasts)
        {
            downcasts = new ParameterDowncastCache(handlerType);
            invocationAdapters = Get(handlerType).invocationAdapters;
        }
        else
        {
            invocationAdapters = [];
        }

        if (handlerType.IsCollectible)
        {
            collectibleConverted = new ConditionalWeakTable<Delegate, WeakReference<Delegate>>();
        }

        HasVariance = handlerType.IsGenericType && Array.Exists(
            handlerType.GetGenericTypeDefinition().GetGenericArguments(),
            static parameter => (parameter.GenericParameterAttributes & GenericParameterAttributes.VarianceMask) != 0
        );
        convert = ConvertCore;
    }

    public static EventHandlerAdapter Get(Type handlerType, bool allowParameterDowncasts = false)
    {
        return allowParameterDowncasts
            ? downcast_adapters.GetValue(handlerType, static type => new EventHandlerAdapter(type, allowParameterDowncasts: true))
            : adapters.GetValue(handlerType, static type => new EventHandlerAdapter(type, allowParameterDowncasts: false));
    }

    public void ValidateDowncasts(Type sourceType)
    {
        _ = downcasts!.Get(sourceType);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Delegate? Convert(Delegate? handler)
    {
        if (handler is null || handler.GetType() == HandlerType)
        {
            return handler;
        }

        if (collectibleConverted is not null)
        {
            // We can't keep strong references to collectible handlers, so we
            // bind weak references.
            var weak = collectibleConverted.GetValue(handler, static _ => new WeakReference<Delegate>(null!));
            if (!weak.TryGetTarget(out var result))
            {
                result = ConvertCore(handler);
                weak.SetTarget(result);
            }

            return result;
        }

        return converted.GetValue(handler, convert);
    }

    private Delegate ConvertCore(Delegate handler)
    {
        var result = default(Delegate);
        foreach (var entry in Delegate.EnumerateInvocationList(handler))
        {
            // Need to rebind individual entries to keep ordering and
            // partial/duplicate removals.  This also gives us equality across
            // multiple created handlers, which is necessary for normal removal
            // syntax.
            result = Delegate.Combine(result, ConvertEntry(entry));
        }

        return result!;
    }

    private Delegate ConvertEntry(Delegate entry)
    {
        if (downcasts?.Get(entry.GetType()).Factory is { } factory)
        {
            return GetInvocationAdapter(entry, factory);
        }

        var method = entry.Method;
        if (method.DeclaringType != entry.GetType() || method.Name != "Invoke" || entry.Target is not null)
        {
            return method.CreateDelegate(HandlerType, entry.Target);
        }

        // Delegates of native function pointers expose Invoke as their Method
        // and won't have a target.  Rebinding that pair would result in a
        // delegate closed over null that loses the native pointer, which is
        // bad.  So we need to invoke the original delegate instead, where
        // possible.
        return GetInvocationAdapter(entry, factory: null);
    }

    private Delegate GetInvocationAdapter(Delegate entry, Func<Delegate, Delegate>? factory)
    {
        lock (invocationAdapters)
        {
            for (var i = invocationAdapters.Count - 1; i >= 0; i--)
            {
                var candidate = invocationAdapters[i];
                if (!candidate.Adapted.TryGetTarget(out var existing) || !candidate.Source.TryGetTarget(out var source))
                {
                    invocationAdapters.RemoveAt(i);
                }
                else if (entry.Equals(source))
                {
                    return existing;
                }
            }

            // Now canonicalize equal delegates so cloned handlers and other
            // EventReferences can remove existing adapters.
            var adapted = factory is null
                ? entry.GetType().GetMethod("Invoke")!.CreateDelegate(HandlerType, entry)
                : factory(entry);
            invocationAdapters.Add(new InvocationAdapter(new WeakReference<Delegate>(entry), new WeakReference<Delegate>(adapted)));
            return adapted;
        }
    }
}
