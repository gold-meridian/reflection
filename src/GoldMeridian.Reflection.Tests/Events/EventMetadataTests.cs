using System;
using System.Reflection;

namespace GoldMeridian.Reflection.Tests;

[TestFixture]
public static class EventMetadataTests
{
    private interface ISource
    {
        event Action<string> Changed;
    }

    private sealed class ExplicitSource : ISource
    {
        event Action<string> ISource.Changed
        {
            add => handlers += value;
            remove => handlers -= value;
        }

        private Action<string>? handlers;

        public void Raise()
        {
            handlers?.Invoke("hello");
        }
    }

    private struct StructSource : ISource
    {
        public event Action<string>? Changed;

        public void Raise()
        {
            Changed?.Invoke("hello");
        }
    }

    private class BaseSource
    {
        public static event Action<string>? StaticChanged;

        public virtual event Action<string>? Changed;

        public void RaiseBase()
        {
            Changed?.Invoke("base");
        }

        public static void RaiseStatic()
        {
            StaticChanged?.Invoke("static");
        }
    }

    private sealed class DerivedSource : BaseSource
    {
        public override event Action<string>? Changed;

        public void Raise()
        {
            Changed?.Invoke("derived");
        }
    }

    private sealed class GenericSource<T>
    {
        public event Action<T>? Changed;

        public void Raise(T value)
        {
            Changed?.Invoke(value);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void InterfaceEvent_DispatchesToExplicitImplementation(bool exact)
    {
        var subject = new ExplicitSource();

        var received = default(object);
        if (exact)
        {
            var reference = Events.Get<ISource, Action<string>>(nameof(ISource.Changed));
            using (reference.Subscribe(subject, text => received = text))
            {
                subject.Raise();
            }
        }
        else
        {
            var reference = EventReference<object, Action<object>>.Create(typeof(ISource).GetEvent(nameof(ISource.Changed))!);
            using (reference.Subscribe(subject, text => received = text))
            {
                subject.Raise();
            }
        }

        Assert.That(received, Is.EqualTo("hello"));

        received = null;
        subject.Raise();

        Assert.That(received, Is.Null);
    }

    [Test]
    public static void InterfaceTarget_PreservesExistingStructBox()
    {
        ISource source = new StructSource();
        var count = 0;

        var reference = Events.Get<ISource, Action<object>>(nameof(ISource.Changed));
        using (reference.Subscribe(source, _ => count++))
        {
            ((StructSource)source).Raise();
        }

        ((StructSource)source).Raise();
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public static void ConcreteStructEvent_WithInterfaceTarget_PreservesExistingBox()
    {
        ISource source = new StructSource();
        var count = 0;

        var reference = EventReference<ISource, Action<object>>.Create(typeof(StructSource).GetEvent(nameof(StructSource.Changed))!);
        using (reference.Subscribe(source, _ => count++))
        {
            ((StructSource)source).Raise();
        }

        ((StructSource)source).Raise();
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public static void VirtualAccessors_DispatchToOverride()
    {
        var subject = new DerivedSource();

        var count = 0;
        var reference = Events.Get<BaseSource, Action<object>>(nameof(BaseSource.Changed));
        using (reference.Subscribe(subject, _ => count++))
        {
            subject.RaiseBase();
            subject.Raise();
        }

        subject.Raise();
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public static void InheritedStaticEvent_IsFound_AndIgnoresTarget()
    {
        var count = 0;
        var reference = Events.Get<DerivedSource, Action<object>>(nameof(BaseSource.StaticChanged));
        using (reference.Subscribe(null, _ => count++))
        {
            BaseSource.RaiseStatic();
        }

        BaseSource.RaiseStatic();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(count, Is.EqualTo(1));
            Assert.That(reference.IsStatic, Is.True);
            Assert.That(reference.CanAdd && reference.CanRemove, Is.True);
            Assert.That(reference.HandlerType, Is.EqualTo(typeof(Action<object>)));
            Assert.That(reference.EventHandlerType, Is.EqualTo(typeof(Action<string>)));
        }
    }

    [Test]
    public static void ClosedGenericEvent_Works_AndOpenGenericEventIsRejected()
    {
        var subject = new GenericSource<string>();

        object? received = null;
        var reference = Events.Get<GenericSource<string>, Action<object>>(nameof(GenericSource<string>.Changed));
        using (reference.Subscribe(subject, value => received = value))
        {
            subject.Raise("hello");
        }

        Assert.That(received, Is.EqualTo("hello"));

        Assert.Throws<ArgumentException>(() => EventReference<object, Action<object>>.Create(typeof(GenericSource<>).GetEvent("Changed")!));
    }

    [Test]
    public static void Lookup_ValidatesInputs_AndReusesReferences()
    {
        Assert.Throws<ArgumentNullException>(() => EventReference<object, Action>.Create(null!));
        Assert.Throws<ArgumentNullException>(() => Events.Get<BaseSource, Action>(null!));
        Assert.Throws<MissingMemberException>(() => Events.Get<BaseSource, Action>("missing"));
        Assert.That(Events.TryGet<BaseSource, Action>("missing", out var missing), Is.False);
        Assert.That(missing, Is.Null);
        Assert.Throws<ArgumentException>(() => Events.Get<BaseSource, Delegate>(nameof(BaseSource.Changed)));
        Assert.Throws<ArgumentException>(() => Events.Get<BaseSource, MulticastDelegate>(nameof(BaseSource.Changed)));
        Assert.That(
            Events.Get<BaseSource, Action<object>>(nameof(BaseSource.Changed)),
            Is.SameAs(Events.Get<BaseSource, Action<object>>(nameof(BaseSource.Changed)))
        );
    }

    private sealed class AlteredEvent(EventInfo original, MethodInfo? add, MethodInfo? remove) : EventInfo
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

    private static void WrongArity() { }
    private static int WrongReturn(Action<string> _) => 0;
    private static void WrongHandler(Action<int> _) { }
    private static void ValidStatic(Action<string> _) { }

    [TestCase(nameof(WrongArity))]
    [TestCase(nameof(WrongReturn))]
    [TestCase(nameof(WrongHandler))]
    [TestCase(nameof(ValidStatic))]
    public static void MalformedAccessorMetadata_IsRejectedAtCreation(string methodName)
    {
        var info = typeof(BaseSource).GetEvent(nameof(BaseSource.Changed))!;
        var method = typeof(EventMetadataTests).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.Throws<ArgumentException>(() => EventReference<object, Action<object>>.Create(new AlteredEvent(info, method, info.RemoveMethod)));
    }

    [Test]
    public static void EventWithoutAccessors_IsRejectedAtCreation()
    {
        var info = typeof(BaseSource).GetEvent(nameof(BaseSource.Changed))!;
        Assert.Throws<ArgumentException>(() => EventReference<object, Action<object>>.Create(new AlteredEvent(info, null, null)));
    }
}
