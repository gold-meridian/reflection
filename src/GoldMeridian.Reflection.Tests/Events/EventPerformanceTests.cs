using System;
using System.Diagnostics;

namespace GoldMeridian.Reflection.Tests;

// TODO: These are mostly regression tests for performance rather than "real"
//       benchmarks; it might be worthwhile to set up proper benchmarks as well.

[TestFixture]
public static class EventPerformanceTests
{
    private sealed class Subject
    {
        public static int StaticChanges;
        public int Changes;

        public static event Action<string> StaticChanged
        {
            add => StaticChanges++;
            remove => StaticChanges--;
        }

        public event EventHandler Invariant
        {
            add => Changes++;
            remove => Changes--;
        }

        public event Action<string> Changed
        {
            add => Changes++;
            remove => Changes--;
        }
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void RepeatedAddRemove_AfterWarmup_DoesNotAllocate(bool isStatic, bool coerce)
    {
        var subject = new Subject();
        var name = isStatic ? nameof(Subject.StaticChanged) : nameof(Subject.Changed);

        Action operation;
        if (coerce)
        {
            var reference = Events.Get<Subject, Action<object>>(name);
            Action<object> handler = _ => { };
            operation = () =>
            {
                reference.Add(subject, handler);
                reference.Remove(subject, handler);
            };
        }
        else
        {
            var reference = Events.Get<Subject, Action<string>>(name);
            Action<string> handler = _ => { };
            operation = () =>
            {
                reference.Add(subject, handler);
                reference.Remove(subject, handler);
            };
        }

        // warmup
        for (var i = 0; i < 10_000; i++)
        {
            operation();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            operation();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(allocated, Is.Zero);
            Assert.That(subject.Changes, Is.Zero);
            Assert.That(Subject.StaticChanges, Is.Zero);
        }
    }

    // TODO: No actual assertions, mostly diagnostics (can we at least assert
    //       that we're faster than MethodInfo.Invoke?).
    [Test, Explicit("Only run in release")]
    public static void CompareAccessorCosts()
    {
        var subject = new Subject();

        EventHandler invariantHandler = (_, _) => { };
        Action<string> exactHandler = _ => { };
        Action<object> broadHandler = _ => { };

        var exact = Events.Get<Subject, Action<string>>(nameof(Subject.Changed));
        var invariant = Events.Get<Subject, EventHandler>(nameof(Subject.Invariant));
        var coerced = Events.Get<Subject, Action<object>>(nameof(Subject.Changed));
        var staticReference = Events.Get<Subject, Action<string>>(nameof(Subject.StaticChanged));
        var info = typeof(Subject).GetEvent(nameof(Subject.Changed))!;

        Measure(
            "Direct C#",
            () =>
            {
                subject.Changed += exactHandler;
                subject.Changed -= exactHandler;
            }
        );

        Measure(
            "Exact invariant instance",
            () =>
            {
                invariant.Add(subject, invariantHandler);
                invariant.Remove(subject, invariantHandler);
            }
        );

        Measure(
            "Exact instance",
            () =>
            {
                exact.Add(subject, exactHandler);
                exact.Remove(subject, exactHandler);
            }
        );

        Measure(
            "Coerced instance (cached handler)",
            () =>
            {
                coerced.Add(subject, broadHandler);
                coerced.Remove(subject, broadHandler);
            }
        );

        Measure(
            "Exact static",
            () =>
            {
                staticReference.Add(null, exactHandler);
                staticReference.Remove(null, exactHandler);
            }
        );

        Measure(
            "MethodInfo.Invoke",
            () =>
            {
                info.AddMethod!.Invoke(subject, [exactHandler]);
                info.RemoveMethod!.Invoke(subject, [exactHandler]);
            }
        );
    }

    private static void Measure(string name, Action operation)
    {
        const int iterations = 1_000_000;

        // warmup
        for (var i = 0; i < 20_000; i++)
        {
            operation();
        }

        var best = double.MaxValue;
        long allocated = 0;
        for (var round = 0; round < 3; round++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            for (var i = 0; i < iterations; i++)
            {
                operation();
            }

            var elapsed = Stopwatch.GetElapsedTime(start).TotalNanoseconds / iterations;
            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            best = Math.Min(best, elapsed);
        }

        TestContext.Out.WriteLine($"{name}: {best:F1} ns/add-remove pair; {(double)allocated / iterations:F1} B/pair");
    }
}
