using System;
using System.Collections.Generic;
using System.Reflection;

namespace GoldMeridian.Reflection.Tests;

[TestFixture]
public static class EventReferenceTests
{
    private sealed class TestSubject
    {
        public static event EventHandler? StaticChanged;

        public event EventHandler? Changed;

        internal event EventHandler? InternalChanged;

        private event EventHandler? PrivateChanged;

        public int ChangeCount { get; private set; }

        public void RaiseChanged()
        {
            ChangeCount++;
            StaticChanged?.Invoke(null, EventArgs.Empty);
            Changed?.Invoke(this, EventArgs.Empty);
            InternalChanged?.Invoke(this, EventArgs.Empty);
            PrivateChanged?.Invoke(this, EventArgs.Empty);
        }

        public static void Setup()
        {
            StaticChanged = null;
        }
    }

    [SetUp]
    public static void SetUp()
    {
        TestSubject.Setup();
    }

    [TestCase("StaticChanged")]
    [TestCase("Changed")]
    [TestCase("InternalChanged")]
    [TestCase("PrivateChanged")]
    public static void TryAdd_SubjectReceivesEvent(string eventName)
    {
        var subject = new TestSubject();
        var reference = Events.Get<TestSubject, EventHandler>(eventName);

        var count = 0;

        Assert.That(reference.TryAdd(subject, Handler), Is.True);
        subject.RaiseChanged();

        Assert.That(count, Is.EqualTo(1));
        return;

        void Handler(object? o, EventArgs eventArgs) => count++;
    }

    [TestCase("StaticChanged")]
    [TestCase("Changed")]
    [TestCase("InternalChanged")]
    [TestCase("PrivateChanged")]
    public static void Add_SubjectReceivesEvent(string eventName)
    {
        var subject = new TestSubject();
        var reference = Events.Get<TestSubject, EventHandler>(eventName);

        var count = 0;

        reference.Add(subject, Handler);
        subject.RaiseChanged();

        Assert.That(count, Is.EqualTo(1));
        return;

        void Handler(object? o, EventArgs eventArgs) => count++;
    }

    [TestCase("StaticChanged")]
    [TestCase("Changed")]
    [TestCase("InternalChanged")]
    [TestCase("PrivateChanged")]
    public static void Bind_SubjectReceivesEvent(string eventName)
    {
        var subject = new TestSubject();
        var reference = Events.Get<TestSubject, EventHandler>(eventName);
        var binding = reference.Bind(subject);

        var count = 0;

        binding += Handler;
        subject.RaiseChanged();

        Assert.That(count, Is.EqualTo(1));
        return;

        void Handler(object? o, EventArgs eventArgs) => count++;
    }

    [TestCase("StaticChanged")]
    [TestCase("Changed")]
    [TestCase("InternalChanged")]
    [TestCase("PrivateChanged")]
    public static void Remove_SubjectStopsReceivingEvent(string eventName)
    {
        var subject = new TestSubject();
        var reference = Events.Get<TestSubject, EventHandler>(eventName);

        var count = 0;

        reference.Add(subject, Handler);
        subject.RaiseChanged();

        reference.Remove(subject, Handler);
        subject.RaiseChanged();

        Assert.That(count, Is.EqualTo(1));
        return;

        void Handler(object? o, EventArgs eventArgs) => count++;
    }

    [TestCase("StaticChanged")]
    [TestCase("Changed")]
    [TestCase("InternalChanged")]
    [TestCase("PrivateChanged")]
    public static void TryRemove_SubjectStopsReceivingEvent(string eventName)
    {
        var subject = new TestSubject();
        var reference = Events.Get<TestSubject, EventHandler>(eventName);

        var count = 0;

        Assert.That(reference.TryAdd(subject, Handler), Is.True);
        subject.RaiseChanged();

        Assert.That(reference.TryRemove(subject, Handler), Is.True);
        subject.RaiseChanged();

        Assert.That(count, Is.EqualTo(1));
        return;

        void Handler(object? o, EventArgs eventArgs) => count++;
    }

    [TestCase("StaticChanged")]
    [TestCase("Changed")]
    [TestCase("InternalChanged")]
    [TestCase("PrivateChanged")]
    public static void Bind_SubjectStopsReceivingEvent(string eventName)
    {
        var subject = new TestSubject();
        var reference = Events.Get<TestSubject, EventHandler>(eventName);
        var binding = reference.Bind(subject);

        var count = 0;

        binding += Handler;
        subject.RaiseChanged();

        binding -= Handler;
        subject.RaiseChanged();

        Assert.That(count, Is.EqualTo(1));
        return;

        void Handler(object? o, EventArgs eventArgs) => count++;
    }

    [TestCase("StaticChanged")]
    [TestCase("Changed")]
    [TestCase("InternalChanged")]
    [TestCase("PrivateChanged")]
    public static void Subscribe_ReturnsDisposableThatRemovesHandler(string eventName)
    {
        var subject = new TestSubject();
        var reference = Events.Get<TestSubject, EventHandler>(eventName);

        var count = 0;
        using (reference.Subscribe(subject, Handler))
        {
            subject.RaiseChanged();
            Assert.That(count, Is.EqualTo(1));
        }

        subject.RaiseChanged();

        Assert.That(count, Is.EqualTo(1));
        return;

        void Handler(object? o, EventArgs eventArgs) => count++;
    }

    [TestCase("StaticChanged")]
    [TestCase("Changed")]
    [TestCase("InternalChanged")]
    [TestCase("PrivateChanged")]
    public static void Subscribe_CanBeDisposedMultipleTimes(string eventName)
    {
        var subject = new TestSubject();
        var reference = Events.Get<TestSubject, EventHandler>(eventName);

        var subscription = reference.Subscribe(subject, Handler);

        Assert.DoesNotThrow(
            () =>
            {
                subscription.Dispose();
                subscription.Dispose();
            }
        );
        return;

        void Handler(object? o, EventArgs eventArgs) { }
    }

    [TestCase("StaticChanged")]
    [TestCase("Changed")]
    [TestCase("InternalChanged")]
    [TestCase("PrivateChanged")]
    public static void Add_WithTypeCoercion(string eventName)
    {
        var subject = new TestSubject();
        var reference = Events.Get<TestSubject, Action<object?, object>>(eventName);

        var count = 0;

        Assert.That(reference.TryAdd(subject, Handler), Is.True);
        subject.RaiseChanged();

        Assert.That(count, Is.EqualTo(1));
        return;

        void Handler(object? o, object eventArgs) => count++;
    }

    [TestCase("StaticChanged")]
    [TestCase("Changed")]
    [TestCase("InternalChanged")]
    [TestCase("PrivateChanged")]
    public static void Remove_WithTypeCoercion(string eventName)
    {
        var subject = new TestSubject();
        var reference = Events.Get<TestSubject, Action<object?, object>>(eventName);
        var count = 0;

        reference.Add(subject, Handler);
        subject.RaiseChanged();

        // Need to make sure that attempting to remove with a NEW delegate
        // succeeds.
        reference.Remove(subject, Handler);
        subject.RaiseChanged();

        Assert.That(count, Is.EqualTo(1));
        return;

        void Handler(object? sender, object args) => count++;
    }

    [TestCase("StaticChanged")]
    [TestCase("Changed")]
    [TestCase("InternalChanged")]
    [TestCase("PrivateChanged")]
    public static void Subscribe_WithTypeCoercion_PreservesMulticastRemoval(string eventName)
    {
        var subject = new TestSubject();
        var reference = Events.Get<TestSubject, Action<object?, object>>(eventName);

        var calls = new List<int>();
        Action<object?, object> first = (_, _) => calls.Add(1);
        Action<object?, object> second = (_, _) => calls.Add(2);

        // Ensure first is run, then second.  Then remove second and ensure
        // first is run.
        using (reference.Subscribe(subject, first + second))
        {
            subject.RaiseChanged();
            reference.Remove(subject, second);
            subject.RaiseChanged();
        }

        // Remove the remaining first and ensure nothing is run.
        reference.Remove(subject, first);
        subject.RaiseChanged();

        Assert.That(calls, Is.EqualTo(new[] { 1, 2, 1 }));
    }

    private sealed class HiddenSubject
    {
        private sealed class HiddenArgs : EventArgs;

        private event Action<HiddenArgs>? Changed;

        public void RaiseChanged() => Changed?.Invoke(new HiddenArgs());
    }

    [Test]
    public static void Subscribe_WithPrivateParameterType_UsesAccessibleBaseType()
    {
        object subject = new HiddenSubject();
        var info = subject.GetType().GetEvent("Changed", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var reference = EventReference<object, Action<EventArgs>>.Create(info);

        var received = default(EventArgs);
        var count = 0;

        using (reference.Subscribe(
                   subject,
                   args =>
                   {
                       received = args;
                       count++;
                   }
               ))
        {
            ((HiddenSubject)subject).RaiseChanged();
        }

        ((HiddenSubject)subject).RaiseChanged();
        Assert.That(received, Is.Not.Null);
        Assert.That(received!.GetType().IsNestedPrivate, Is.True);
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public static void Create_WithIncompatibleParameterType_Throws()
    {
        Assert.Throws<ArgumentException>(() => Events.Get<TestSubject, Action<object, string>>("Changed"));
    }
}
