# LazyProxy

Dynamic lazy proxy to allow injection of circular dependencies, either with or without an IoC container.

*Note: at this point, this library is just a POC, not production ready code. Only the most basic cases are covered. Things like events, generic methods, and ref/out parameters are not supported.*

Assume you have two interfaces like this:

    public interface IFoo
    {
        void Test();
        int X { get; }
    }
    
    public interface IBar
    {
        string Baz();
    }

And the implementation of each interface depends on the other interface:

    class Foo : IFoo
    {
        private readonly IBar _bar;
        public Foo(IBar bar)
        {
            _bar = bar;
        }
        
        public void Test() => Console.WriteLine(_bar.Baz());
        public int X => 42;
    }
    
    class Bar : IBar
    {
        private IFoo _foo;
        public Bar(IFoo foo)
        {
            _foo = foo;
        }
    
        public string Baz() => $"The answer is {_foo.X}";
    }

We have a circular dependency: `Foo` depends on `IBar`, which is implemented by `Bar`, which depends on `IFoo`,
which is implemented by `Foo`. In theory, there is no way to perform this injection, since we need `Foo` to create
`Bar`, and vice versa... it's a chicken and egg problem. But there's a trick that would let us do it anyway: we just
need to resolve either `Foo` or `Bar` lazily. Instead of injecting `Bar` directly as the implementation of `IBar`, we could
inject a proxy class like this:

    class IBar_LazyProxy : IBar
    {
        private Lazy<Bar> _lazy;
        public IBar_LazyProxy(Lazy<Bar> lazy)
        {
            _lazy = lazy;
        }
    
        public string Baz() => _lazy.Value.Baz();
    }
    
This class just delegates the implementation to a lazily resolved instance of `Bar`. Since it doesn't depend on `IFoo`,
there is no circular dependency. Only the first time `Baz()` is called, the actual instance of `Bar` will be resolved,
and the resolution will work because `Foo` will already have been resolved.

But writing such a proxy class manually is tedious; so this library generates it dynamically at runtime. It provides
extension methods to register lazy types in a Unity container. In the example above, the container could be configured
like this:

    var container = new UnityContainer();
    container.RegisterType<IFoo, Foo>();
    container.RegisterLazy<IBar, Bar>();

    var foo = container.Resolve<IFoo>();
    foo.Test();
