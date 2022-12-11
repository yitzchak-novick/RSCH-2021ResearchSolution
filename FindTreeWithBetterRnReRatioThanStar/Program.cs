using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilsYN;

namespace FindTreeWithBetterRnReRatioThanStar
{
    class Program
    {
        /* YN 2/4/22 - This should be a very simple program. We conjecture that a start of n vertices maximizes RN/RE over all trees of n vertices.
         * We will just generate random trees and see if any produce a better ratio.
         */

        static string DTS => UtilsYN.Utils.DTS;
        static IEnumerable<int> Range(int start, int count) => Enumerable.Range(start, count);
        static IEnumerable<int> Range(int count) => Range(0, count);
        static Random[] rands = TSRandom.ArrayOfRandoms(70); 
        static Graph getRandomTree(int n, Random rand)
        {
            List<Tuple<int, int>> list = new List<Tuple<int, int>>();
            list.Add(new Tuple<int, int>(1, 2));

            for (int i = 3; i <= n; i++)
            {
                var prevVertexId = rand.Next(1, i);
                list.Add(new Tuple<int, int>(i, prevVertexId));
            }

            Graph graph = new Graph();
            foreach (var edge in list)
                graph.AddEdge(edge.Item1, edge.Item2);

            return graph;

        }

        static void Main(string[] args)
        {
            TryTrees();
        }

        static void TryTrees()
        {
            DateTime lastTime = DateTime.Now;
            Console.WriteLine($"Searching for tree better than star, starting {DTS}");
            while (true)
            {
                var numThreads = Convert.ToInt32(File.ReadAllText("threads.txt")); // configure number of threads from outside, depending on other resource needs
                 
                if (numThreads == 0)
                {
                    Thread.Sleep(TimeSpan.FromMinutes(30)); // assuming something important is going on, take a long break..
                    continue;
                }

                bool[] completed = new bool[numThreads]; // Go again until every thread has completed at least one job

                int completedInThisRound = 0;

                Parallel.For(0, numThreads, i =>
                {
                    do
                    {
                        var size = rands[i].Next(9, 1000001);
                        var graph = getRandomTree(size, rands[i]);
                        var calculatedRatio = (2m * (size - 1) * (size - 1) + 2) / ((decimal)size * size);
                        var actualRatio = graph.Vertices.Average(v => v.Neighbors.Average(ng => (decimal)ng.Degree)) / graph.Edges.Average(e => (e.v1.Degree + e.v2.Degree) / 2m);
                        if (actualRatio >= calculatedRatio)
                        {
                            Console.WriteLine($"FOUND ONE {DTS}, {calculatedRatio}, {actualRatio}");
                            File.WriteAllLines($"CounterExample.txt{00}.txt", graph.Edges.Select(e => e.v1.Id + "\t" + e.v2.Id));
                            Console.ReadKey();
                        }
                        completed[i] = true;
                        Interlocked.Increment(ref completedInThisRound);
                    } while (completed.Any(b => !b));
                });
                //var lastCompleted = Convert.ToInt64(File.ReadAllText("completed.txt"));PUT BACK
                var lastCompleted = Convert.ToInt64(File.ReadAllLines("completed.txt").Last().Split(',')[0]); // TAKE OUT

                lastCompleted += completedInThisRound;
                //File.WriteAllText("completed.txt", "" + lastCompleted); PUT BACK
                File.AppendAllText("completed.txt", $"{lastCompleted}, Threads: {numThreads}, {DTS}:{DateTime.Now.ToString("ss")}\n"); // TAKE OUT
                if (DateTime.Now.Subtract(lastTime).TotalDays >= 1)
                {
                    Console.WriteLine($"Searching for tree better than star, Finished {lastCompleted} experiments so far {DTS}");
                    lastTime = DateTime.Now;
                }
            }
            Console.WriteLine($"Finished {DTS}");
            Console.ReadKey();
        }
        
        static void VerifyStarRatio()
        {
            for (int n = 2; n < 100; n++)
            {
                decimal calculatedRatio = (2m*(n-1)*(n-1)+2) / ((decimal)n*n);
                Graph graph = new Graph();
                graph.AddVertex(1);
                for (int i = 2; i <= n; i++)
                    graph.AddEdge(1, i);
                decimal actualRatio = graph.Vertices.Average(v => v.Neighbors.Average(ng => (decimal)ng.Degree)) / graph.Edges.Average(e => (e.v1.Degree + e.v2.Degree) / 2m);
                Console.WriteLine($"{calculatedRatio}\t{actualRatio}");
                Console.ReadKey();

            }
        }
    }
}
