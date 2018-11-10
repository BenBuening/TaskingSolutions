using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TaskingSolutions.Module
{
    class Program
    {
        public static void Main(string[] args)
        {
            string rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string workFolder = Path.Combine(rootPath, "WorkBinaries");
            if (!Directory.Exists(workFolder)) Directory.CreateDirectory(workFolder);

            Runner g = new Runner();
            g.Start(workFolder);
        }
    }
}
