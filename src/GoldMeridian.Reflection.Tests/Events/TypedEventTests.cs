using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace GoldMeridian.Reflection.Tests;

[TestFixture]
public static class TypedEventTests
{
    public sealed class MouseArgs;

    public sealed class SpriteBatch;

    public delegate void MouseEvent(MouseArgs args, GuiElement element);

    public delegate void TypedMouseEvent<in TElement>(MouseArgs args, TElement element)
        where TElement : GuiElement;

    public delegate void TypedDrawEvent<in TElement>(TElement element, SpriteBatch sb)
        where TElement : GuiElement;

    public class GuiElement
    {
        public event MouseEvent? MouseDown;
        public event Action<GuiElement>? Update;
        public event Action<GuiElement, SpriteBatch>? Draw;

        public void RaiseMouse(GuiElement? argument = null)
        {
            MouseDown?.Invoke(new MouseArgs(), argument ?? this);
        }

        public void RaiseUpdate()
        {
            Update?.Invoke(this);
        }

        public void RaiseDraw()
        {
            Draw?.Invoke(this, new SpriteBatch());
        }
    }

    public sealed class Button : GuiElement;

    public sealed class ShadowButton : GuiElement
    {
        public new event Action<int>? MouseDown;

        public void RaiseShadow()
        {
            MouseDown?.Invoke(1);
        }
    }

    [Test]
    public static void ExtensionProperty_WrapsTheBaseEvent_EvenIfDerivedTypeHidesIt()
    {
        var button = new ShadowButton();
        var count = 0;
        TypedMouseEvent<ShadowButton> handler = (_, _) => count++;

        button.MouseDownTyped += handler;
        button.RaiseMouse();
        button.RaiseShadow();

        button.MouseDownTyped -= handler;
        button.RaiseMouse();

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public static void ExtensionProperty_RepeatedGetters_AddAndRemoveEqualHandlers()
    {
        var button = new Button();
        var count = 0;

        button.MouseDownTyped += Handler;
        button.RaiseMouse();

        button.MouseDownTyped -= Handler;
        button.RaiseMouse();

        Assert.That(count, Is.EqualTo(1));
        return;

        void Handler(MouseArgs args, Button listeningElement)
        {
            Assert.That(listeningElement, Is.SameAs(button));
            count++;
        }
    }

    [Test]
    public static void ExtensionProperties_SupportDifferentParameterPositionsAndCounts()
    {
        var button = new Button();
        var updates = 0;
        var draws = 0;

        button.UpdateTyped += Update;
        button.DrawTyped += Draw;
        button.RaiseUpdate();
        button.RaiseDraw();

        button.UpdateTyped -= Update;
        button.DrawTyped -= Draw;
        button.RaiseUpdate();
        button.RaiseDraw();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(updates, Is.EqualTo(1));
            Assert.That(draws, Is.EqualTo(1));
        }

        return;

        void Update(Button element)
        {
            Assert.That(element, Is.SameAs(button));
            updates++;
        }

        void Draw(Button element, SpriteBatch batch)
        {
            Assert.That(element, Is.SameAs(button));
            draws++;
        }
    }

    [TestCase("add")]
    [TestCase("try")]
    [TestCase("subscribe")]
    [TestCase("extension")]
    public static void Multicast_PreservesOrderDuplicatesAndPartialRemoval(string api)
    {
        var button = new Button();
        var reference = Events.Get<Button, TypedMouseEvent<Button>>(nameof(GuiElement.MouseDown), allowParameterDowncasts: true);
        var calls = new List<int>();
        TypedMouseEvent<Button> first = (_, _) => calls.Add(1);
        TypedMouseEvent<Button> second = (_, _) => calls.Add(2);
        TypedMouseEvent<Button> middle = (_, _) => calls.Add(3);
        var pair = first + second;
        var subscription = default(EventReference<Button, TypedMouseEvent<Button>>.Subscription);
        reference.Add(button, pair + middle);

        switch (api)
        {
            case "add":
                reference.Add(button, pair);
                break;

            case "try":
                Assert.That(reference.TryAdd(button, pair), Is.True);
                break;

            case "subscribe":
                subscription = reference.Subscribe(button, pair);
                break;

            case "extension":
                button.MouseDownTyped += pair;
                break;
        }

        button.RaiseMouse();
        Assert.That(calls, Is.EqualTo(new[] { 1, 2, 3, 1, 2 }));

        calls.Clear();

        var equal = (TypedMouseEvent<Button>)pair.Clone();
        switch (api)
        {
            case "add":
                reference.Remove(button, equal);
                break;

            case "try":
                Assert.That(reference.TryRemove(button, equal), Is.True);
                break;

            case "subscribe":
                subscription.Dispose();
                break;

            case "extension":
                button.MouseDownTyped -= equal;
                break;
        }

        button.RaiseMouse();
        Assert.That(calls, Is.EqualTo(new[] { 1, 2, 3 }));

        calls.Clear();

        button.MouseDownTyped -= second;
        button.RaiseMouse();

        Assert.That(calls, Is.EqualTo(new[] { 1, 3 }));
    }

    [Test]
    public static void DefaultPolicy_StillRejectsNarrowing_AfterOptInReferenceIsCached()
    {
        var info = typeof(GuiElement).GetEvent(nameof(GuiElement.MouseDown))!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Events.TryGet<Button, TypedMouseEvent<Button>>(nameof(GuiElement.MouseDown), out var optedIn, allowParameterDowncasts: true), Is.True);
            Assert.That(optedIn!.AllowsParameterDowncasts, Is.True);
        }

        Assert.Throws<ArgumentException>(() => Events.Get<Button, TypedMouseEvent<Button>>(nameof(GuiElement.MouseDown)));
        Assert.Throws<ArgumentException>(() => EventReference<GuiElement, TypedMouseEvent<Button>>.Create(info));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(EventReference<GuiElement, TypedMouseEvent<Button>>.Create(info, allowParameterDowncasts: true).AllowsParameterDowncasts, Is.True);
            Assert.That(Events.Get<Button, MouseEvent>(nameof(GuiElement.MouseDown)).AllowsParameterDowncasts, Is.False);
        }
    }

    [Test]
    public static void IncorrectArgument_ThrowsCheckedCast_WithoutSubstitutingTarget()
    {
        var button = new Button();
        var other = new Button();
        Button? received = null;
        TypedMouseEvent<Button> handler = (_, element) => received = element;

        button.MouseDownTyped += handler;
        button.RaiseMouse(other);

        Assert.That(received, Is.SameAs(other));
        Assert.Throws<InvalidCastException>(() => button.RaiseMouse(new GuiElement()));

        button.MouseDownTyped -= handler;

        Assert.DoesNotThrow(() => button.RaiseMouse(new GuiElement()));
    }

    [Test]
    public static void HandlerEquality_IsSharedAcrossTargetsAndReferences()
    {
        var first = new Button();
        var second = new Button();
        var calls = 0;

        first.MouseDownTyped += Handler;
        second.MouseDownTyped += Handler;

        var reference = EventReference<object, TypedMouseEvent<Button>>.Create(
            typeof(GuiElement).GetEvent(nameof(GuiElement.MouseDown))!,
            allowParameterDowncasts: true
        );
        reference.Remove(first, Handler);

        first.RaiseMouse();
        second.RaiseMouse();

        Assert.That(calls, Is.EqualTo(1));

        second.MouseDownTyped -= Handler;
        second.RaiseMouse();

        Assert.That(calls, Is.EqualTo(1));
        return;

        void Handler(MouseArgs args, Button element) => calls++;
    }

    [Test]
    public static void VariantRuntimeHandlers_CanMixWithNarrowHandlers()
    {
        var button = new Button();
        var calls = new List<int>();
        TypedMouseEvent<GuiElement> broad = (_, _) => calls.Add(1);
        TypedMouseEvent<Button> narrow = (_, _) => calls.Add(2);

        button.MouseDownTyped += broad;
        button.MouseDownTyped += narrow;
        button.RaiseMouse();

        button.MouseDownTyped -= broad;
        button.RaiseMouse();

        button.MouseDownTyped -= narrow;

        Assert.That(calls, Is.EqualTo(new[] { 1, 2, 2 }));
    }

    private sealed class Receiver
    {
        public int Calls;

        public void Handle(MouseArgs args, Button element)
        {
            Calls++;
        }
    }

    [Test]
    public static void EqualHandlers_AdaptedConcurrently_RemainRemovable()
    {
        var button = new Button();
        var receiver = new Receiver();

        Parallel.For(0, 20, _ => button.MouseDownTyped += receiver.Handle);
        button.RaiseMouse();

        Assert.That(receiver.Calls, Is.EqualTo(20));

        Parallel.For(0, 20, _ => button.MouseDownTyped -= receiver.Handle);
        button.RaiseMouse();

        Assert.That(receiver.Calls, Is.EqualTo(20));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference SubscribeAndReleaseHandler(Button button, Receiver receiver)
    {
        var handler = new TypedMouseEvent<Button>(receiver.Handle);
        button.MouseDownTyped += handler;
        return new WeakReference(handler);
    }

    [Test]
    public static void AdapterSurvivesGc_WhileSubscribed_AndRemovesByMethodGroup()
    {
        var button = new Button();
        var receiver = new Receiver();

        var weak = SubscribeAndReleaseHandler(button, receiver);
        GC.Collect();
        GC.WaitForPendingFinalizers();

        button.RaiseMouse();
        button.MouseDownTyped -= receiver.Handle;
        button.RaiseMouse();

        Assert.That(receiver.Calls, Is.EqualTo(1));

        GC.KeepAlive(weak);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AddAndRemoveReceiver(Button button)
    {
        var receiver = new Receiver();
        button.MouseDownTyped += receiver.Handle;
        button.MouseDownTyped -= receiver.Handle;
        return new WeakReference(receiver);
    }

    [Test]
    public static void RemovedAdapters_DoNotRetainSubscriber()
    {
        var button = new Button();
        var weak = AddAndRemoveReceiver(button);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.That(weak.IsAlive, Is.False);
        GC.KeepAlive(button);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void WarmAddRemoveAndInvocation_DoNotAllocate(bool propertySyntax)
    {
        var button = new Button();
        var reference = Events.Get<Button, Action<Button>>(nameof(GuiElement.Update), allowParameterDowncasts: true);
        var calls = 0;
        Action<Button> handler = _ => calls++;

        // warmup
        for (var i = 0; i < 10_000; i++)
        {
            Cycle();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            Cycle();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(allocated, Is.Zero);
            Assert.That(calls, Is.EqualTo(20_000));
        }

        return;

        void Cycle()
        {
            if (propertySyntax)
            {
                button.UpdateTyped += handler;
                button.RaiseUpdate();
                button.UpdateTyped -= handler;
            }
            else
            {
                reference.Add(button, handler);
                button.RaiseUpdate();
                reference.Remove(button, handler);
            }
        }
    }

    private delegate void WrongRefHandler(ref Button element);

    private delegate void RefBaseHandler(ref GuiElement element);

    private delegate object BaseResultHandler(GuiElement element);

    private delegate void RefCountHandler(GuiElement element, ref int count);

    private delegate ref int BaseRefResultHandler(GuiElement element);

    private delegate ref int ButtonRefResultHandler(Button element);

    private delegate void BaseSpanHandler(GuiElement element, Span<int> values);

    private delegate void ButtonSpanHandler(Button element, Span<int> values);

    private sealed class SignatureSubject
    {
        public event RefBaseHandler? Reference;
        public event BaseResultHandler? Result;
        public event RefCountHandler? Count;
        public event BaseRefResultHandler? RefResult;
        public event BaseSpanHandler? Span;

        public int Value;

        public void RaiseReference(ref GuiElement element)
        {
            Reference?.Invoke(ref element);
        }

        public object? RaiseResult(GuiElement element)
        {
            return Result?.Invoke(element);
        }

        public void RaiseCount(GuiElement element, ref int count)
        {
            Count?.Invoke(element, ref count);
        }

        public ref int RaiseRefResult(GuiElement element)
        {
            return ref RefResult!(element);
        }

        public void RaiseSpan(GuiElement element, Span<int> values)
        {
            Span?.Invoke(element, values);
        }
    }

    private delegate void ButtonRefCountHandler(Button element, ref int count);

    [Test]
    public static void Narrowing_CanCombineWithCovariantReturnsAndUnchangedByRefParameters()
    {
        var subject = new SignatureSubject();
        var reference = Events.Get<SignatureSubject, Func<Button, string>>(nameof(SignatureSubject.Result), allowParameterDowncasts: true);
        Func<Button, string> first = _ => "first";
        Func<Button, string> second = _ => "second";

        using (reference.Subscribe(subject, first + second))
        {
            Assert.That(subject.RaiseResult(new Button()), Is.EqualTo("second"));
        }

        Assert.That(subject.RaiseResult(new Button()), Is.Null);

        var counts = Events.Get<SignatureSubject, ButtonRefCountHandler>(nameof(SignatureSubject.Count), allowParameterDowncasts: true);
        using var subscription = counts.Subscribe(subject, (_, ref value) => value++);

        var count = 0;
        subject.RaiseCount(new Button(), ref count);

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public static void OptIn_DoesNotAllowBoxingNumericConversionsOrByRefNarrowing()
    {
        Assert.Throws<ArgumentException>(() => Events.Get<Button, Action<int>>(nameof(GuiElement.Update), allowParameterDowncasts: true));
        Assert.Throws<ArgumentException>(() => Events.Get<Button, Action<string>>(nameof(GuiElement.Update), allowParameterDowncasts: true));
        Assert.Throws<ArgumentException>(() => Events.Get<Button, Action<Button, int>>(nameof(GuiElement.Update), allowParameterDowncasts: true));
        Assert.Throws<ArgumentException>(() => Events.Get<SignatureSubject, WrongRefHandler>(nameof(SignatureSubject.Reference), allowParameterDowncasts: true));
        Assert.Throws<ArgumentException>(() => Events.Get<SignatureSubject, Func<Button, int>>(nameof(SignatureSubject.Result), allowParameterDowncasts: true));
    }

    [Test]
    public static void Narrowing_PreservesRefReturnsAndSpanParameters()
    {
        var subject = new SignatureSubject();
        var reference = Events.Get<SignatureSubject, ButtonRefResultHandler>(nameof(SignatureSubject.RefResult), allowParameterDowncasts: true);
        using (reference.Subscribe(subject, GetValue))
        {
            subject.RaiseRefResult(new Button()) = 42;
        }

        Assert.That(subject.Value, Is.EqualTo(42));

        var spans = Events.Get<SignatureSubject, ButtonSpanHandler>(nameof(SignatureSubject.Span), allowParameterDowncasts: true);
        using var subscription = spans.Subscribe(subject, (_, values) => values[0]++);

        var buffer = (Span<int>)[1];
        subject.RaiseSpan(new Button(), buffer);

        Assert.That(buffer[0], Is.EqualTo(2));
        return;

        ref int GetValue(Button element) => ref subject.Value;
    }

    private static class StaticSubject
    {
        public static event Action<GuiElement?>? Changed;

        public static void Raise(GuiElement? element)
        {
            Changed?.Invoke(element);
        }
    }

    [Test]
    public static void StaticEvents_IgnoreTarget_AndPreserveNullArguments()
    {
        var reference = EventReference<object, Action<Button?>>.Create(
            typeof(StaticSubject).GetEvent(nameof(StaticSubject.Changed))!,
            allowParameterDowncasts: true
        );
        var calls = new List<Button?>();
        var button = new Button();

        using (reference.Subscribe(new object(), calls.Add))
        {
            StaticSubject.Raise(button);
            StaticSubject.Raise(null);
        }

        StaticSubject.Raise(button);

        Assert.That(calls, Is.EqualTo(new[] { button, null }));

        reference.Add(null, null);
        reference.Remove(null, null);
    }

    [Test]
    public static void CompiledExpressionHandler_RemovesUsingEqualClone()
    {
        var calls = new List<Button>();
        var parameter = Expression.Parameter(typeof(Button));
        var handler = Expression.Lambda<Action<Button>>(
            Expression.Call(Expression.Constant(calls), typeof(List<Button>).GetMethod(nameof(List<>.Add))!, parameter),
            parameter
        ).Compile();
        var button = new Button();

        button.UpdateTyped += handler;
        button.RaiseUpdate();

        button.UpdateTyped -= (Action<Button>)handler.Clone();
        button.RaiseUpdate();

        Assert.That(calls, Is.EqualTo(new[] { button }));
    }

    [Test]
    public static void HandlerException_IsUnwrapped_AndStopsMulticastInvocation()
    {
        var button = new Button();
        var exception = new InvalidOperationException("handler failure");
        var reached = false;
        Action<Button> first = _ => throw exception;
        Action<Button> second = _ => reached = true;

        button.UpdateTyped += first + second;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Assert.Throws<InvalidOperationException>(button.RaiseUpdate), Is.SameAs(exception));
            Assert.That(reached, Is.False);
        }

        button.UpdateTyped -= first + second;
        Assert.DoesNotThrow(button.RaiseUpdate);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateCollectibleDowncastEvent()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("CollectibleDowncastEvent"), AssemblyBuilderAccess.RunAndCollect);
        var module = assembly.DefineDynamicModule("Events");
        var delegateBuilder = module.DefineType("HiddenHandler", TypeAttributes.Sealed, typeof(MulticastDelegate));

        var constructor = delegateBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            [typeof(object), typeof(nint)]
        );
        constructor.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

        var invoke = delegateBuilder.DefineMethod(
            "Invoke",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(void),
            [typeof(object)]
        );
        invoke.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

        var handlerType = delegateBuilder.CreateType();
        var sourceBuilder = module.DefineType("Source");
        var eventBuilder = sourceBuilder.DefineEvent("Changed", EventAttributes.None, handlerType);

        var add = sourceBuilder.DefineMethod("add_Changed", MethodAttributes.Public | MethodAttributes.SpecialName, typeof(void), [handlerType]);
        add.GetILGenerator().Emit(OpCodes.Ret);

        var remove = sourceBuilder.DefineMethod("remove_Changed", MethodAttributes.Public | MethodAttributes.SpecialName, typeof(void), [handlerType]);
        remove.GetILGenerator().Emit(OpCodes.Ret);

        eventBuilder.SetAddOnMethod(add);
        eventBuilder.SetRemoveOnMethod(remove);

        var sourceType = sourceBuilder.CreateType();
        var source = Activator.CreateInstance(sourceType)!;
        var reference = EventReference<object, Action<Button>>.Create(sourceType.GetEvent("Changed")!, allowParameterDowncasts: true);
        Action<Button> handler = _ => { };

        reference.Add(source, handler);
        reference.Remove(source, handler);

        return new WeakReference(sourceType.Assembly);
    }

    [Test]
    public static void DowncastFactoryCache_DoesNotRetainCollectibleEventTypes()
    {
        var weak = CreateCollectibleDowncastEvent();
        for (var i = 0; i < 5 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.That(weak.IsAlive, Is.False);
    }
}

internal static class TypedEventExtensions
{
    extension<TElement>(TElement element) where TElement : TypedEventTests.GuiElement
    {
        public EventWrapper<TypedEventTests.TypedMouseEvent<TElement>> MouseDownTyped
        {
            get => Events.Wrap<TypedEventTests.GuiElement, TypedEventTests.TypedMouseEvent<TElement>>(
                element,
                nameof(TypedEventTests.GuiElement.MouseDown),
                allowParameterDowncasts: true
            );

            set => _ = value;
        }

        public EventWrapper<Action<TElement>> UpdateTyped
        {
            get => Events.Wrap<TypedEventTests.GuiElement, Action<TElement>>(
                element,
                nameof(TypedEventTests.GuiElement.Update),
                allowParameterDowncasts: true
            );

            set => _ = value;
        }

        public EventWrapper<TypedEventTests.TypedDrawEvent<TElement>> DrawTyped
        {
            get => Events.Wrap<TypedEventTests.GuiElement, TypedEventTests.TypedDrawEvent<TElement>>(
                element,
                nameof(TypedEventTests.GuiElement.Draw),
                allowParameterDowncasts: true
            );

            set => _ = value;
        }
    }
}
