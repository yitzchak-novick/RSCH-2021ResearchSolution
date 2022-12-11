using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UtilsYN;

namespace DoesIncreasingKIncreaseAvgOrMaxDeg
{
    class Program
    {
        /* YN 2/10/22 - A simple question, when we increase k in RkN (or RVkN, same thing for now), have we gotten a better neighbor than 
         * what we already had? Put a different way, if Cv = 0, should we still take an additional neighbor?
         */

        const int THREADS = 61;
        const int EXPERIMENTS = 400; // Again may want a large value here, we'll see.
        const int SAMPLINGS = 100000; // for each graph, how many time will we sample

        static string DTS => UtilsYN.Utils.DTS;
        static IEnumerable<int> Range(int start, int count) => Enumerable.Range(start, count);
        static IEnumerable<int> Range(int count) => Range(0, count);
        static Random[] rands = TSRandom.ArrayOfRandoms(EXPERIMENTS);
        static Graph[] graphs = new Graph[EXPERIMENTS];
        static int KVALS_TO_TRY = 10; // for how many increases in k will be experiment?

        static long[] totalImprovementsArray_AVG = new long[KVALS_TO_TRY + 1];
        static long[] totalImprovementsArray_MAX = new long[KVALS_TO_TRY + 1];
        static long[] totalTiesArray_AVG = new long[KVALS_TO_TRY + 1];
        static long[] totalTiesArray_MAX = new long[KVALS_TO_TRY + 1];


        static object arrayLock = new object(); // use the same for both, sure there won't be a huge price paid...

        static void Main(string[] args)
        {
            File.WriteAllText("ERresultsfile.txt", "K\tAvg Tied\tAvg Imprv\tMax Tied\tMax Imprv\n");
            File.WriteAllText("BAresultsfile.txt", "K\tAvg Tied\tAvg Imprv\tMax Tied\tMax Imprv\n");

            Console.WriteLine($"Starting program {DTS}");
            if (!File.Exists("C:\\Graphs-7000-5\\graphs000.txt"))
            {
                graphs = Range(0, EXPERIMENTS).AsParallel().Select(i => Graph.NewBaGraph(7000, 5, random: rands[i])).ToArray();
                for (int i = 0; i < graphs.Length; i++)
                {
                    File.WriteAllLines($"C:\\Graphs-7000-5\\graphs{i.ToString("00#")}.txt", graphs[i].Edges.Select(e => e.v1.Id + "\t" + e.v2.Id));
                }
            }
            else
            {
                graphs = new Graph[EXPERIMENTS];
                Parallel.For(0, graphs.Length, new ParallelOptions() { MaxDegreeOfParallelism = THREADS }, i =>
                {
                    graphs[i] = new Graph();
                    File.ReadAllLines($"C:\\Graphs-7000-5\\graphs{i.ToString("00#")}.txt").ToList().ForEach(l => graphs[i].AddEdge(Regex.Split(l, @"\s+")[0], Regex.Split(l, @"\s+")[1]));
                });
            }

            Experiment1("BA");
            Experiment2("BA");


            if (!File.Exists("C:\\ERGraphs-7000-5\\graphs000.txt"))
            {
                graphs = Range(0, EXPERIMENTS).AsParallel().Select(i => Graph.NewErGraphFromBaM(7000, 5, random: rands[i])).ToArray();
                for (int i = 0; i < graphs.Length; i++)
                {
                    File.WriteAllLines($"C:\\ERGraphs-7000-5\\graphs{i.ToString("00#")}.txt", graphs[i].Edges.Select(e => e.v1.Id + "\t" + e.v2.Id));
                }
            }
            else
            {
                graphs = new Graph[EXPERIMENTS];
                Parallel.For(0, graphs.Length, new ParallelOptions() { MaxDegreeOfParallelism = THREADS }, i =>
                {
                    graphs[i] = new Graph();
                    File.ReadAllLines($"C:\\ERGraphs-7000-5\\graphs{i.ToString("00#")}.txt").ToList().ForEach(l => graphs[i].AddEdge(Regex.Split(l, @"\s+")[0], Regex.Split(l, @"\s+")[1]));
                });
            }

            Experiment1("ER");
            Experiment2("ER");

            Console.ReadKey();
        }

        public static void Experiment1(String graphType)
        {
            for (int k = 2; k <= KVALS_TO_TRY; k++)
            {
                Console.WriteLine($"Working on k={k}, {DTS}");
                Parallel.For(0, graphs.Length, new ParallelOptions() { MaxDegreeOfParallelism = THREADS }, thrd =>
                {
                    var g = graphs[thrd];
                    int totalImprovementsAvg = 0, totalImprovementsMax = 0, totalTiesAvg = 0, totalTiesMax = 0;
                    for (int i = 0; i < SAMPLINGS; i++)
                    {
                        var vertex = g.Vertices.ChooseRandomElement(rands[thrd]);
                        if (vertex.Neighbors.Count() >= k)
                        {
                            var neighbors = vertex.Neighbors.ChooseRandomSubset(k, random: rands[thrd]).ToArray();
                            if (neighbors.Last().Degree == neighbors.Take(k - 1).Average(n => n.Degree))
                                totalTiesAvg++;
                            if (neighbors.Last().Degree == neighbors.Take(k - 1).Max(n => n.Degree))
                                totalTiesMax++;
                            if (neighbors.Last().Degree > neighbors.Take(k - 1).Average(n => n.Degree))
                                totalImprovementsAvg++;
                            if (neighbors.Last().Degree > neighbors.Take(k - 1).Max(n => n.Degree))
                                totalImprovementsMax++;
                        }
                    }
                    lock (arrayLock)
                    {
                        totalImprovementsArray_AVG[k] += totalImprovementsAvg;
                        totalTiesArray_AVG[k] += totalTiesAvg;
                        totalImprovementsArray_MAX[k] += totalImprovementsMax;
                        totalTiesArray_MAX[k] += totalTiesMax;
                    }
                }

                );

                var result = $"k={k}\t{totalTiesArray_AVG[k] / (double)(SAMPLINGS * graphs.Length):0.#0}\t" +
                    $"{totalImprovementsArray_AVG[k] / (double)(SAMPLINGS * graphs.Length):0.#0}\t" +
                    $"{totalTiesArray_MAX[k] / (double)(SAMPLINGS * graphs.Length):0.#0}\t" +
                    $"{totalImprovementsArray_MAX[k] / (double)(SAMPLINGS * graphs.Length):0.#0}\n";

                File.AppendAllText($"{graphType}resultsfile.txt", result);
                Console.WriteLine($"{result}  ({DTS})");
            }
        }

        public static void Experiment2(String graphType)
        {
            int[] totalDegress = new int[KVALS_TO_TRY + 1];

            Parallel.For(0, graphs.Length, new ParallelOptions() { MaxDegreeOfParallelism = THREADS }, thrd =>
            {
                for (int i = 0; i < SAMPLINGS; i++)
                {
                    if (thrd == graphs.Length-1 && i % 1000 == 0)
                    {
                        Console.WriteLine($"Thread {thrd} up to iteration {i}, {DTS}");
                    }
                    var vertex = graphs[thrd].Vertices.ChooseRandomElement(rands[thrd]);
                    var allNeighbors = new HashSet<Vertex>(vertex.Neighbors);
                    for (int j = 0; j < Math.Min(KVALS_TO_TRY, allNeighbors.Count); j++)
                    {
                        var neighbor = allNeighbors.ChooseRandomElement(rands[thrd]);
                        allNeighbors.Remove(neighbor);
                        Interlocked.Add(ref totalDegress[j], neighbor.Degree);
                    }
                }
            });

            File.WriteAllText($"{graphType}AvgDegreeOfEachNeighbor.txt", String.Join(", ", totalDegress.Select(i => (i / (double)(SAMPLINGS*graphs.Length)).ToString("0.####0"))));
            Console.WriteLine(File.ReadAllText($"{graphType}AvgDegreeOfEachNeighbor.txt"));
        }
    }
}
