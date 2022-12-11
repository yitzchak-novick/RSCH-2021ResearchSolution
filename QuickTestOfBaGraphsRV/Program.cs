using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickTestOfBaGraphsRV
{
    class Program
    {
        /* YN 2/14/20 - This is just a quick and dirty test to see if we can select a desired RV for 
         * both BA and ER graphs.
         */

        static void Main(string[] args)
        {
            File.WriteAllText("barvs.txt", "");
            foreach (var n in new[] { 25, 50, 100, 250, 1000, 2000, 5000 })
                foreach (var m in new[] { 2, 3, 5, 10, 20, 50, 100, 200 })
                {
                    if (m >= n) continue;
                    var g = Graph.NewBaGraph(n, m);
                    File.AppendAllText("barvs.txt", $"N: {n}, M: {m}, RV: {g.RV}\n");
                }
            Console.WriteLine("Done");
            Console.ReadKey();
        }
    }
}
