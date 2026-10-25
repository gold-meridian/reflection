using System;
using System.Linq;

namespace GoldMeridian.Reflection.Tests;

[TestFixture]
public static class MethodOverloadTests
{
    private delegate void RefHandler(ref int value);

    private sealed class Subject
    {
        public int Select(int value)
        {
            throw new InvalidOperationException("Method executed.");
        }

        public int Select(string value)
        {
            throw new InvalidOperationException("Method executed.");
        }

        public int Select(int first, int second)
        {
            throw new InvalidOperationException("Method executed.");
        }

        public int Select<T>(T value)
        {
            throw new InvalidOperationException("Method executed.");
        }

        public static void StaticSelect(int value)
        {
            throw new InvalidOperationException("Method executed.");
        }

        public static void StaticSelect(string value)
        {
            throw new InvalidOperationException("Method executed.");
        }

        public void ByReference(int value)
        {
            throw new InvalidOperationException("Method executed.");
        }

        public void ByReference(ref int value)
        {
            throw new InvalidOperationException("Method executed.");
        }
    }

    [Test]
    public static void StaticOverloads_SelectParameterTypesUsingDelegateCastsAndCallArguments()
    {
        var intOverload = typeof(Subject).GetMethod(nameof(Subject.StaticSelect), [typeof(int)]);
        var stringOverload = typeof(Subject).GetMethod(nameof(Subject.StaticSelect), [typeof(string)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetMethod((Action<int>)Subject.StaticSelect), Is.EqualTo(intOverload));
            Assert.That(Members.GetMethod((Action<string>)Subject.StaticSelect), Is.EqualTo(stringOverload));
            Assert.That(Members.GetMethod(() => (Action<int>)Subject.StaticSelect), Is.EqualTo(intOverload));
            Assert.That(Members.GetMethod(() => (Action<string>)Subject.StaticSelect), Is.EqualTo(stringOverload));
            Assert.That(Members.GetMethod(_ => (Action<int>)Subject.StaticSelect), Is.EqualTo(intOverload));
            Assert.That(Members.GetMethod(_ => (Action<string>)Subject.StaticSelect), Is.EqualTo(stringOverload));
            Assert.That(Members.GetMethod(() => Subject.StaticSelect(0)), Is.EqualTo(intOverload));
            Assert.That(Members.GetMethod(() => Subject.StaticSelect("")), Is.EqualTo(stringOverload));
            Assert.That(Members.GetMethod(_ => Subject.StaticSelect(0)), Is.EqualTo(intOverload));
            Assert.That(Members.GetMethod(_ => Subject.StaticSelect("")), Is.EqualTo(stringOverload));
        }
    }

    [Test]
    public static void InstanceOverloads_WithIdenticalReturnTypes_SelectTheRequestedParameterType()
    {
        var subject = new Subject();
        var intOverload = typeof(Subject).GetMethod(nameof(Subject.Select), [typeof(int)]);
        var stringOverload = typeof(Subject).GetMethod(nameof(Subject.Select), [typeof(string)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetMethod((Func<int, int>)subject.Select), Is.EqualTo(intOverload));
            Assert.That(Members.GetMethod((Func<string, int>)subject.Select), Is.EqualTo(stringOverload));
            Assert.That(Members.GetMethod<Subject>(x => (Func<int, int>)x.Select), Is.EqualTo(intOverload));
            Assert.That(Members.GetMethod<Subject>(x => (Func<string, int>)x.Select), Is.EqualTo(stringOverload));
            Assert.That(Members.GetMethod<Subject>(x => x.Select(0)), Is.EqualTo(intOverload));
            Assert.That(Members.GetMethod<Subject>(x => x.Select("")), Is.EqualTo(stringOverload));
        }
    }

    [Test]
    public static void Overloads_SelectDifferentParameterCounts()
    {
        var single = typeof(Subject).GetMethod(nameof(Subject.Select), [typeof(int)]);
        var pair = typeof(Subject).GetMethod(nameof(Subject.Select), [typeof(int), typeof(int)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetMethod<Subject>(x => (Func<int, int>)x.Select), Is.EqualTo(single));
            Assert.That(Members.GetMethod<Subject>(x => (Func<int, int, int>)x.Select), Is.EqualTo(pair));
            Assert.That(Members.GetMethod<Subject>(x => x.Select(0)), Is.EqualTo(single));
            Assert.That(Members.GetMethod<Subject>(x => x.Select(0, 0)), Is.EqualTo(pair));
        }
    }

    [Test]
    public static void GenericAndNonGenericOverloads_WithTheSameConstructedSignature_RemainDistinct()
    {
        var nonGeneric = typeof(Subject).GetMethod(nameof(Subject.Select), [typeof(int)]);
        var generic = typeof(Subject).GetMethods()
                                     .Single(method => method is { Name: nameof(Subject.Select), IsGenericMethodDefinition: true })
                                     .MakeGenericMethod(typeof(int));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetMethod<Subject>(x => (Func<int, int>)x.Select), Is.EqualTo(nonGeneric));
            Assert.That(Members.GetMethod<Subject>(x => x.Select<int>), Is.EqualTo(generic));
            Assert.That(Members.GetMethod<Subject>(x => x.Select(0)), Is.EqualTo(nonGeneric));
            Assert.That(Members.GetMethod<Subject>(x => x.Select<int>(0)), Is.EqualTo(generic));
            Assert.That(Members.GetMethod(new Subject().Select<int>), Is.EqualTo(generic));
        }
    }

    [Test]
    public static void ValueAndRefOverloads_SelectTheRequestedParameterPassingMode()
    {
        var byValue = typeof(Subject).GetMethod(nameof(Subject.ByReference), [typeof(int)]);
        var byReference = typeof(Subject).GetMethod(nameof(Subject.ByReference), [typeof(int).MakeByRefType()]);
        var argument = 0;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Members.GetMethod<Subject>(x => (Action<int>)x.ByReference), Is.EqualTo(byValue));
            Assert.That(Members.GetMethod<Subject>(x => (RefHandler)x.ByReference), Is.EqualTo(byReference));
            Assert.That(Members.GetMethod<Subject>(x => x.ByReference(argument)), Is.EqualTo(byValue));
            Assert.That(Members.GetMethod<Subject>(x => x.ByReference(ref argument)), Is.EqualTo(byReference));
        }
    }
}
