using System;

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
}
