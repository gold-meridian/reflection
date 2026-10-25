using System;
using System.Linq.Expressions;
using System.Reflection;

namespace GoldMeridian.Reflection;

public static class Members
{
#region GetMethod
    public static MethodInfo GetMethod(Delegate method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return method.HasSingleTarget
            ? method.Method
            : throw new ArgumentException("Select a single method, not a multicast delegate.", nameof(method));
    }

    public static MethodInfo GetMethod(Expression<Func<Delegate>> selector)
    {
        return GetMethodCore(selector);
    }

    public static MethodInfo GetMethod(Expression<Func<object, Delegate>> selector)
    {
        return GetMethodCore(selector);
    }

    public static MethodInfo GetMethod<T>(Expression<Func<T, Delegate>> selector)
    {
        return GetMethodCore(selector);
    }

    public static MethodInfo GetMethod(Expression<Action> selector)
    {
        return GetMethodCore(selector);
    }

    public static MethodInfo GetMethod(Expression<Action<object>> selector)
    {
        return GetMethodCore(selector);
    }

    public static MethodInfo GetMethod<T>(Expression<Action<T>> selector)
    {
        return GetMethodCore(selector);
    }

    public static MethodInfo GetMethod(Expression<Func<object?>> selector)
    {
        return GetMethodCore(selector);
    }

    public static MethodInfo GetMethod(Expression<Func<object, object?>> selector)
    {
        return GetMethodCore(selector);
    }

    public static MethodInfo GetMethod<T>(Expression<Func<T, object?>> selector)
    {
        return GetMethodCore(selector);
    }
#endregion

#region GetField
    public static FieldInfo GetField(Expression<Func<object?>> selector)
    {
        return GetFieldCore(selector);
    }

    public static FieldInfo GetField(Expression<Func<object, object?>> selector)
    {
        return GetFieldCore(selector);
    }

    public static FieldInfo GetField<T>(Expression<Func<T, object?>> selector)
    {
        return GetFieldCore(selector);
    }
#endregion

#region GetProperty
    public static PropertyInfo GetProperty(Expression<Func<object?>> selector)
    {
        return GetPropertyCore(selector);
    }

    public static PropertyInfo GetProperty(Expression<Func<object, object?>> selector)
    {
        return GetPropertyCore(selector);
    }

    public static PropertyInfo GetProperty<T>(Expression<Func<T, object?>> selector)
    {
        return GetPropertyCore(selector);
    }
#endregion

#region GetConstructor
    public static ConstructorInfo GetConstructor(Expression<Func<object?>> selector)
    {
        return GetConstructorCore(selector);
    }

    public static ConstructorInfo GetConstructor(Expression<Func<object, object?>> selector)
    {
        return GetConstructorCore(selector);
    }

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
