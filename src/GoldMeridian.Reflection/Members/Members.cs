using System;
using System.Linq.Expressions;
using System.Reflection;

namespace GoldMeridian.Reflection;

/// <summary>
///     Retrieves member metadata using delegates and expressions.
/// </summary>
/// <remarks>
///     Select members directly.  Expressions are not executed and do not
///     require an instance.
///     <br />
///     Virtual members may identify a base declaration; interface members
///     identify the interface declaration.
/// </remarks>
public static class Members
{
#region GetMethod
    /// <summary>
    ///     Gets the method represented by a single delegate.  Pass a method
    ///     group, using a delegate cast to select an overload.
    /// </summary>
    /// <remarks>
    ///     A lambda delegate identifies the lambda itself.  Multicast delegates
    ///     are not supported.
    /// </remarks>
    public static MethodInfo GetMethod(Delegate method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return method.HasSingleTarget
            ? method.Method
            : throw new ArgumentException("Select a single method, not a multicast delegate.", nameof(method));
    }

    /// <summary>
    ///     Gets the method selected by a method-group or method-call
    ///     expression.
    /// </summary>
    /// <param name="selector">
    ///     A selector such as <c>() =&gt; Type.Method</c>,
    ///     <c>x =&gt; x.Method</c>, or <c>x =&gt; x.Method(0)</c>.
    /// </param>
    /// <remarks>
    ///     Use a delegate cast or call arguments to select an overload.  The
    ///     expression is not executed.
    /// </remarks>
    public static MethodInfo GetMethod(Expression<Func<Delegate>> selector)
    {
        return GetMethodCore(selector);
    }

    /// <inheritdoc cref="GetMethod(Expression{Func{Delegate}})"/>
    public static MethodInfo GetMethod(Expression<Func<object, Delegate>> selector)
    {
        return GetMethodCore(selector);
    }

    /// <inheritdoc cref="GetMethod(Expression{Func{Delegate}})"/>
    public static MethodInfo GetMethod<T>(Expression<Func<T, Delegate>> selector)
    {
        return GetMethodCore(selector);
    }

    /// <inheritdoc cref="GetMethod(Expression{Func{Delegate}})"/>
    public static MethodInfo GetMethod(Expression<Action> selector)
    {
        return GetMethodCore(selector);
    }

    /// <inheritdoc cref="GetMethod(Expression{Func{Delegate}})"/>
    public static MethodInfo GetMethod(Expression<Action<object>> selector)
    {
        return GetMethodCore(selector);
    }

    /// <inheritdoc cref="GetMethod(Expression{Func{Delegate}})"/>
    public static MethodInfo GetMethod<T>(Expression<Action<T>> selector)
    {
        return GetMethodCore(selector);
    }

    /// <inheritdoc cref="GetMethod(Expression{Func{Delegate}})"/>
    public static MethodInfo GetMethod(Expression<Func<object?>> selector)
    {
        return GetMethodCore(selector);
    }

    /// <inheritdoc cref="GetMethod(Expression{Func{Delegate}})"/>
    public static MethodInfo GetMethod(Expression<Func<object, object?>> selector)
    {
        return GetMethodCore(selector);
    }

    /// <inheritdoc cref="GetMethod(Expression{Func{Delegate}})"/>
    public static MethodInfo GetMethod<T>(Expression<Func<T, object?>> selector)
    {
        return GetMethodCore(selector);
    }
#endregion

#region GetField
    /// <summary>
    ///     Gets the field selected by an expression without reading its value.
    ///     Constants are not supported.
    /// </summary>
    /// <param name="selector">
    ///     A field access such as <c>() =&gt; Type.Field</c> or
    ///     <c>x =&gt; x.Field</c>.
    /// </param>
    public static FieldInfo GetField(Expression<Func<object?>> selector)
    {
        return GetFieldCore(selector);
    }

    /// <inheritdoc cref="GetField(Expression{Func{object}})"/>
    public static FieldInfo GetField(Expression<Func<object, object?>> selector)
    {
        return GetFieldCore(selector);
    }

    /// <inheritdoc cref="GetField(Expression{Func{object}})"/>
    public static FieldInfo GetField<T>(Expression<Func<T, object?>> selector)
    {
        return GetFieldCore(selector);
    }
#endregion

#region GetProperty
    /// <summary>
    ///     Gets the property or indexer selected by an expression without
    ///     invoking its getter.
    /// </summary>
    /// <param name="selector">
    ///     A property access such as <c>() =&gt; Type.Property</c>,
    ///     <c>x =&gt; x.Property</c>, or <c>x =&gt; x[0]</c>.
    /// </param>
    public static PropertyInfo GetProperty(Expression<Func<object?>> selector)
    {
        return GetPropertyCore(selector);
    }

    /// <inheritdoc cref="GetProperty(Expression{Func{object}})"/>
    public static PropertyInfo GetProperty(Expression<Func<object, object?>> selector)
    {
        return GetPropertyCore(selector);
    }

    /// <inheritdoc cref="GetProperty(Expression{Func{object}})"/>
    public static PropertyInfo GetProperty<T>(Expression<Func<T, object?>> selector)
    {
        return GetPropertyCore(selector);
    }
#endregion

#region GetConstructor
    /// <summary>
    ///     Gets the constructor selected by a <c>new</c> expression without
    ///     creating an instance.
    /// </summary>
    /// <param name="selector">
    ///     A constructor call such as <c>() =&gt; new Type(0)</c>, without an
    ///     object or collection initializer.
    /// </param>
    /// <remarks>
    ///     Implicit parameterless struct constructors are not supported.
    /// </remarks>
    public static ConstructorInfo GetConstructor(Expression<Func<object?>> selector)
    {
        return GetConstructorCore(selector);
    }

    /// <inheritdoc cref="GetConstructor(Expression{Func{object}})"/>
    public static ConstructorInfo GetConstructor(Expression<Func<object, object?>> selector)
    {
        return GetConstructorCore(selector);
    }

    /// <inheritdoc cref="GetConstructor(Expression{Func{object}})"/>
    public static ConstructorInfo GetConstructor<T>(Expression<Func<T, object?>> selector)
    {
        return GetConstructorCore(selector);
    }
#endregion

    private static readonly MethodInfo create_delegate = typeof(MethodInfo).GetMethod(nameof(MethodInfo.CreateDelegate), [typeof(Type), typeof(object)])!;

    private static MethodInfo GetMethodCore(LambdaExpression selector)
    {
        if (GetBody(selector) is MethodCallExpression call)
        {
            // Method-group conversion is handled as:
            // (TDelegate)((MethodInfo)constant).CreateDelegate(typeof(TDelegate), receiver)
            // We need to extract the constant method.
            if (call.Method == create_delegate
             && call.Object is ConstantExpression { Value: MethodInfo method }
             && call.Arguments[0] is ConstantExpression { Value: Type delegateType }
             && delegateType.IsSubclassOf(typeof(MulticastDelegate)))
            {
                return method;
            }

            return call.Method;
        }

        throw InvalidSelector("method group or method call", nameof(selector));
    }

    private static FieldInfo GetFieldCore(LambdaExpression selector)
    {
        return GetBody(selector) is MemberExpression { Member: FieldInfo field }
            ? field
            : throw InvalidSelector("field access", nameof(selector));
    }

    private static PropertyInfo GetPropertyCore(LambdaExpression selector)
    {
        var body = GetBody(selector);
        if (body is MemberExpression { Member: PropertyInfo property })
        {
            return property;
        }

        if (body is IndexExpression { Indexer: { } indexer })
        {
            return indexer;
        }

        // Compiler-generated indexer selectors may directly call the getter
        // function rather than an IndexExpression.
        if (body is MethodCallExpression { Method: { IsSpecialName: true, DeclaringType: { } declaringType } getter })
        {
            foreach (var candidate in declaringType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (candidate.GetMethod == getter)
                {
                    return candidate;
                }
            }
        }

        throw InvalidSelector("property or indexer access", nameof(selector));
    }

    private static ConstructorInfo GetConstructorCore(LambdaExpression selector)
    {
        return GetBody(selector) is NewExpression { Constructor: { } constructor }
            ? constructor
            : throw InvalidSelector("constructor call (new Type(...)) with constructor metadata", nameof(selector));
    }

    private static Expression GetBody(LambdaExpression selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        var body = selector.Body;
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked or ExpressionType.TypeAs, Method: null } conversion)
        {
            body = conversion.Operand;
        }

        return body;
    }

    private static ArgumentException InvalidSelector(string expected, string paramName)
    {
        return new ArgumentException($"The selector must directly select a {expected}.", paramName);
    }
}
