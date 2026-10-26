# reflection

> By three methods we may introspect our program: First, by `System.Reflection`, which is noblest; Second, by `GoldMeridian.Reflection`, which is easiest; and third by `System.NullReferenceException: Object reference not set to an instance of an object.`, which is the bitterest. -- Confucius

---

**GoldMeridian.Reflection** provides a handful of extra reflection utilities with the goal of making common operations safer and more elegant.

## Type-Safe Member Selection

`Members` provides APIs for retrieving `System.Reflection` metadata using compiler type-checked references to members.

```cs
MethodInfo method = Members.GetMethod(StaticType.Method);
MethodInfo staticSelector = Members.GetMethod(() => StaticType.Method);
MethodInfo unusedParameter = Members.GetMethod(_ => StaticType.Method);
MethodInfo instanceMethod = Members.GetMethod<InstanceType>(x => x.Method);

FieldInfo staticField = Members.GetField(_ => StaticType.Field);
FieldInfo instanceField = Members.GetField<InstanceType>(x => x.Field);
PropertyInfo property = Members.GetProperty<InstanceType>(x => x.Property);
PropertyInfo indexer = Members.GetProperty<InstanceType>(x => x[0]);
ConstructorInfo constructor = Members.GetConstructor(() => new InstanceType(0));
```

All expressions will be properly inspected without compiling or executing them. The parameter used in instanced cases is also purely symbolic; no instance of `InstanceType` will be created, for example, and no call with `default(InstanceType)` actually occurs. No expressions are actually evaluated.

### Overloads and generics

Specific overloads of methods can be selected with explicit delegate casts or through method-call expressions. The arguments in a method-call selector will be correctly used by the compiler to resolve the method, but, again, they won't be evaluated.

```cs
var bySignature = Members.GetMethod<InstanceType>(x => (Action<int>)x.Overloaded);
var byArguments = Members.GetMethod<InstanceType>(x => x.Overloaded(0));
var generic = Members.GetMethod<InstanceType>(x => x.GenericMethod<string>);
var inferred = Members.GetMethod((InstanceType x) => x.Method);
var constructor = Members.GetConstructor<int>(value => new InstanceType(value));
```

Methods returning both `void` and arbitrary values are supported. Method-group selects also support signatures containing by-`ref` parameters, by-`ref` returns, and `ref struct`s such as `Span<T>`, where a value selector would not work. Constructed generic types keep their concrete generic arguments. Extension methods will return their underlying static method.

## Reified Events

An extensive API for working with events is provided, which is designed to promote them into first-class objects you can work with as you would most .NET concepts.

`EventReference<TTarget, THandler>` represents an event's `add` and `remove` operations. You can obtain one by name with `Events.Get<TTarget, THandler>(name)`.

Due to the nature of reflection, there are times when `TTarget` cannot be written in source code (e.g. inaccessible through access modifiers, a name that can't be written in C#); in such an event (lol), you can use `EventReference<TTarget, THandler>.Create(EventInfo)` with `TTarget` and `THandler` being given less precise types.

```cs
object obj = External.MethodFrom(anotherLibrary);
var info = source.GetType().GetEvent("EventName", bindingFlags)!;
var eventName = EventReference<object, Action<object?, object>>.Create(info);

using var subscription = changed.Subscribe(obj, (sender, args) => DoSomething(args));
```

Instead of manually adding/removing handlers or scoping with `Subscribe`, you can also `Bind` an event to a source object and act on that bound event similarly to a regular event:

```cs
var changed = Events.Get<MySource, EventHandler>("Changed");
var bound = changed.Bind(source);

bound += Handler;
bound -= Handler;
```

You can also avoid necessitating reflection completely for APIs accessible to you by wrapping with an `EventWrapper<THandler>` instead:

```cs
var changed = new EventWrapper<EventHandler>(
    add: handler => source.Changed += handler,
    removed: handler => source.Changed -= handler
);
```

### Strictly-typed event views

Using `EventWrapper<THandler>`, events can be made to have a stronger contract than their delegate signatures normally express. The use case we encountered for this was a UI event declaring a `UIElement` argument, but would always supply `this`. Naturally, consuming code would then benefit from that type being better scoped to `TSelf`,

You can opt in to checked reference-parameter downcasts when exposing events. Use `Events.Wrap` for typed adaptation so equal handlers reuse forwarding delegates and remain removable. Constructing the wrapper directly (as shown above) will only call the callbacks you provide, and cannot repair equality the way `Events.Wrap` does due to the forwarding lambdas provided being fresh.

```cs
public delegate void MouseEvent<in TElement>(UIMouseEvent evt, TEelement element)
    where TElement : UIElement;

extension<TElement>(TElement element)
    where TElement : UIElement
{
    public EventWrapper<MouseEvent<TElement>> OnLeftMouseDownExt
    {
        get => Events.Wrap<UIElement, MouseEvent<TElement>>(element, nameof(UIElement.OnLeftMouseDown), allowParameterDowncasts: true);
        set => _ = value;
    }
}
```

### Delegate forwarding and delegate equality

Previous examples have demonstrated this implicitly. In an effort to support type coercion in delegate arguments, delegates with coercible parameters may be automatically forwarded internally. In such events, equality is preserved, allowing you to treat a reference to the same handler as equal as if no forwarding had to occur.

<!-- TODO: more can be written here, but how much does it matter -->
