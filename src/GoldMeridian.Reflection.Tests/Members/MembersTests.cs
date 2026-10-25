using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

// ReSharper disable RedundantTypeArgumentsOfMethod
// ReSharper disable RedundantCast

namespace GoldMeridian.Reflection.Tests;

[TestFixture]
public static class MembersTests
{
    private interface ISubject
    {
        int Property { get; }

        int this[int _] { get; }

        void Method();
    }

    private class BaseSubject
    {
        public int Field = 0;

        public virtual int Property => throw new InvalidOperationException("Getter executed.");

        public virtual void Method()
        {
            throw new InvalidOperationException("Method executed.");
        }

        public void Inherited()
        {
            throw new InvalidOperationException("Method executed.");
        }
    }

    private sealed class Subject : BaseSubject, ISubject
    {
        public new string Field = "field";
        public readonly int ReadonlyField = 0;
        public static int StaticField;
        public static readonly string STATIC_READONLY_FIELD = "field";
        public const int CONSTANT = 1;

        public override int Property => throw new InvalidOperationException("Getter executed.");

        public static int StaticProperty => throw new InvalidOperationException("Getter executed.");

        public int this[int _] => throw new InvalidOperationException("Indexer executed.");

        public string this[string _] => throw new InvalidOperationException("Indexer executed.");

        public Subject Child => throw new InvalidOperationException("Receiver evaluated.");

        public Subject() { }

        public Subject(int _) => throw new InvalidOperationException("Constructor executed.");

        public Subject(string _) => throw new InvalidOperationException("Constructor executed.");

        public override void Method()
        {
            throw new InvalidOperationException("Method executed.");
        }

        public int Result()
        {
            throw new InvalidOperationException("Method executed.");
        }

        public int Overload(int value)
        {
            throw new InvalidOperationException("Method executed.");
        }

        public string Overload(string value)
        {
            throw new InvalidOperationException("Method executed.");
        }

        public T Generic<T>(T value)
        {
            throw new InvalidOperationException("Method executed.");
        }

        public static void StaticMethod()
        {
            throw new InvalidOperationException("Method executed.");
        }

        public static int StaticResult()
        {
            throw new InvalidOperationException("Method executed.");
        }

        public static Action DelegateResult()
        {
            throw new InvalidOperationException("Method executed.");
        }

        public static ref int RefReturn()
        {
            return ref StaticField;
        }

        public static void RefArgument(ref int value)
        {
            throw new InvalidOperationException("Method executed.");
        }

        public static void SpanArgument(Span<int> value)
        {
            throw new InvalidOperationException("Method executed.");
        }
    }

    private sealed class GenericSubject<T>
    {
        public T Property => throw new InvalidOperationException("Getter executed.");

        public T this[int _] => throw new InvalidOperationException("Indexer executed.");

        public T? Field = default;

        public GenericSubject(T _)
        {
            throw new InvalidOperationException("Constructor executed.");
        }

        public T Method(T _)
        {
            throw new InvalidOperationException("Method executed.");
        }
    }

    private struct StructSubject
    {
        public int Property => throw new InvalidOperationException("Getter executed.");

        public int Field = 0;

        public StructSubject(int _)
        {
            throw new InvalidOperationException("Constructor executed.");
        }

        public void Method()
        {
            throw new InvalidOperationException("Method executed.");
        }
    }

    private struct ExplicitDefaultStruct
    {
        public ExplicitDefaultStruct()
        {
            throw new InvalidOperationException("Constructor executed.");
        }
    }

    private sealed class ExplicitSubject : ISubject
    {
        int ISubject.Property => throw new InvalidOperationException("Getter executed.");

        int ISubject.this[int _] => throw new InvalidOperationException("Indexer executed.");

        void ISubject.Method()
        {
            throw new InvalidOperationException("Method executed.");
        }
    }

    private sealed class Conversion
    {
        public static implicit operator int(Conversion _)
        {
            throw new InvalidOperationException("Conversion executed.");
        }
    }

    private sealed class StaticInitializationSubject
    {
        static StaticInitializationSubject()
        {
            throw new InvalidOperationException("Type initialized.");
        }

        public static int Property => throw new InvalidOperationException("Getter executed.");

        public static int Field = 0;

        public static void Method()
        {
            throw new InvalidOperationException("Method executed.");
        }
    }

    private static int PrivateProperty => throw new InvalidOperationException("Getter executed.");

    private static readonly Conversion conversion = new();

    private static void PrivateMethod()
    {
        throw new InvalidOperationException("Method executed.");
    }

    private static Subject GetReceiver()
    {
        throw new InvalidOperationException("Receiver evaluated.");
    }

    private static int GetArgument()
    {
        throw new InvalidOperationException("Argument evaluated.");
    }

    [Test]
    public static void MethodGroups_SupportEverySelectorForm_WithoutInvokingMethods()
    {
        var expected = typeof(Subject).GetMethod(nameof(Subject.StaticMethod));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetMethod(Subject.StaticMethod), Is.EqualTo(expected));
            Assert.That(Members.GetMethod(() => Subject.StaticMethod), Is.EqualTo(expected));
            Assert.That(Members.GetMethod(_ => Subject.StaticMethod), Is.EqualTo(expected));
            Assert.That(Members.GetMethod<Subject>(_ => Subject.StaticMethod), Is.EqualTo(expected));
            Assert.That(Members.GetMethod<Subject>(x => x.Method), Is.EqualTo(typeof(BaseSubject).GetMethod(nameof(BaseSubject.Method))));
            Assert.That(Members.GetMethod((Subject x) => x.Method), Is.EqualTo(typeof(BaseSubject).GetMethod(nameof(BaseSubject.Method))));
        }
    }

    [Test]
    public static void MethodCalls_SupportEverySelectorForm_WithoutInvokingMethods()
    {
        var voidMethod = typeof(Subject).GetMethod(nameof(Subject.StaticMethod));
        var valueMethod = typeof(Subject).GetMethod(nameof(Subject.StaticResult));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetMethod(() => Subject.StaticMethod()), Is.EqualTo(voidMethod));
            Assert.That(Members.GetMethod(_ => Subject.StaticMethod()), Is.EqualTo(voidMethod));
            Assert.That(Members.GetMethod<Subject>(_ => Subject.StaticMethod()), Is.EqualTo(voidMethod));
            Assert.That(Members.GetMethod(() => Subject.StaticResult()), Is.EqualTo(valueMethod));
            Assert.That(Members.GetMethod(_ => Subject.StaticResult()), Is.EqualTo(valueMethod));
            Assert.That(Members.GetMethod<Subject>(_ => Subject.StaticResult()), Is.EqualTo(valueMethod));
            Assert.That(Members.GetMethod<Subject>(x => x.Method()), Is.EqualTo(typeof(BaseSubject).GetMethod(nameof(BaseSubject.Method))));
            Assert.That(Members.GetMethod<Subject>(x => x.Result()), Is.EqualTo(typeof(Subject).GetMethod(nameof(Subject.Result))));
            Assert.That(Members.GetMethod(() => Subject.DelegateResult()), Is.EqualTo(typeof(Subject).GetMethod(nameof(Subject.DelegateResult))));
        }
    }

    [Test]
    public static void DirectDelegates_SupportBoundOpenAndLambdaDelegates()
    {
        var subject = new Subject();
        var expected = typeof(Subject).GetMethod(nameof(Subject.Method))!;
        var open = expected.CreateDelegate<Action<Subject>>();
        Action lambda = () => throw new InvalidOperationException("Delegate invoked");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetMethod(subject.Method), Is.EqualTo(expected));
            Assert.That(Members.GetMethod(open), Is.EqualTo(expected));
            Assert.That(Members.GetMethod(lambda), Is.EqualTo(lambda.Method));
        }
    }

    [Test]
    public static void DirectDelegates_RejectMulticastRatherThanReturningOnlyTheLastMethod()
    {
        var first = Subject.StaticMethod;
        var second = PrivateMethod;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Assert.Throws<ArgumentException>(() => Members.GetMethod(first + second))!.ParamName, Is.EqualTo("method"));
            Assert.Throws<ArgumentException>(() => Members.GetMethod(first + first));
        }
    }

    [Test]
    public static void Methods_SelectOverloadsByDelegateCastOrCallArguments()
    {
        var intOverload = typeof(Subject).GetMethod(nameof(Subject.Overload), [typeof(int)]);
        var stringOverload = typeof(Subject).GetMethod(nameof(Subject.Overload), [typeof(string)]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetMethod<Subject>(x => (Func<int, int>)x.Overload), Is.EqualTo(intOverload));
            Assert.That(Members.GetMethod<Subject>(x => (Func<string, string>)x.Overload), Is.EqualTo(stringOverload));
            Assert.That(Members.GetMethod<Subject>(x => x.Overload(0)), Is.EqualTo(intOverload));
            Assert.That(Members.GetMethod<Subject>(x => x.Overload("")), Is.EqualTo(stringOverload));
            Assert.That(Members.GetMethod((Func<int, int>)new Subject().Overload), Is.EqualTo(intOverload));
        }
    }

    [Test]
    public static void Methods_PreserveConstructedGenericArguments()
    {
        var expected = typeof(Subject).GetMethod(nameof(Subject.Generic))!.MakeGenericMethod(typeof(string));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetMethod<Subject>(x => x.Generic<string>), Is.EqualTo(expected));
            Assert.That(Members.GetMethod<Subject>(x => x.Generic("")), Is.EqualTo(expected));
            Assert.That(Members.GetMethod(new Subject().Generic<string>), Is.EqualTo(expected));
        }
    }

    [Test]
    public static void MethodGroups_SupportSignaturesThatCannotBeBoxed()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetMethod(() => Subject.RefReturn), Is.EqualTo(typeof(Subject).GetMethod(nameof(Subject.RefReturn))));
            Assert.That(Members.GetMethod(() => Subject.RefArgument), Is.EqualTo(typeof(Subject).GetMethod(nameof(Subject.RefArgument))));
            Assert.That(Members.GetMethod(() => Subject.SpanArgument), Is.EqualTo(typeof(Subject).GetMethod(nameof(Subject.SpanArgument))));
            Assert.That(Members.GetMethod(Subject.RefReturn), Is.EqualTo(typeof(Subject).GetMethod(nameof(Subject.RefReturn))));
        }
    }

    [Test]
    public static void ExtensionMethods_ReturnTheUnderlyingStaticMethod()
    {
        var expected = typeof(MemberSelectorTestExtensions).GetMethod(nameof(MemberSelectorTestExtensions.Repeat));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetMethod<string>(x => x.Repeat), Is.EqualTo(expected));
            Assert.That(Members.GetMethod<string>(x => x.Repeat(GetArgument())), Is.EqualTo(expected));
            Assert.That(Members.GetMethod("".Repeat), Is.EqualTo(expected));
        }
    }

    [Test]
    public static void ExplicitDelegateConstruction_SelectsTheReferencedMethod()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetMethod(() => new Action(Subject.StaticMethod)), Is.EqualTo(typeof(Subject).GetMethod("StaticMethod")));
            Assert.That(Members.GetMethod<Subject>(x => new Func<int>(x.Result)), Is.EqualTo(typeof(Subject).GetMethod("Result")));
        }
    }

    [Test]
    public static void Fields_SupportStaticInstanceReadonlyAndBoxedValues()
    {
        var expected = typeof(Subject).GetField(nameof(Subject.StaticField));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetField(() => Subject.StaticField), Is.EqualTo(expected));
            Assert.That(Members.GetField(_ => Subject.StaticField), Is.EqualTo(expected));
            Assert.That(Members.GetField<Subject>(_ => Subject.StaticField), Is.EqualTo(expected));
            Assert.That(Members.GetField<Subject>(x => x.Field), Is.EqualTo(typeof(Subject).GetField(nameof(Subject.Field))));
            Assert.That(Members.GetField((Subject x) => x.ReadonlyField), Is.EqualTo(typeof(Subject).GetField(nameof(Subject.ReadonlyField))));
            Assert.That(Members.GetField(() => Subject.STATIC_READONLY_FIELD), Is.EqualTo(typeof(Subject).GetField(nameof(Subject.STATIC_READONLY_FIELD))));
        }
    }

    [Test]
    public static void Properties_SupportEverySelectorForm_WithoutInvokingGetters()
    {
        var expected = typeof(Subject).GetProperty(nameof(Subject.StaticProperty));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetProperty(() => Subject.StaticProperty), Is.EqualTo(expected));
            Assert.That(Members.GetProperty(_ => Subject.StaticProperty), Is.EqualTo(expected));
            Assert.That(Members.GetProperty<Subject>(_ => Subject.StaticProperty), Is.EqualTo(expected));
            Assert.That(Members.GetProperty<Subject>(x => x.Property), Is.EqualTo(typeof(BaseSubject).GetProperty(nameof(BaseSubject.Property))));
            Assert.That(Members.GetProperty((Subject x) => x.Property), Is.EqualTo(typeof(BaseSubject).GetProperty(nameof(BaseSubject.Property))));
        }
    }

    [Test]
    public static void Indexers_SelectOverloadsWithoutEvaluatingArgumentsOrGetters()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetProperty<Subject>(x => x[GetArgument()]), Is.EqualTo(typeof(Subject).GetProperty("Item", [typeof(int)])));
            Assert.That(Members.GetProperty<Subject>(x => x[""]), Is.EqualTo(typeof(Subject).GetProperty("Item", [typeof(string)])));
            Assert.That(Members.GetProperty(() => GetReceiver()[0]), Is.EqualTo(typeof(Subject).GetProperty("Item", [typeof(int)])));
        }
    }

    [Test]
    public static void Indexers_SupportManuallyConstructedIndexExpressions()
    {
        var parameter = Expression.Parameter(typeof(Subject));
        var expected = typeof(Subject).GetProperty("Item", [typeof(int)])!;
        var index = Expression.MakeIndex(parameter, expected, [Expression.Constant(0)]);
        var selector = Expression.Lambda<Func<Subject, object?>>(Expression.Convert(index, typeof(object)), parameter);

        Assert.That(Members.GetProperty(selector), Is.EqualTo(expected));
    }

    [Test]
    public static void Properties_RejectSpecialNameMethodsThatAreNotGetters()
    {
        var parameter = Expression.Parameter(typeof(DateTime));
        var duration = Expression.Constant(TimeSpan.Zero);
        var addition = Expression.Add(parameter, duration);
        var call = Expression.Call(addition.Method!, parameter, duration);
        var selector = Expression.Lambda<Func<DateTime, object?>>(Expression.Convert(call, typeof(object)), parameter);

        Assert.That(Assert.Throws<ArgumentException>(() => Members.GetProperty(selector))!.ParamName, Is.EqualTo("selector"));
    }

    [Test]
    public static void MethodSelectors_RejectCapturedDelegatesRatherThanEvaluatingThem()
    {
        var handler = Subject.StaticMethod;

        Assert.That(Assert.Throws<ArgumentException>(() => Members.GetMethod(() => handler))!.ParamName, Is.EqualTo("selector"));
    }

    [Test]
    public static void Constructors_SelectOverloadsWithoutEvaluatingArgumentsOrConstructingInstances()
    {
        var expected = typeof(Subject).GetConstructor([typeof(int)]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetConstructor(() => new Subject(GetArgument())), Is.EqualTo(expected));
            Assert.That(Members.GetConstructor(_ => new Subject(GetArgument())), Is.EqualTo(expected));
            Assert.That(Members.GetConstructor<int>(x => new Subject(x)), Is.EqualTo(expected));
            Assert.That(Members.GetConstructor(() => new Subject("")), Is.EqualTo(typeof(Subject).GetConstructor([typeof(string)])));
            Assert.That(Members.GetConstructor(() => new Subject()), Is.EqualTo(typeof(Subject).GetConstructor(Type.EmptyTypes)));
        }
    }

    [Test]
    public static void Members_OnConstructedGenericTypes_RetainTheClosedDeclaringType()
    {
        var type = typeof(GenericSubject<string>);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetField<GenericSubject<string>>(x => x.Field), Is.EqualTo(type.GetField("Field")));
            Assert.That(Members.GetProperty<GenericSubject<string>>(x => x.Property), Is.EqualTo(type.GetProperty("Property")));
            Assert.That(Members.GetProperty<GenericSubject<string>>(x => x[0]), Is.EqualTo(type.GetProperty("Item")));
            Assert.That(Members.GetMethod<GenericSubject<string>>(x => x.Method), Is.EqualTo(type.GetMethod("Method")));
            Assert.That(Members.GetConstructor(() => new GenericSubject<string>("")), Is.EqualTo(type.GetConstructor([typeof(string)])));
        }
    }

    [Test]
    public static void StructMembers_DoNotRequireAnInstance()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetField<StructSubject>(x => x.Field), Is.EqualTo(typeof(StructSubject).GetField("Field")));
            Assert.That(Members.GetProperty<StructSubject>(x => x.Property), Is.EqualTo(typeof(StructSubject).GetProperty("Property")));
            Assert.That(Members.GetMethod<StructSubject>(x => x.Method), Is.EqualTo(typeof(StructSubject).GetMethod("Method")));
            Assert.That(Members.GetConstructor(() => new StructSubject(0)), Is.EqualTo(typeof(StructSubject).GetConstructor([typeof(int)])));
            Assert.That(Members.GetConstructor(() => new ExplicitDefaultStruct()), Is.EqualTo(typeof(ExplicitDefaultStruct).GetConstructor(Type.EmptyTypes)));
        }
    }

    [Test]
    public static void InheritedHiddenAndOverriddenMembers_PreserveCompilerMetadata()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetField<Subject>(x => ((BaseSubject)x).Field), Is.EqualTo(typeof(BaseSubject).GetField("Field")));
            Assert.That(Members.GetField<Subject>(x => x.Field).DeclaringType, Is.EqualTo(typeof(Subject)));
            Assert.That(Members.GetProperty<BaseSubject>(x => x.Property).DeclaringType, Is.EqualTo(typeof(BaseSubject)));
            // The compiler emits the base virtual slot even for a derived
            // receiver.
            Assert.That(Members.GetProperty<Subject>(x => x.Property).DeclaringType, Is.EqualTo(typeof(BaseSubject)));
            Assert.That(Members.GetMethod<Subject>(x => x.Inherited).DeclaringType, Is.EqualTo(typeof(BaseSubject)));
            Assert.That(Members.GetMethod<Subject>(x => ((BaseSubject)x).Method).DeclaringType, Is.EqualTo(typeof(BaseSubject)));
            Assert.That(Members.GetMethod<Subject>(x => x.Method).DeclaringType, Is.EqualTo(typeof(BaseSubject)));
            Assert.That(Members.GetMethod(new Subject().Method).DeclaringType, Is.EqualTo(typeof(Subject)));
        }
    }

    [Test]
    public static void InterfaceSelectors_ReturnInterfaceMetadataIncludingForExplicitImplementations()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetMethod<ISubject>(x => x.Method), Is.EqualTo(typeof(ISubject).GetMethod("Method")));
            Assert.That(Members.GetMethod<ExplicitSubject>(x => ((ISubject)x).Method), Is.EqualTo(typeof(ISubject).GetMethod("Method")));
            Assert.That(Members.GetProperty<ExplicitSubject>(x => ((ISubject)x).Property), Is.EqualTo(typeof(ISubject).GetProperty("Property")));
            Assert.That(Members.GetProperty<ExplicitSubject>(x => ((ISubject)x)[0]), Is.EqualTo(typeof(ISubject).GetProperty("Item")));
        }
    }

    [Test]
    public static void AccessibleNonPublicMembers_CanBeSelected()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetMethod(() => PrivateMethod), Is.EqualTo(typeof(MembersTests).GetMethod(nameof(PrivateMethod), flags)));
            Assert.That(Members.GetField(() => conversion), Is.EqualTo(typeof(MembersTests).GetField(nameof(conversion), flags)));
            Assert.That(Members.GetProperty(() => PrivateProperty), Is.EqualTo(typeof(MembersTests).GetProperty(nameof(PrivateProperty), flags)));
        }
    }

    [Test]
    public static void Selectors_DoNotEvaluateReceivers_AndSelectTheOutermostMember()
    {
        var captured = default(Subject)!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetMethod(() => captured.Method), Is.EqualTo(typeof(BaseSubject).GetMethod("Method")));
            Assert.That(Members.GetField(() => captured.Field), Is.EqualTo(typeof(Subject).GetField("Field")));
            Assert.That(Members.GetProperty(() => captured.Property), Is.EqualTo(typeof(BaseSubject).GetProperty("Property")));
            Assert.That(Members.GetMethod(() => GetReceiver().Method), Is.EqualTo(typeof(BaseSubject).GetMethod("Method")));
            Assert.That(Members.GetMethod<Subject>(x => x.Child.Overload(GetArgument())), Is.EqualTo(typeof(Subject).GetMethod("Overload", [typeof(int)])));
            Assert.That(Members.GetField<Subject>(x => x.Child.Field), Is.EqualTo(typeof(Subject).GetField("Field")));
            Assert.That(Members.GetProperty<Subject>(x => x.Child.Property), Is.EqualTo(typeof(BaseSubject).GetProperty("Property")));
        }
    }

    [Test]
    public static void BuiltInOuterConversions_AreUnwrapped()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetField<Subject>(x => checked((byte)x.ReadonlyField)), Is.EqualTo(typeof(Subject).GetField("ReadonlyField")));
            Assert.That(Members.GetField<Subject>(x => (long)x.ReadonlyField), Is.EqualTo(typeof(Subject).GetField("ReadonlyField")));
            Assert.That(Members.GetField<Subject>(x => x.Field as object), Is.EqualTo(typeof(Subject).GetField("Field")));
            Assert.That(Members.GetProperty<Subject>(x => (int?)x.Property), Is.EqualTo(typeof(BaseSubject).GetProperty("Property")));
            Assert.That(Members.GetMethod<Subject>(x => (long)x.Result()), Is.EqualTo(typeof(Subject).GetMethod("Result")));
        }
    }

    [Test]
    public static void Selectors_DoNotTriggerStaticInitialization()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetField(() => StaticInitializationSubject.Field), Is.EqualTo(typeof(StaticInitializationSubject).GetField("Field")));
            Assert.That(Members.GetProperty(() => StaticInitializationSubject.Property), Is.EqualTo(typeof(StaticInitializationSubject).GetProperty("Property")));
            Assert.That(Members.GetMethod(() => StaticInitializationSubject.Method), Is.EqualTo(typeof(StaticInitializationSubject).GetMethod("Method")));
            Assert.That(Members.GetConstructor(() => new StaticInitializationSubject()), Is.EqualTo(typeof(StaticInitializationSubject).GetConstructor(Type.EmptyTypes)));
        }
    }

    public static IEnumerable<TestCaseData> InvalidSelectors()
    {
        yield return Invalid("Field_Property", () => Members.GetField<Subject>(x => x.Property));
        yield return Invalid("Field_Constant", () => Members.GetField(() => Subject.CONSTANT));
        yield return Invalid("Field_Literal", () => Members.GetField(() => 1));
        yield return Invalid("Field_Arithmetic", () => Members.GetField<Subject>(x => x.ReadonlyField + 1));
        yield return Invalid("Field_Method", () => Members.GetField<Subject>(x => x.Result()));
        yield return Invalid("Field_UserConversion", () => Members.GetField(() => (int)conversion));
        yield return Invalid("Property_Field", () => Members.GetProperty<Subject>(x => x.Field));
        yield return Invalid("Property_Method", () => Members.GetProperty<Subject>(x => x.Result()));
        yield return Invalid("Property_ArrayIndex", () => Members.GetProperty<int[]>(x => x[0]));
        yield return Invalid("Property_ArrayLength", () => Members.GetProperty<int[]>(x => x.Length));
        yield return Invalid("Property_Conditional", () => Members.GetProperty<Subject>(x => x == null ? 0 : x.Property));
        yield return Invalid("Method_Field", () => Members.GetMethod<Subject>(x => x.Field));
        yield return Invalid("Method_Property", () => Members.GetMethod<Subject>(x => x.Property));
        yield return Invalid("Method_Arithmetic", () => Members.GetMethod<Subject>(x => x.Result() + 1));
        yield return Invalid("Method_UserConversion", () => Members.GetMethod(() => (int)conversion));
        yield return Invalid("Method_New", () => Members.GetMethod(() => new Subject()));
        yield return Invalid("Method_DelegateInvocation", () => Members.GetMethod<Action>(x => x()));
        yield return Invalid("Method_DelegateParameter", () => Members.GetMethod<Action>(x => x));
        yield return Invalid("Method_Lambda", () => Members.GetMethod(() => (Action)(() => Subject.StaticMethod())));
        yield return Invalid("Constructor_Method", () => Members.GetConstructor(() => GetReceiver()));
        yield return Invalid("Constructor_Default", () => Members.GetConstructor(() => default(Subject)));
        yield return Invalid("Constructor_ImplicitStructDefault", () => Members.GetConstructor(() => new StructSubject()));
        yield return Invalid("Constructor_Array", () => Members.GetConstructor(() => new int[1]));
        yield return Invalid("Constructor_Initializer", () => Members.GetConstructor(() => new Subject { Field = "" }));

        yield break;

        static TestCaseData Invalid(string name, TestDelegate action)
        {
            return new TestCaseData(action).SetName("InvalidSelector_" + name);
        }
    }

    [TestCaseSource(nameof(InvalidSelectors))]
    public static void InvalidSelectors_ThrowArgumentException(TestDelegate action)
    {
        Assert.That(Assert.Throws<ArgumentException>(action)!.ParamName, Is.EqualTo("selector"));
    }

    public static IEnumerable<TestCaseData> NullSelectors()
    {
        yield return Null("Method_Delegate", () => Members.GetMethod((Delegate)null!), "method");
        yield return Null("Method_Group", () => Members.GetMethod((Expression<Func<Delegate>>)null!));
        yield return Null("Method_GroupUnusedParameter", () => Members.GetMethod((Expression<Func<object, Delegate>>)null!));
        yield return Null("Method_GroupInstance", () => Members.GetMethod<Subject>((Expression<Func<Subject, Delegate>>)null!));
        yield return Null("Method_Void", () => Members.GetMethod((Expression<Action>)null!));
        yield return Null("Method_VoidUnusedParameter", () => Members.GetMethod((Expression<Action<object>>)null!));
        yield return Null("Method_VoidInstance", () => Members.GetMethod<Subject>((Expression<Action<Subject>>)null!));
        yield return Null("Method_Value", () => Members.GetMethod((Expression<Func<object?>>)null!));
        yield return Null("Method_ValueUnusedParameter", () => Members.GetMethod((Expression<Func<object, object?>>)null!));
        yield return Null("Method_ValueInstance", () => Members.GetMethod<Subject>((Expression<Func<Subject, object?>>)null!));
        yield return Null("Field_Static", () => Members.GetField((Expression<Func<object?>>)null!));
        yield return Null("Field_UnusedParameter", () => Members.GetField((Expression<Func<object, object?>>)null!));
        yield return Null("Field_Instance", () => Members.GetField<Subject>(null!));
        yield return Null("Property_Static", () => Members.GetProperty((Expression<Func<object?>>)null!));
        yield return Null("Property_UnusedParameter", () => Members.GetProperty((Expression<Func<object, object?>>)null!));
        yield return Null("Property_Instance", () => Members.GetProperty<Subject>(null!));
        yield return Null("Constructor_Static", () => Members.GetConstructor((Expression<Func<object?>>)null!));
        yield return Null("Constructor_UnusedParameter", () => Members.GetConstructor((Expression<Func<object, object?>>)null!));
        yield return Null("Constructor_Parameter", () => Members.GetConstructor<Subject>(null!));

        yield break;

        static TestCaseData Null(string name, TestDelegate action, string parameter = "selector")
        {
            return new TestCaseData(action, parameter).SetName("NullSelector_" + name);
        }
    }

    [TestCaseSource(nameof(NullSelectors))]
    public static void NullSelectors_ThrowArgumentNullException(TestDelegate action, string parameter)
    {
        Assert.That(Assert.Throws<ArgumentNullException>(action)!.ParamName, Is.EqualTo(parameter));
    }
}

internal static class MemberSelectorTestExtensions
{
    public static string Repeat(this string value, int count)
    {
        throw new InvalidOperationException("Extension method executed.");
    }
}
