using System;
using System.Collections.Generic;
using System.Reflection;

namespace GoldMeridian.Reflection.Tests;

[TestFixture]
public static class EventWrapperTests
{
    private sealed class Subject
    {
        public event Action Changed
        {
            add
            {
                Adds++;
                handlers += value;
            }

            remove
            {
                Removes++;
                handlers -= value;
            }
        }

        public EventWrapper<Action> Wrapped
        {
            get => new(handler => Changed += handler, handler => Changed -= handler);
            set { }
        }

        public int Adds;
        public int Removes;

        private Action? handlers;

        public void Raise()
        {
            handlers?.Invoke();
        }
    }

    [Test]
    public static void CompoundAssignment_OnLocal_AddsAndRemovesExactlyOnce()
    {
        var subject = new Subject();
        var wrapper = subject.Wrapped;
        var calls = 0;

        wrapper += Handler;
        subject.Raise();

        wrapper -= Handler;
        subject.Raise();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(subject.Adds, Is.EqualTo(1));
            Assert.That(subject.Removes, Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(1));
        }

        return;

        void Handler()
        {
            calls++;
        }
    }

    [Test]
    public static void CompoundAssignment_OnProperty_SupportsMulticastAndFreshGetters()
    {
        var subject = new Subject();
        var calls = new List<int>();
        var first = () => calls.Add(1);
        var second = () => calls.Add(2);

        subject.Wrapped += first + second;
        subject.Raise();

        subject.Wrapped -= second;
        subject.Raise();

        subject.Wrapped -= first;
        subject.Raise();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(calls, Is.EqualTo(new[] { 1, 2, 1 }));
            Assert.That(subject.Adds, Is.EqualTo(1));
            Assert.That(subject.Removes, Is.EqualTo(2));
        }
    }

    [Test]
    public static void BinaryOperators_ReturnUsableWrapperCopies()
    {
        var subject = new Subject();
        var handler = () => { };

        var added = subject.Wrapped + handler;
        var removed = added - handler;
        removed += handler;
        removed -= handler;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(subject.Adds, Is.EqualTo(2));
            Assert.That(subject.Removes, Is.EqualTo(2));
        }
    }

    [Test]
    public static void DirectAccessors_PreserveNullAndExceptions()
    {
        var exception = new InvalidOperationException("accessor");
        var sawNull = false;
        var wrapper = new EventWrapper<Action>(
            handler => sawNull = handler is null,
            _ => throw exception
        );

        wrapper += null;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sawNull, Is.True);
            Assert.That(Assert.Throws<InvalidOperationException>(() => wrapper -= null), Is.SameAs(exception));
        }
    }

    [Test]
    public static void DefaultAndInvalidConstruction_FailClearly()
    {
        Assert.Throws<ArgumentNullException>(() => _ = new EventWrapper<Action>(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => _ = new EventWrapper<Action>(_ => { }, null!));

        var wrapper = default(EventWrapper<Action>);
        Assert.Throws<InvalidOperationException>(() => wrapper += () => { });
        Assert.Throws<InvalidOperationException>(() => wrapper -= () => { });
    }

    [Test]
    public static void ReflectedWrapper_UsesExistingReferenceAndValidatesTarget()
    {
        var subject = new Subject();
        var reference = Events.Get<Subject, Action>(nameof(Subject.Changed));
        var wrapper = reference.Wrap(subject);
        var handler = () => { };

        wrapper += handler;
        Events.Wrap<Subject, Action>(subject, nameof(Subject.Changed)).Remove(handler);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(subject.Adds, Is.EqualTo(1));
            Assert.That(subject.Removes, Is.EqualTo(1));
        }

        Assert.Throws<TargetException>(() => reference.Wrap(null));
    }

    private struct StaticSubject
    {
        public static event Action? Changed;

        public static void Raise()
        {
            Changed?.Invoke();
        }
    }

    [Test]
    public static void StaticWrapper_IgnoresValueTypeTargetWithoutUnboxingNull()
    {
        var wrapper = Events.Wrap<StaticSubject, Action>(default(StaticSubject), nameof(StaticSubject.Changed));
        var calls = 0;
        Action handler = () => calls++;

        wrapper += handler;
        StaticSubject.Raise();

        wrapper -= handler;
        StaticSubject.Raise();

        Assert.That(calls, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RepeatedConstructionAndOperators_DoNotAllocateWithReusedCallbacks(bool reflected)
    {
        var subject = new Subject();
        Action<Action?> add = handler => subject.Changed += handler;
        Action<Action?> remove = handler => subject.Changed -= handler;
        var handler = () => { };

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
            Assert.That(subject.Adds, Is.EqualTo(20_000));
            Assert.That(subject.Removes, Is.EqualTo(20_000));
        }

        return;

        void Cycle()
        {
            var wrapper = reflected
                ? Events.Wrap<Subject, Action>(subject, nameof(Subject.Changed))
                : new EventWrapper<Action>(add, remove);
            wrapper += handler;
            wrapper -= handler;
        }
    }
}
