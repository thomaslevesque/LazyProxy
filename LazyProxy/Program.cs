using System;
using Microsoft.Practices.Unity;

namespace LazyProxy
{
    class Program
    {
        static void Main()
        {
            var container = new UnityContainer();
            container.RegisterType<IFoo, Foo>();
            container.RegisterLazy<IBar, Bar>();

            var foo = container.Resolve<IFoo>();
            foo.Test();
            Console.ReadLine();
        }
    }


    public interface IFoo
    {
        void Test();
    }

    public interface IBar
    {
        string Baz();
        int X { get; set; }
    }

    class Foo : IFoo
    {
        private readonly IBar _bar;
        public Foo(IBar bar)
        {
            _bar = bar;
        }

        public void Test()
        {
            Console.WriteLine("Baz(): " + _bar.Baz());
            Console.WriteLine("X:" + _bar.X);
            Console.WriteLine("Setting X to 123");
            _bar.X = 123;
        }
    }

    class Bar : IBar
    {
        // ReSharper disable once UnusedParameter.Local
        public Bar(IFoo foo)
        {
        }

        public string Baz()
        {
            return "Hello world";
        }

        public int X
        {
            get
            {
                return 42;
            }
            set
            {
                if (value == 42)
                    Console.WriteLine("OK!");
                else
                    Console.WriteLine("No, the answer is 42");
            }
        }
    }

}
