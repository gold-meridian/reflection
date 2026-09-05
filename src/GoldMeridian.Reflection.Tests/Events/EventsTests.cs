using System;

namespace GoldMeridian.Reflection.Tests;

[TestFixture]
public static class EventsTests
{
    private sealed class TestSubject
    {
        public delegate void MyCustomDelegate(string a);

        public static event EventHandler StaticPublicDelegate;
        private static event EventHandler StaticPrivateDelegate;
        public static event Action<int> StaticPublicAction;
        private static event Action<int> StaticPrivateAction;
        public static event MyCustomDelegate StaticPublicCustom;
        private static event MyCustomDelegate StaticPrivateCustom;

        public event EventHandler InstancePublicDelegate;
        private event EventHandler InstancePrivateDelegate;
        public event Action<int> InstancePublicAction;
        private event Action<int> InstancePrivateAction;
        public event MyCustomDelegate InstancePublicCustom;
        private event MyCustomDelegate InstancePrivateCustom;
    }

    public delegate void ActionInt(int a);

    [TestCase("StaticPublicDelegate", TypeArgs = [typeof(EventHandler)])]
    [TestCase("StaticPrivateDelegate", TypeArgs = [typeof(EventHandler)])]
    [TestCase("StaticPublicAction", TypeArgs = [typeof(Action<int>)])]
    [TestCase("StaticPrivateAction", TypeArgs = [typeof(Action<int>)])]
    [TestCase("StaticPublicCustom", TypeArgs = [typeof(TestSubject.MyCustomDelegate)])]
    [TestCase("StaticPrivateCustom", TypeArgs = [typeof(TestSubject.MyCustomDelegate)])]
    [TestCase("InstancePublicDelegate", TypeArgs = [typeof(EventHandler)])]
    [TestCase("InstancePrivateDelegate", TypeArgs = [typeof(EventHandler)])]
    [TestCase("InstancePublicAction", TypeArgs = [typeof(Action<int>)])]
    [TestCase("InstancePrivateAction", TypeArgs = [typeof(Action<int>)])]
    [TestCase("InstancePublicCustom", TypeArgs = [typeof(TestSubject.MyCustomDelegate)])]
    [TestCase("InstancePrivateCustom", TypeArgs = [typeof(TestSubject.MyCustomDelegate)])]
    public static void TryGetReturnsTrueWhenPresent<THandler>(string eventName)
        where THandler : Delegate
    {
        Assert.That(Events.TryGet<TestSubject, THandler>(eventName, out _), Is.True);
    }

    [TestCase("StaticPublicAction", TypeArgs = [typeof(ActionInt)])]
    [TestCase("StaticPrivateAction", TypeArgs = [typeof(ActionInt)])]
    [TestCase("StaticPublicCustom", TypeArgs = [typeof(Action<string>)])]
    [TestCase("StaticPrivateCustom", TypeArgs = [typeof(Action<string>)])]
    [TestCase("StaticPublicCustom", TypeArgs = [typeof(Action<object>)])]
    [TestCase("StaticPrivateCustom", TypeArgs = [typeof(Action<object>)])]
    [TestCase("InstancePublicAction", TypeArgs = [typeof(ActionInt)])]
    [TestCase("InstancePrivateAction", TypeArgs = [typeof(ActionInt)])]
    [TestCase("InstancePublicCustom", TypeArgs = [typeof(Action<string>)])]
    [TestCase("InstancePrivateCustom", TypeArgs = [typeof(Action<string>)])]
    [TestCase("InstancePublicCustom", TypeArgs = [typeof(Action<object>)])]
    [TestCase("InstancePrivateCustom", TypeArgs = [typeof(Action<object>)])]
    public static void TryGetReturnsTrueWhenPresentWithTypeCoercion<THandler>(string eventName)
        where THandler : Delegate
    {
        Assert.That(Events.TryGet<TestSubject, THandler>(eventName, out _), Is.True);
    }

    // These test cases are sort of meaningless, but why not lol
    [TestCase("Name1", TypeArgs = [typeof(EventHandler)])]
    [TestCase("Name2", TypeArgs = [typeof(Action<int>)])]
    [TestCase("Name3", TypeArgs = [typeof(TestSubject.MyCustomDelegate)])]
    [TestCase("Name4", TypeArgs = [typeof(Action<string>)])]
    [TestCase("Name5", TypeArgs = [typeof(ActionInt)])]
    public static void TryGetReturnsFalseWhenAbsent<THandler>(string eventName)
        where THandler : Delegate
    {
        Assert.That(Events.TryGet<TestSubject, THandler>(eventName, out _), Is.False);
    }

    [TestCase("StaticPublicAction", TypeArgs = [typeof(Action<object>)])]
    [TestCase("StaticPrivateAction", TypeArgs = [typeof(Action<object>)])]
    [TestCase("StaticPublicCustom", TypeArgs = [typeof(Action<int>)])]
    [TestCase("StaticPrivateCustom", TypeArgs = [typeof(Action<int>)])]
    [TestCase("InstancePublicAction", TypeArgs = [typeof(Action<object>)])]
    [TestCase("InstancePrivateAction", TypeArgs = [typeof(Action<object>)])]
    [TestCase("InstancePublicCustom", TypeArgs = [typeof(Action<int>)])]
    [TestCase("InstancePrivateCustom", TypeArgs = [typeof(Action<int>)])]
    public static void TryGetThrowsWhenPresentWithIncorrectTypeCoercion<THandler>(string eventName)
        where THandler : Delegate
    {
        Assert.Throws<ArgumentException>(() => Events.TryGet<TestSubject, THandler>(eventName, out _));
    }
}
