using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestAssemblyDependency
{
    public class Class1
    {

        public Class1(string logmessge)
        {
            if (logmessge != null)
                Console.WriteLine(logmessge);
        }

        public void DoSomething()
        {
            Console.WriteLine("do something");
        }

    }
}
