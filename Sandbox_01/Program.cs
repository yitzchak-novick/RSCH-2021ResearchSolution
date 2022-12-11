using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UtilsYN;

namespace Sandbox_01
{
    class Program
    {
        const int N = 7000; // Size of the graph. We can test with small values, but if the program runs fast enough we'll want data for a large N
        const int M = 5; // Same remark, may raise to 5 if N raises
        const int THREADS = 27;
        const int EXPERIMENTS = 400; // Again may want a large value here, we'll see.
        readonly decimal[] ALL_DESIRED_PCTS = new[] { .75m, .85m, .95m }; // think ideally .7, .75, .8, .85, .9, .95, 1
        const double PARTITION = .2; // the top 20% will be considered "hubs", we can make this adjustable if we want

        static string DTS => UtilsYN.Utils.DTS;
        static IEnumerable<int> Range(int start, int count) => Enumerable.Range(start, count);
        static IEnumerable<int> Range(int count) => Range(0, count);
        static Random[] rands = TSRandom.ArrayOfRandoms(EXPERIMENTS);
        static Graph[] graphs = new Graph[EXPERIMENTS];
        // Just dump in some code
        static void Main(string[] args)
        {
            Console.WriteLine($"Starting program {DTS}");
            if (!File.Exists("C:\\Graphs\\graphs000.txt"))
            {
                graphs = Range(0, EXPERIMENTS).AsParallel().Select(i => Graph.NewBaGraph(N, M, random: rands[i])).ToArray();
                for (int i = 0; i < graphs.Length; i++)
                {
                    File.WriteAllLines($"C:\\Graphs\\graphs{i.ToString("00#")}.txt", graphs[i].Edges.Select(e => e.v1.Id + "\t" + e.v2.Id));
                }
            }
            else
            {
                graphs = new Graph[EXPERIMENTS];
                for (int i = 0; i < graphs.Length; i++)
                {
                    graphs[i] = new Graph();
                    File.ReadAllLines($"C:\\Graphs\\graphs{i.ToString("00#")}.txt").ToList().ForEach(l => graphs[i].AddEdge(Regex.Split(l, @"\s+")[0], Regex.Split(l, @"\s+")[1]));
                }
            }

            // TEMP CODE, TAKE THIS OUT:
            foreach (var g in graphs)
            {
                Console.WriteLine(g.Vertices.Average(v => v.Degree) + " " + g.Vertices.Max(v => v.Degree) + " " + g.Vertices.Count());
            }
            Console.ReadKey();

            Console.WriteLine($"Finished generating graphs {DTS}");
        }
    }
}
