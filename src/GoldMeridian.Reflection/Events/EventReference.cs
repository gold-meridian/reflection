using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GoldMeridian.Reflection;

/// <summary>
///     Provides typed subscription operations for an event, including
///     non-public events.
/// </summary>
/// <typeparam name="TTarget">The type of the target object.</typeparam>
/// <typeparam name="THandler">
///     A delegate type compatible with the event's handler signature.
/// </typeparam>
/// <remarks>
///     Instance events require a compatible, non-null target.  Static events
///     ignore the target.
/// </remarks>
public sealed partial class EventReference<TTarget, THandler>
    where THandler : Delegate
{
    private delegate void Accessor(TTarget? target, THandler? handler);

    private static readonly ConditionalWeakTable<EventInfo, EventReference<TTarget, THandler>> cache = new();
    private static readonly ConditionalWeakTable<EventInfo, EventReference<TTarget, THandler>> downcast_cache = new();

    /// <summary>
    ///     The event's reflection metadata.
    /// </summary>
    public EventInfo Info { get; }

    /// <summary>
    ///     The handler type accepted by this reference.
    /// </summary>
    public Type HandlerType => typeof(THandler);

    /// <summary>
    ///     The actual delegate type declared by the event.
    /// </summary>
    public Type EventHandlerType { get; }

    /// <summary>
    ///     Whether checked reference downcasts are permitted for handler
    ///     parameters.
    /// </summary>
    public bool AllowsParameterDowncasts { get; }

    /// <summary>
    ///     Whether the event supports adding handlers.
    /// </summary>
    [MemberNotNullWhen(returnValue: true, "add")]
    public bool CanAdd => add is not null;

    /// <summary>
    ///     Whether the event supports removing handlers.
    /// </summary>
    [MemberNotNullWhen(returnValue: true, "remove")]
    public bool CanRemove => remove is not null;

    /// <summary>
    ///     Whether the event is static and requires no target instance.
    /// </summary>
    public bool IsStatic { get; }

    private readonly Accessor? add;
    private readonly Accessor? remove;
    private readonly Type declaringType;
    private readonly bool checkTargetType;

    private EventReference(
        EventInfo info,
        Type handlerType,
        MethodInfo? addMethod,
        MethodInfo? removeMethod,
        bool allowParameterDowncasts
    )
    {
        var method = (addMethod ?? removeMethod)!;
        var adapter = EventHandlerAdapter.Get(handlerType, allowParameterDowncasts);

        Info = info;
        EventHandlerType = handlerType;
        AllowsParameterDowncasts = allowParameterDowncasts;
        IsStatic = method.IsStatic;
        declaringType = method.DeclaringType!;
        checkTargetType = !declaringType.IsAssignableFrom(typeof(TTarget));
        add = CreateAccessor(addMethod, adapter);
        remove = CreateAccessor(removeMethod, adapter);
    }

    /// <summary>
    ///     Adds a handler, returning <see langword="false"/> only if the event
    ///     has no <see langword="add"/> accessor.
    /// </summary>
    /// <inheritdoc cref="Add" path="/param"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(TTarget? target, THandler? handler)
    {
        if (add is null)
        {
            return false;
        }

        ValidateTarget(target);
        add(target, handler);
        return true;
    }

    /// <summary>
    ///     Adds a handler to the event.
    /// </summary>
    /// <param name="target">
    ///     The event source.  Ignored for static events.
    /// </param>
    /// <param name="handler">
    ///     The handler to add.
    /// </param>
    /// <remarks>
    ///     For standard events, repeated additions create separate
    ///     subscriptions.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TTarget? target, THandler? handler)
    {
        if (!TryAdd(target, handler))
        {
            throw new InvalidOperationException($"Event '{Info.Name}' has no add accessor.");
        }
    }

    /// <summary>
    ///     Removes a handler, returning <see langword="false"/> only if the
    ///     event has no <see langword="remove"/> accessor.
    /// </summary>
    /// <inheritdoc cref="Remove" path="/param"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRemove(TTarget? target, THandler? handler)
    {
        if (remove is null)
        {
            return false;
        }

        ValidateTarget(target);
        remove(target, handler);
        return true;
    }

    /// <summary>
    ///     Removes a handler from the event.
    /// </summary>
    /// <param name="target">
    ///     The event source.  Ignored for static events.
    /// </param>
    /// <param name="handler">A delegate equal to the handler to remove.</param>
    /// <remarks>
    ///     For standard events, removes the last matching subscription; absent
    ///     handlers have no effect.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Remove(TTarget? target, THandler? handler)
    {
        if (!TryRemove(target, handler))
        {
            throw new InvalidOperationException($"Event '{Info.Name}' has no remove accessor.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateTarget(TTarget? target)
    {
        if (!IsStatic && (target is null || (checkTargetType && !declaringType.IsInstanceOfType(target))))
        {
            throw new TargetException($"Event '{Info.Name}' requires an instance of '{declaringType}'.");
        }
    }

    /// <summary>
    ///     Creates a typed reference from an event's reflection metadata.
    /// </summary>
    /// <param name="info">The event to reference.</param>
    /// <param name="allowParameterDowncasts">
    ///     Allows narrower handler parameter types.  Incompatible arguments
    ///     throw when the event is raised.
    /// </param>
    /// <remarks>
    ///     For instance events on structs, use <see langword="object"/> or an
    ///     interface as the target type and retain the boxed instance.
    /// </remarks>
    public static EventReference<TTarget, THandler> Create(EventInfo info, bool allowParameterDowncasts = false)
    {
        ArgumentNullException.ThrowIfNull(info);

        return allowParameterDowncasts
            ? downcast_cache.GetValue(info, static value => CreateCore(value, allowParameterDowncasts: true))
            : cache.GetValue(info, static value => CreateCore(value, allowParameterDowncasts: false));
    }

    private static EventReference<TTarget, THandler> CreateCore(EventInfo info, bool allowParameterDowncasts)
    {
        var handlerType = info.EventHandlerType;
        if (handlerType is null
         || handlerType.ContainsGenericParameters
         || !handlerType.IsSubclassOf(typeof(MulticastDelegate))
         || typeof(THandler).GetMethod("Invoke") is not { } handlerInvoke)
        {
            throw new ArgumentException("The event and handler must have closed, concrete delegate types.", nameof(info));
        }

        var add = info.GetAddMethod(nonPublic: true);
        var remove = info.GetRemoveMethod(nonPublic: true);
        if (add is null && remove is null)
        {
            throw new ArgumentException("The event has neither an add nor a remove accessor.", nameof(info));
        }

        ValidateAccessor(add, handlerType, nameof(info));
        ValidateAccessor(remove, handlerType, nameof(info));
        if (add is not null
         && remove is not null
         && (add.IsStatic != remove.IsStatic || add.DeclaringType != remove.DeclaringType))
        {
            throw new ArgumentException("Event accessors must agree on their declaring type and static-ness.", nameof(info));
        }

        // TODO: Can we remove this restriction later?
        if (!(add ?? remove)!.IsStatic && typeof(TTarget).IsValueType)
        {
            throw new ArgumentException("Instance events require a reference target. Use object or an interface to retain a boxed struct.", nameof(info));
        }

        if (handlerType != typeof(THandler))
        {
            if (allowParameterDowncasts)
            {
                EventHandlerAdapter.Get(handlerType, allowParameterDowncasts: true).ValidateDowncasts(typeof(THandler));
            }
            else
            {
                // The possible allocation is annoying, but CreateDelegate will
                // throw for us, so we can use it to validate variance, by-ref
                // parameters, and returns.
                _ = handlerInvoke.CreateDelegate(handlerType, target: null);
            }
        }

        return new EventReference<TTarget, THandler>(info, handlerType, add, remove, allowParameterDowncasts);
    }

    private static void ValidateAccessor(MethodInfo? method, Type handlerType, string paramName)
    {
        if (method is null)
        {
            return;
        }

        var parameters = method.GetParameters();
        if (method.DeclaringType is null
         || method.ContainsGenericParameters
         || method.ReturnType != typeof(void)
         || parameters.Length != 1
         || parameters[0].ParameterType != handlerType
         || method is { IsStatic: true, IsAbstract: true }
         || (method.CallingConvention & CallingConventions.VarArgs) != 0)
        {
            throw new ArgumentException("The event has an unsupported accessor signature.", paramName);
        }
    }

    // TODO: Test IsDynamicCodeSupported
    private static Accessor? CreateAccessor(MethodInfo? method, EventHandlerAdapter adapter)
    {
        if (method is null)
        {
            return null;
        }

        // For exact, invariant signatures, we don't need to generate anything.
        // For variant types, we should inline normalization into the call
        // itself to avoid an extra delegate dispatch.
        if (adapter.HandlerType == typeof(THandler) && (!adapter.HasVariance || !RuntimeFeature.IsDynamicCodeSupported))
        {
            if (method.IsStatic)
            {
                var invoke = method.CreateDelegate<Action<THandler?>>();
                if (!adapter.HasVariance)
                {
                    return (_, handler) => invoke(handler);
                }

                return (_, handler) => invoke((THandler?)adapter.Convert(handler));
            }

            if (!method.DeclaringType!.IsValueType && method.DeclaringType.IsAssignableFrom(typeof(TTarget)))
            {
                var invoke = method.CreateDelegate<Accessor>();
                if (!adapter.HasVariance)
                {
                    return invoke;
                }

                return (target, handler) => invoke(target, (THandler?)adapter.Convert(handler));
            }
        }

        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            // Unfortunate slow path with reflection when no JIT is available to
            // us.
            return (target, handler) => method.Invoke(
                method.IsStatic ? null : target,
                BindingFlags.DoNotWrapExceptions,
                binder: null,
                [adapter.Convert(handler)],
                culture: null
            );
        }

        var instance = Expression.Parameter(typeof(TTarget), "target");
        var handlerParameter = Expression.Parameter(typeof(THandler), "handler");
        var converted = Expression.Convert(
            Expression.Call(Expression.Constant(adapter), nameof(EventHandlerAdapter.Convert), Type.EmptyTypes, handlerParameter),
            adapter.HandlerType
        );
        var targetExpression = default(Expression);
        if (!method.IsStatic)
        {
            targetExpression = method.DeclaringType!.IsValueType
                ? Expression.Unbox(instance, method.DeclaringType)
                : Expression.Convert(instance, method.DeclaringType);
        }

        return Expression.Lambda<Accessor>(Expression.Call(targetExpression, method, converted), instance, handlerParameter).Compile();
    }
}
