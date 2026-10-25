using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace GoldMeridian.Reflection;

internal sealed class ParameterDowncastCache
{
    public sealed record Binding(Func<Delegate, Delegate>? Factory);

    private readonly ConditionalWeakTable<Type, Binding> bindings = new();
    private readonly ConditionalWeakTable<Type, WeakReference<Binding>>? collectibleBindings;
    private readonly ConditionalWeakTable<Type, Binding>.CreateValueCallback createBinding;
    private readonly Type handlerType;

    public ParameterDowncastCache(Type handlerType)
    {
        this.handlerType = handlerType;
        if (handlerType.IsCollectible)
        {
            collectibleBindings = new ConditionalWeakTable<Type, WeakReference<Binding>>();
        }

        createBinding = CreateBinding;
    }

    public Binding Get(Type sourceType)
    {
        if (collectibleBindings is null)
        {
            return bindings.GetValue(sourceType, createBinding);
        }

        // We can't keep strong references to collectible handlers, so we bind
        // weak references.
        var weak = collectibleBindings.GetValue(sourceType, static _ => new WeakReference<Binding>(null!));
        if (!weak.TryGetTarget(out var binding))
        {
            binding = CreateBinding(sourceType);
            weak.SetTarget(binding);
        }

        return binding;
    }

    private Binding CreateBinding(Type sourceType)
    {
        var sourceInvoke = sourceType.GetMethod("Invoke")!;

        try
        {
            _ = sourceInvoke.CreateDelegate(handlerType, target: null);
            return new Binding(null);
        }
        catch (ArgumentException) { }

        var eventInvoke = handlerType.GetMethod("Invoke")!;
        var eventParameters = eventInvoke.GetParameters();
        var sourceParameters = sourceInvoke.GetParameters();
        if (eventParameters.Length != sourceParameters.Length)
        {
            throw IncompatibleSignature(sourceType);
        }

        var hasDowncast = false;
        var parameters = new ParameterExpression[eventParameters.Length];
        var arguments = new Expression[eventParameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var eventType = eventParameters[i].ParameterType;
            var sourceParameterType = sourceParameters[i].ParameterType;
            parameters[i] = Expression.Parameter(eventType, eventParameters[i].Name);
            arguments[i] = parameters[i];
            if (eventType == sourceParameterType)
            {
                continue;
            }

            if (!IsReferenceType(eventType) || !IsReferenceType(sourceParameterType))
            {
                throw IncompatibleSignature(sourceType);
            }

            if (!sourceParameterType.IsAssignableFrom(eventType))
            {
                if (!eventType.IsAssignableFrom(sourceParameterType))
                {
                    throw IncompatibleSignature(sourceType);
                }

                hasDowncast = true;
            }

            arguments[i] = Expression.Convert(parameters[i], sourceParameterType);
        }

        var eventReturn = eventInvoke.ReturnType;
        var sourceReturn = sourceInvoke.ReturnType;
        if (!hasDowncast
         || (eventReturn != sourceReturn && (!IsReferenceType(eventReturn) || !IsReferenceType(sourceReturn) || !eventReturn.IsAssignableFrom(sourceReturn))))
        {
            throw IncompatibleSignature(sourceType);
        }

        var source = Expression.Parameter(typeof(Delegate), "handler");
        Expression body = Expression.Invoke(Expression.Convert(source, sourceType), arguments);
        if (eventReturn != sourceReturn)
        {
            body = Expression.Convert(body, eventReturn);
        }

        var adapted = Expression.Lambda(handlerType, body, parameters);
        var factory = Expression.Lambda<Func<Delegate, Delegate>>(
            Expression.Convert(adapted, typeof(Delegate)),
            source
        ).Compile(preferInterpretation: !RuntimeFeature.IsDynamicCodeSupported);
        return new Binding(factory);
    }

    private ArgumentException IncompatibleSignature(Type sourceType)
    {
        return new ArgumentException($"Handler '{sourceType}' cannot handle event '{handlerType}', even with reference-parameter downcasts.");
    }

    private static bool IsReferenceType(Type type)
    {
        return type is { IsValueType: false, IsByRef: false, IsPointer: false, IsFunctionPointer: false } && type != typeof(void);
    }
}
