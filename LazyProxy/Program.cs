using System;
using System.Reflection;
using Microsoft.Practices.Unity;

namespace LazyProxy
{
    class Program
    {
        static void Main(string[] args)
        {
            var container = new UnityContainer();
            container.RegisterType<IFoo, Foo>();
            container.RegisterLazy<IBar, Bar>();

            var foo = container.Resolve<IFoo>();
            foo.Test();
        }

        static void Test(ParameterInfo parameter)
        {
            var attributes = parameter.GetCustomAttributes(false);
            if (attributes != null)
            {
                
            }
        }
    }


    public interface IFoo
    {
        void Test();
    }

    public interface IBar
    {
        string Baz();
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
            Console.WriteLine(_bar.Baz());
        }
    }

    class Bar : IBar
    {
        public Bar(IFoo foo)
        {
        }

        public string Baz()
        {
            return "Hello world";
        }
    }

}
