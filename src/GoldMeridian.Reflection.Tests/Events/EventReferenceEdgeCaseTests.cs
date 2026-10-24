using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

// These edge cases were primarily sourced from ChatGPT and thus aren't as well-
// scoped as the rest of the test suite. That's the nature of edge cases though,
// and these did catch some scarier bugs.

namespace GoldMeridian.Reflection.Tests;

[TestFixture]
public static class EventReferenceEdgeCaseTests
{
    private delegate void TextHandler(string value);

    private delegate object ResultHandler(string value);

    private delegate void RefHandler(ref int value);

    private sealed class Subject
    {
        public event Action<string>? Variant;
        public event TextHandler? Text;
        public event ResultHandler? Result;
        public event RefHandler? Ref;

        public void Raise(string value)
        {
            Variant?.Invoke(value);
            Text?.Invoke(value);
        }

        public object? RaiseResult(string value)
        {
            return Result?.Invoke(value);
        }

        public void RaiseRef(ref int value)
        {
            Ref?.Invoke(ref value);
        }
    }

    [Test]
    public static void Add_NormalizesVariantRuntimeTypes_EvenWhenDeclaredTypesMatch()
    {
        var subject = new Subject();

        var reference = Events.Get<Subject, Action<string>>(nameof(Subject.Variant));
        var calls = new List<string>();
        Action<string> exact = text => calls.Add("exact:" + text);
        Action<object> broad = text => calls.Add("broad:" + text);
        reference.Add(subject, exact);
        reference.Add(subject, broad);
        subject.Raise("a");
        reference.Remove(subject, broad);
        subject.Raise("b");
        Assert.That(calls, Is.EqualTo(new[] { "exact:a", "broad:a", "exact:b" }));
    }

    [TestCase("add")]
    [TestCase("try")]
    [TestCase("bind")]
    [TestCase("subscribe")]
    public static void Multicast_AllEntryPoints_MatchDelegateRemove(string api)
    {
        var subject = new Subject();
        var reference = Events.Get<Subject, Action<object>>(nameof(Subject.Text));
        var calls = new List<string>();
        Action<object> first = _ => calls.Add("first");
        Action<object> second = _ => calls.Add("second");
        var pair = first + second;
        var bound = reference.Bind(subject);
        var subscription = default(EventReference<Subject, Action<object>>.Subscription);
        reference.Add(subject, pair);
        switch (api)
        {
            case "add":
                reference.Add(subject, pair);
                break;

            case "try":
                Assert.That(reference.TryAdd(subject, pair), Is.True);
                break;

            case "bind":
                bound += pair;
                break;

            case "subscribe":
                subscription = reference.Subscribe(subject, pair);
                break;
        }

        subject.Raise("a");
        switch (api)
        {
            case "add":
                reference.Remove(subject, pair);
                break;

            case "try":
                Assert.That(reference.TryRemove(subject, pair), Is.True);
                break;

            case "bind":
                bound -= pair;
                break;

            case "subscribe":
                subscription.Dispose();
                break;
        }

        subject.Raise("b");
        reference.Remove(subject, second);
        subject.Raise("c");
        reference.Remove(subject, first);
        subject.Raise("d");
        Assert.That(calls, Is.EqualTo(new[] { "first", "second", "first", "second", "first", "second", "first" }));
    }

    [Test]
    public static void Dispose_CopiesAndConcurrentCalls_RemoveOnlyOnce()
    {
        var subject = new Subject();
        var reference = Events.Get<Subject, Action<object>>(nameof(Subject.Text));
        var count = 0;
        Action<object> handler = _ => count++;
        reference.Add(subject, handler);
        var subscription = reference.Subscribe(subject, handler);
        var copy = subscription;
        Parallel.For(0, 20, _ => copy.Dispose());
        subscription.Dispose();
        subject.Raise("test");
        Assert.That(count, Is.EqualTo(1));
        default(EventReference<Subject, Action<object>>.Subscription).Dispose();
    }

    [Test]
    public static void Coercion_CovariantReturn_PreservesLastMulticastResult()
    {
        var subject = new Subject();
        var reference = Events.Get<Subject, Func<object, string>>(nameof(Subject.Result));
        Func<object, string> first = text => "first:" + text;
        Func<object, string> second = text => "second:" + text;
        reference.Add(subject, first + second);
        Assert.That(subject.RaiseResult("a"), Is.EqualTo("second:a"));
        reference.Remove(subject, second);
        Assert.That(subject.RaiseResult("b"), Is.EqualTo("first:b"));
    }

    private delegate void AlternateRefHandler(ref int value);

    private delegate void WrongRefHandler(ref object value);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint NativeHandler();

    private sealed class NativeSubject
    {
        public event Func<uint>? GetId;

        public uint? Raise()
        {
            return GetId?.Invoke();
        }
    }

    [Test]
    public static void Coercion_UnmanagedDelegate_PreservesFunctionPointerAndRemoval()
    {
        // chatgpt suggested these cases for the native function, no idea if
        // anything but windows works right now
        var libraryName = OperatingSystem.IsWindows()
            ? "kernel32.dll"
            : OperatingSystem.IsMacOS()
                ? "/usr/lib/libSystem.B.dylib"
                : "libc.so.6";

        if (!NativeLibrary.TryLoad(libraryName, out var library))
        {
            Assert.Ignore("Native process ID library is unavailable.");
        }

        try
        {
            var pointer = NativeLibrary.GetExport(library, OperatingSystem.IsWindows() ? "GetCurrentProcessId" : "getpid");
            var handler = Marshal.GetDelegateForFunctionPointer<NativeHandler>(pointer);
            var subject = new NativeSubject();
            var reference = Events.Get<NativeSubject, NativeHandler>(nameof(NativeSubject.GetId));

            reference.Add(subject, handler + handler);
            Assert.That(subject.Raise(), Is.EqualTo(handler()));

            var equal = (NativeHandler)handler.Clone();
            reference.Remove(subject, equal);
            Assert.That(subject.Raise(), Is.EqualTo(handler()));
            EventReference<object, NativeHandler>.Create(reference.Info).Remove(subject, equal);
            Assert.That(subject.Raise(), Is.Null);
        }
        finally
        {
            NativeLibrary.Free(library);
        }
    }

    [Test]
    public static void Coercion_ByRef_PreservesWritesAndMulticastOrder()
    {
        var subject = new Subject();
        var reference = Events.Get<Subject, AlternateRefHandler>(nameof(Subject.Ref));
        AlternateRefHandler first = (ref value) => value++;
        AlternateRefHandler second = (ref value) => value *= 3;
        using (reference.Subscribe(subject, first + second))
        {
            var value = 2;
            subject.RaiseRef(ref value);
            Assert.That(value, Is.EqualTo(9));
        }

        var unchanged = 2;
        subject.RaiseRef(ref unchanged);
        Assert.That(unchanged, Is.EqualTo(2));
        Assert.Throws<ArgumentException>(() => Events.Get<Subject, WrongRefHandler>(nameof(Subject.Ref)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void Coercion_CompiledExpressions_CanBeRemovedUsingEqualDelegates(bool interpreted)
    {
        var subject = new Subject();
        var reference = Events.Get<Subject, Action<object>>(nameof(Subject.Text));
        var calls = new List<object>();
        var parameter = Expression.Parameter(typeof(object));
        var handler = Expression.Lambda<Action<object>>(
            Expression.Call(Expression.Constant(calls), typeof(List<object>).GetMethod("Add")!, parameter),
            parameter
        ).Compile(preferInterpretation: interpreted);
        var equal = (Action<object>)handler.Clone();
        Assert.That(equal, Is.EqualTo(handler));
        reference.Add(subject, handler);
        subject.Raise("a");

        // Removal through another reference must work too.
        Events.Get<Subject, Action<object>>(nameof(Subject.Text)).Remove(subject, equal);
        subject.Raise("b");
        Assert.That(calls, Is.EqualTo(new[] { "a" }));
    }

    private sealed class Receiver
    {
        public readonly List<string> Calls = [];

        public void Accept(string value)
        {
            Calls.Add(value);
        }
    }

    private delegate void OpenHandler(Receiver receiver, string value);

    private sealed class OpenSubject
    {
        public event OpenHandler? Changed;

        public void Raise(Receiver receiver, string value)
        {
            Changed?.Invoke(receiver, value);
        }
    }

    [Test]
    public static void Coercion_OpenInstanceDelegate_PreservesReceiver()
    {
        var subject = new OpenSubject();
        var receiver = new Receiver();
        var handler = typeof(Receiver).GetMethod(nameof(Receiver.Accept))!.CreateDelegate<Action<Receiver, string>>();
        var reference = Events.Get<OpenSubject, Action<Receiver, string>>(nameof(OpenSubject.Changed));
        reference.Add(subject, handler);
        subject.Raise(receiver, "a");
        reference.Remove(subject, (Action<Receiver, string>)handler.Clone());
        subject.Raise(receiver, "b");
        Assert.That(receiver.Calls, Is.EqualTo(new[] { "a" }));
    }

    private static void ClosedStatic(List<string> calls, string value)
    {
        calls.Add(value);
    }

    private static string ClosedOverNull(object? target, object value)
    {
        return target is null ? value.ToString()! : throw new InvalidOperationException();
    }

    [Test]
    public static void Coercion_StaticDelegateClosedOverNull_PreservesBinding()
    {
        var handler = typeof(EventReferenceEdgeCaseTests)
                     .GetMethod(nameof(ClosedOverNull), BindingFlags.Static | BindingFlags.NonPublic)!
                     .CreateDelegate<Func<object, string>>(target: null);
        var subject = new Subject();
        var reference = Events.Get<Subject, Func<object, string>>(nameof(Subject.Result));
        using (reference.Subscribe(subject, handler))
        {
            Assert.That(subject.RaiseResult("a"), Is.EqualTo("a"));
        }

        Assert.That(subject.RaiseResult("b"), Is.Null);
    }

    [Test]
    public static void Coercion_DynamicMethod_PreservesMulticastAndRemoval()
    {
        var method = new DynamicMethod("Echo", typeof(string), [typeof(object)]);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, typeof(string));
        il.Emit(OpCodes.Ret);
        var handler = method.CreateDelegate<Func<object, string>>();
        var subject = new Subject();
        var reference = Events.Get<Subject, Func<object, string>>(nameof(Subject.Result));
        reference.Add(subject, handler + handler);
        reference.Remove(subject, (Func<object, string>)handler.Clone());
        Assert.That(subject.RaiseResult("a"), Is.EqualTo("a"));
        reference.Remove(subject, (Func<object, string>)handler.Clone());
        Assert.That(subject.RaiseResult("b"), Is.Null);
    }

    [Test]
    public static void Multicast_OperationSequence_MatchesNativeDelegateBehavior()
    {
        var subject = new Subject();
        var reference = Events.Get<Subject, Action<object>>(nameof(Subject.Text));
        var calls = new List<int>();
        Action<object>[] entries = [_ => calls.Add(1), _ => calls.Add(2), _ => calls.Add(3)];
        Action<object>? native = null;
        var random = new Random(1234);
        for (var step = 0; step < 100; step++)
        {
            var handler = entries[random.Next(entries.Length)];
            if (random.Next(2) == 0) handler += entries[random.Next(entries.Length)];
            if (random.Next(2) == 0)
            {
                native += handler;
                reference.Add(subject, handler);
            }
            else
            {
                native -= handler;
                reference.Remove(subject, handler);
            }

            calls.Clear();
            native?.Invoke("test");
            var expected = calls.ToArray();
            calls.Clear();
            subject.Raise("test");
            Assert.That(calls, Is.EqualTo(expected), $"Operation {step}");
        }
    }

    [Test]
    public static void Multicast_HandlerException_StopsSubsequentHandlers()
    {
        var subject = new Subject();
        var reference = Events.Get<Subject, Action<object>>(nameof(Subject.Text));
        var exception = new AccessorException();
        var called = false;
        Action<object> first = _ => throw exception;
        Action<object> second = _ => called = true;
        using var subscription = reference.Subscribe(subject, first + second);
        Assert.That(Assert.Throws<AccessorException>(() => subject.Raise("test")), Is.SameAs(exception));
        Assert.That(called, Is.False);
    }

    [Test]
    public static void BoundEvent_DefaultAndInvalidTarget_FailClearly()
    {
        Assert.Throws<InvalidOperationException>(
            () =>
            {
                var binding = default(EventReference<Subject, Action<object>>.BoundEvent);
                binding += _ => { };
            }
        );
        Assert.Throws<InvalidOperationException>(
            () =>
            {
                var binding = default(EventReference<Subject, Action<object>>.BoundEvent);
                binding -= _ => { };
            }
        );
        Assert.Throws<TargetException>(() => Events.Get<Subject, Action<object>>(nameof(Subject.Text)).Bind(null));
    }

    [Test]
    public static void BoundEvent_CanBeStoredAndReused()
    {
        var subject = new Subject();
        var reference = Events.Get<Subject, Action<object>>(nameof(Subject.Text));
        EventReference<Subject, Action<object>>.BoundEvent[] bindings = [reference.Bind(subject)];
        var count = 0;
        Action<object> handler = _ => count++;
        bindings[0] += handler;
        subject.Raise("a");
        bindings[0] -= handler;
        subject.Raise("b");
        Assert.That(count, Is.EqualTo(1));
    }

    private delegate ref int RefReturnHandler();

    private delegate ref int AlternateRefReturnHandler();

    private delegate void SpanHandler(Span<int> values);

    private delegate void AlternateSpanHandler(Span<int> values);

    private sealed class SpecialSignatureSubject
    {
        public int Value;
        public event RefReturnHandler? Reference;
        public event SpanHandler? Span;

        public ref int GetValue()
        {
            return ref Value;
        }

        public ref int RaiseReference()
        {
            return ref Reference!();
        }

        public void RaiseSpan(Span<int> values)
        {
            Span?.Invoke(values);
        }
    }

    [Test]
    public static void Coercion_RefReturnsAndRefStructParameters_ArePreserved()
    {
        var subject = new SpecialSignatureSubject();
        var reference = Events.Get<SpecialSignatureSubject, AlternateRefReturnHandler>(nameof(SpecialSignatureSubject.Reference));
        using (reference.Subscribe(subject, subject.GetValue))
        {
            subject.RaiseReference() = 42;
        }

        Assert.That(subject.Value, Is.EqualTo(42));

        var spanReference = Events.Get<SpecialSignatureSubject, AlternateSpanHandler>(nameof(SpecialSignatureSubject.Span));
        using var subscription = spanReference.Subscribe(subject, values => values[0]++);
        var buffer = (Span<int>)[1];
        subject.RaiseSpan(buffer);
        Assert.That(buffer[0], Is.EqualTo(2));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateReleasedHandler()
    {
        var receiver = new Receiver();
        var subject = new Subject();
        var reference = Events.Get<Subject, Action<string>>(nameof(Subject.Text));
        var handler = receiver.Accept;
        reference.Add(subject, handler);
        reference.Remove(subject, handler);
        return new WeakReference(receiver);
    }

    [Test]
    public static void ConversionCache_DoesNotRetainReleasedSubscribers()
    {
        var weak = CreateReleasedHandler();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.That(weak.IsAlive, Is.False);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateCollectibleEvent()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("CollectibleEvent"), AssemblyBuilderAccess.RunAndCollect);
        var module = assembly.DefineDynamicModule("Events");
        var payload = module.DefineType("HiddenPayload", TypeAttributes.NotPublic).CreateType();
        var eventType = typeof(Action<>).MakeGenericType(payload);
        var builder = module.DefineType("HiddenSource", TypeAttributes.NotPublic);
        var eventBuilder = builder.DefineEvent("Changed", EventAttributes.None, eventType);
        const MethodAttributes attributes = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
        var add = builder.DefineMethod("add_Changed", attributes, typeof(void), [eventType]);
        add.GetILGenerator().Emit(OpCodes.Ret);
        var remove = builder.DefineMethod("remove_Changed", attributes, typeof(void), [eventType]);
        remove.GetILGenerator().Emit(OpCodes.Ret);
        eventBuilder.SetAddOnMethod(add);
        eventBuilder.SetRemoveOnMethod(remove);
        var sourceType = builder.CreateType();
        var source = Activator.CreateInstance(sourceType)!;
        var reference = EventReference<object, Action<object>>.Create(sourceType.GetEvent("Changed")!);
        Action<object> handler = _ => { };
        reference.Add(source, handler);
        reference.Remove(source, handler);
        return new WeakReference(sourceType.Assembly);
    }

    [Test]
    public static void AccessorAndConversionCaches_DoNotRetainCollectibleEvents()
    {
        var weak = CreateCollectibleEvent();
        for (var i = 0; i < 5 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.That(weak.IsAlive, Is.False);
    }

    [Test]
    public static void Coercion_ClosedStaticDelegate_PreservesBoundArgument()
    {
        var subject = new Subject();
        var calls = new List<string>();
        var handler = typeof(EventReferenceEdgeCaseTests).GetMethod(nameof(ClosedStatic), BindingFlags.Static | BindingFlags.NonPublic)!
                                                         .CreateDelegate<Action<string>>(calls);
        var reference = Events.Get<Subject, Action<string>>(nameof(Subject.Text));
        using (reference.Subscribe(subject, handler))
        {
            subject.Raise("a");
        }

        subject.Raise("b");
        Assert.That(calls, Is.EqualTo(new[] { "a" }));
    }

    private struct StructSubject
    {
        public event Action? Changed;

        public void Raise()
        {
            Changed?.Invoke();
        }
    }

    [Test]
    public static void BoxedStructTarget_MutatesTheOriginalBox()
    {
        object subject = new StructSubject();
        var reference = EventReference<object, Action>.Create(typeof(StructSubject).GetEvent(nameof(StructSubject.Changed))!);
        var count = 0;
        using (reference.Subscribe(subject, () => count++))
        {
            ((StructSubject)subject).Raise();
        }

        ((StructSubject)subject).Raise();
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public static void UnboxedStructTarget_IsRejectedInsteadOfSubscribingToACopy()
    {
        Assert.Throws<ArgumentException>(() => Events.Get<StructSubject, Action>(nameof(StructSubject.Changed)));
    }

    private sealed class AccessorException : Exception;

    private sealed class CustomSubject
    {
        public int AddCount;
        public int RemoveCount;
        public Action<string>? LastHandler;

        public event Action<string> Tracked
        {
            add
            {
                AddCount++;
                LastHandler = value;
            }

            remove
            {
                RemoveCount++;
                LastHandler = value;
            }
        }

        public event Action<string> Throws
        {
            add => throw new AccessorException();
            remove => throw new AccessorException();
        }

        public static event Action<string> StaticThrows
        {
            add => throw new AccessorException();
            remove => throw new AccessorException();
        }
    }

    [TestCase(nameof(CustomSubject.Throws), false)]
    [TestCase(nameof(CustomSubject.Throws), true)]
    [TestCase(nameof(CustomSubject.StaticThrows), false)]
    [TestCase(nameof(CustomSubject.StaticThrows), true)]
    public static void AccessorExceptions_AreConsistentAcrossPaths(string name, bool coerce)
    {
        var subject = new CustomSubject();
        if (coerce)
        {
            var reference = Events.Get<CustomSubject, Action<object>>(name);
            Assert.Throws<AccessorException>(() => reference.TryAdd(subject, _ => { }));
            Assert.Throws<AccessorException>(() => reference.TryRemove(subject, _ => { }));
        }
        else
        {
            var reference = Events.Get<CustomSubject, Action<string>>(name);
            Assert.Throws<AccessorException>(() => reference.Add(subject, _ => { }));
            Assert.Throws<AccessorException>(() => reference.Remove(subject, _ => { }));
        }
    }

    [Test]
    public static void NullHandlers_ArePassedToCustomAccessors()
    {
        var subject = new CustomSubject();
        var reference = Events.Get<CustomSubject, Action<object>>(nameof(CustomSubject.Tracked));
        reference.Add(subject, null!);
        reference.Remove(subject, null!);
        Assert.That(subject.AddCount, Is.EqualTo(1));
        Assert.That(subject.RemoveCount, Is.EqualTo(1));
        Assert.That(subject.LastHandler, Is.Null);
    }

    [Test]
    public static void InstanceTargets_AreValidatedConsistently()
    {
        var reference = EventReference<object, Action<object>>.Create(typeof(Subject).GetEvent(nameof(Subject.Text))!);
        Assert.Throws<TargetException>(() => reference.Add(null, _ => { }));
        Assert.Throws<TargetException>(() => reference.Remove(new object(), _ => { }));
        Assert.Throws<TargetException>(() => Events.Get<Subject, Action<string>>(nameof(Subject.Variant)).Add(null, _ => { }));
    }

    [Test]
    public static void MissingAccessors_TryReturnsFalse_RequiredOperationsThrowBeforeAdding()
    {
        var subject = new CustomSubject();
        var info = typeof(CustomSubject).GetEvent(nameof(CustomSubject.Tracked))!;
        var addOnly = EventReference<CustomSubject, Action<string>>.Create(new AccessorEvent(info, info.AddMethod, null));
        var removeOnly = EventReference<CustomSubject, Action<string>>.Create(new AccessorEvent(info, null, info.RemoveMethod));
        Assert.That(addOnly.TryRemove(subject, null!), Is.False);
        Assert.That(removeOnly.TryAdd(subject, null!), Is.False);
        Assert.Throws<InvalidOperationException>(() => addOnly.Remove(subject, null!));
        Assert.Throws<InvalidOperationException>(() => removeOnly.Add(subject, null!));
        Assert.Throws<InvalidOperationException>(() => addOnly.Subscribe(subject, null!));
        Assert.Throws<InvalidOperationException>(() => removeOnly.Subscribe(subject, null!));
        Assert.That(subject.AddCount, Is.Zero);
    }

    private sealed class AccessorEvent(EventInfo original, MethodInfo? add, MethodInfo? remove) : EventInfo
    {
        public override EventAttributes Attributes => original.Attributes;
        public override Type? DeclaringType => original.DeclaringType;
        public override string Name => original.Name;
        public override Type? ReflectedType => original.ReflectedType;
        public override Type? EventHandlerType => original.EventHandlerType;

        public override MethodInfo? GetAddMethod(bool nonPublic)
        {
            return add;
        }

        public override MethodInfo? GetRemoveMethod(bool nonPublic)
        {
            return remove;
        }

        public override MethodInfo? GetRaiseMethod(bool nonPublic)
        {
            return null;
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return [];
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return [];
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return false;
        }
    }
}
