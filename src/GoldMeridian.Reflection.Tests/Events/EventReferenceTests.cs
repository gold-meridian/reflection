using System;

namespace GoldMeridian.Reflection.Tests;

[TestFixture]
public static class EventReferenceTests
{
    private sealed class TestSubject
    {
        public static event EventHandler? StaticChanged;

        public event EventHandler? Changed;

        internal event Action<int>? InternalChanged;

        private event EventHandler? PrivateChanged;

        public int ChangeCount { get; private set; }

        public void RaiseChanged()
        {
            ChangeCount++;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseInternal(int value)
        {
            InternalChanged?.Invoke(value);
        }

        public void RaisePrivate()
        {
            PrivateChanged?.Invoke(this, EventArgs.Empty);
        }

        public static void RaiseStatic()
        {
            StaticChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void SetUp()
        {
            StaticChanged = null;
        }
    }

    [SetUp]
    public static void SetUp()
    {
        TestSubject.SetUp();
    }

    [Test]
    public static void Create_ExposesEventMetadata()
    {
        var info = typeof(TestSubject).GetEvent(nameof(TestSubject.Changed))!;
        var reference = EventReference<TestSubject, EventHandler>.Create(info);

        Assert.Multiple(
            () =>
            {
                Assert.That(reference.Info, Is.SameAs(info));
                Assert.That(reference.HandlerType, Is.EqualTo(typeof(EventHandler)));
            }
        );
    }

    [Test]
    public static void Add_SubjectReceivesEvent()
    {
        var subject = new TestSubject();
        var reference = Events.Get<TestSubject, EventHandler>(nameof(TestSubject.Changed));

        var count = 0;

        reference.Add(subject, Handler);
        subject.RaiseChanged();

        Assert.That(count, Is.EqualTo(1));
        return;

        void Handler(object? o, EventArgs eventArgs) => count++;
    }

    [Test]
    public static void Remove_SubjectStopsReceivingEvent()
    {
        var subject = new TestSubject();
        var reference = Events.Get<TestSubject, EventHandler>(nameof(TestSubject.Changed));

        var count = 0;

        reference.Add(subject, Handler);
        reference.Remove(subject, Handler);

        subject.RaiseChanged();

        Assert.That(count, Is.Zero);
        return;

        void Handler(object? o, EventArgs eventArgs) => count++;
    }

    [Test]
    public static void Subscribe_ReturnsDisposableThatRemovesHandler()
    {
        var subject = new TestSubject();
        var reference = Events.Get<TestSubject, EventHandler>(nameof(TestSubject.Changed));

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

    [Test]
    public static void Subscribe_CanBeDisposedMultipleTimes()
    {
        var subject = new TestSubject();
        var reference = Events.Get<TestSubject, EventHandler>(nameof(TestSubject.Changed));

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

    [Test]
    public static void CanAccessNonPublicEvent()
    {
        var subject = new TestSubject();
        var reference = Events.Get<TestSubject, EventHandler>("PrivateChanged");

        var count = 0;

        reference.Add(subject, Handler);
        subject.RaisePrivate();

        Assert.That(count, Is.EqualTo(1));
        return;

        void Handler(object? o, EventArgs eventArgs) => count++;
    }

    [Test]
    public static void CanAccessNonPublicHandlerType()
    {
        var reference = Events.Get<TestSubject, Action<int>>(nameof(TestSubject.InternalChanged));

        Assert.That(reference.HandlerType, Is.EqualTo(typeof(Action<int>)));
    }

    [Test]
    public static void StaticEvent_CanBeSubscribed()
    {
        var reference = Events.Get<TestSubject, EventHandler>(nameof(TestSubject.StaticChanged));

        var count = 0;
        reference.Add(null!, Handler);

        TestSubject.RaiseStatic();

        reference.Remove(null!, Handler);

        Assert.That(count, Is.EqualTo(1));
        return;

        void Handler(object? o, EventArgs eventArgs) => count++;
    }
}
