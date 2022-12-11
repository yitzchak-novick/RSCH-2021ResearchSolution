using GraphLibYN_2019;
using UtilsYN;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

namespace MaxForBudget_01
{
    class Program
    {
        // YN 2/12/21 - Still struggling with a large mental block, so this isn't necessarily a meaningful experiment, partially
        // just to try and move things along a little..
        // We will consider three sampling methods, RV, RN, and RV+RN where we keep both vertices.
        // We will define three costs, Cv - Cost of "examining" a vertex, Cn - Cost of "examining" a neighbor, and 
        // Cs - Cost of "selecting" a vertex (or neighbor). To start we will assume all 3 are equal to 1, and we will
        // check, for a given budget, whats the max we find for each method. Then maybe we will change the cost model.

        static readonly decimal CV = 1;
        static readonly decimal CN = 1;
        static readonly decimal CS = 1;

        static readonly int THREADS = 30; // This many simultaneous experiments at a time, we will take the average over this many graphs as our result

        static readonly int N = 10000;
        static readonly int MU = 40;



        static string DTS => UtilsYN.Utils.DTS;

        private enum SamplingMethod
        {
            RV,
            RN,
            RV_RN
        }

        static decimal CostPerSampling(SamplingMethod method)
        {
            decimal cost = CV + CS; // In all cases you pay for the first vertex, and the vertex you keep
            if (method == SamplingMethod.RN)
                cost += CN; // add the cost of sampling the neighbor
            if (method == SamplingMethod.RV_RN)
                cost += CN + CS; // add the cost of sampling the neighbor, and the extra cost of selecting a second vertex
            return cost;
        }

        static void Main(string[] args)
        {
            StringBuilder results = new StringBuilder();
            String fileName = "results.tsv";
            File.WriteAllText(fileName, "");
            var rands = TSRandom.ArrayOfRandoms(THREADS);
            var graphs = new Graph[30];
            Parallel.For(0, THREADS, i => graphs[i] = Graph.NewBaGraph(10000, 20, random:rands[i]));
            Console.WriteLine($"Finished generating graphs {DTS}");
            List<double> RVAvgMax = new List<double>();
            List<double> RNAvgMax = new List<double>();
            List<double> RVRNAvgMax = new List<double>();
            File.AppendAllText(fileName, "RV");
            for (int b = 12; b <= 10000; b += 12)
            {
                int AllMaxes = 0;
                Parallel.For(0, THREADS, i =>
                {
                    var graph = graphs[i];
                    var rand = rands[i];

                    var currMax = GetMaxDegreeForBudget(graph, SamplingMethod.RV, b, rand);
                    Interlocked.Add(ref AllMaxes, currMax);

                });
                RVAvgMax.Add((double)AllMaxes / THREADS);
                File.AppendAllText(fileName, "\t" + (double)AllMaxes / THREADS);
                Console.WriteLine($"Finished RV for b={b}, {DTS}");
            }
            File.AppendAllText(fileName, "\nRN");
            for (int b = 12; b <= 10000; b += 12)
            {
                int AllMaxes = 0;
                Parallel.For(0, THREADS, i =>
                {
                    var graph = graphs[i];
                    var rand = rands[i];

                    var currMax = GetMaxDegreeForBudget(graph, SamplingMethod.RN, b, rand);
                    Interlocked.Add(ref AllMaxes, currMax);

                });
                RNAvgMax.Add((double)AllMaxes / THREADS);
                File.AppendAllText(fileName, "\t" + (double)AllMaxes / THREADS);
                Console.WriteLine($"Finished RN for b={b}, {DTS}");
            }
            File.AppendAllText(fileName, "\nRVRN");
            for (int b = 12; b <= 10000; b += 12)
            {
                int AllMaxes = 0;
                Parallel.For(0, THREADS, i =>
                {
                    var graph = graphs[i];
                    var rand = rands[i];

                    var currMax = GetMaxDegreeForBudget(graph, SamplingMethod.RV_RN, b, rand);
                    Interlocked.Add(ref AllMaxes, currMax);

                });
                RVRNAvgMax.Add((double)AllMaxes / THREADS);
                File.AppendAllText(fileName, "\t" + (double)AllMaxes / THREADS);
                Console.WriteLine($"Finished RVRN for b={b}, {DTS}");
            }
            File.AppendAllText(fileName, "\n");
            //String results = $"RV\t{String.Join("\t", RVAvgMax)}" + "\n" + $"RN\t{String.Join("\t", RNAvgMax)}" + "\n" + $"RNRV\t{String.Join("\t", RVRNAvgMax)}" + "\n";
            //File.WriteAllText("Results.tsv", results);
            Console.WriteLine("Done");
            Console.ReadKey();
        }

        static int GetMaxDegreeForBudget(Graph graph, SamplingMethod method, decimal budget, Random rand)
        {
            int currMaxDegree = Int32.MinValue;
            decimal currCost = 0;
            while (currCost <= budget)
            {
                var firstVertex = graph.PositiveDegreeVertices.ChooseRandomElement(rand); // NOTE: Assuming we sample WITH REPLACEMENT
                var neighbor = firstVertex.Neighbors.ChooseRandomElement(rand);
                var thisMaxDegree = 
                    method == SamplingMethod.RV ? firstVertex.Degree : 
                        method == SamplingMethod.RN ? neighbor.Degree : Math.Max(firstVertex.Degree, neighbor.Degree);
                currMaxDegree = Math.Max(currMaxDegree, thisMaxDegree);
                currCost += CostPerSampling(method);
            }
            return currMaxDegree;
        }
    }
}
