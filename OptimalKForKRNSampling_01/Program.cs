using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilsYN;

namespace OptimalKForKRNSampling_01
{
    /* YN 7/13/21 - For the purposes of the paper for Complex Networks 2021, updating an old experiment to make sure it
     * works right and to have the ability to change whatever we need to about the chart.
     * 
     * For now I think I'll start by looking for a heatmap. The graph type, n, and a budget for sampling will be fixed.
     * The x-axis will be values of k, how many neighbors per vertex do we take. The y-axis will be average degree, and the
     * heat colors will be some measure of success,
     * avg. degree, total degree, total unique edges, max-degree, etc. Just need to pick one..
     */

    class Program
    {
        const int EXPERIMENTS = 200;
        private const string SavedFilePath = "SavedResults.csv";
        static Random[] rands = TSRandom.ArrayOfRandoms(EXPERIMENTS);
        static string DTS => UtilsYN.Utils.DTS;
        static IEnumerable<int> Range(int start, int count) => Enumerable.Range(start, count);
        static IEnumerable<int> Range(int count) => Range(0, count);

        static void Main(string[] args)
        {
            while (!File.Exists(@"C:\Users\Yitzchak\Desktop\Visual Studio 2021\2021Research_Sol01\CheckIfMaxDegreeIsInVerticesOrNeighbors_01\bin\Debug\Done.txt"))
                Thread.Sleep(TimeSpan.FromMinutes(1));
            Console.WriteLine($"Other program is done, starting now {DTS}");
            foreach (var budget in new[] { 20, 60, 100, 200, 400, 800, 1500 })
            {
                Console.WriteLine($"Starting budget: {budget}, {DTS}");
                Experiment02(budget);
            }
            Console.WriteLine($"Done {DTS}");
            File.WriteAllText("Done.txt", "Done");
        }

        static void Experiment01()
        {
            // Just to get my feet wet.. We'll use a BA graph with n = 1000 and bugdet of 180. We'll try to generate a heat map
            // for average degree of the collection. We'll collect 1 .. 11 friends per vertex. 180 allows most of these k values
            // to collect all of their neighbors on every iteration, but some will have to collection a few fewer neighbors on their
            // last iteration, we'll consider this negligible.
            var maxM = 25; // 30;
            var maxK = 25; // 60;
            var N = 4000; // 12000;
            var totalBudget = 40;

            var results = Range(maxM).Select(i => new double[maxK]).ToArray();

            if (!File.Exists(SavedFilePath))
                File.WriteAllText(SavedFilePath, "");

            var prevResults =
                File.ReadAllLines(SavedFilePath).Select(l => new Tuple<int, int, double>(int.Parse(l.Split(',')[0]), int.Parse(l.Split(',')[1]), double.Parse(l.Split(',')[2])));

            var mkTuples = Range(2, maxM).SelectMany(m => Range(1, maxK).Select(k => new Tuple<int, int>(m, k))).OrderBy(t => Math.Max(t.Item1, t.Item2)).ThenBy(t => Math.Min(t.Item1, t.Item2));


            foreach (var mkTuple in mkTuples)
            {
                var currM = mkTuple.Item1;
                var k = mkTuple.Item2;
                if (prevResults.Any(t => t.Item1 == k && t.Item2 == currM))
                {
                    results[maxM - (currM - 1)][k - 1] = prevResults.First(t => t.Item1 == k && t.Item2 == currM).Item3;
                }
                else
                {
                    Console.WriteLine($"Starting m: {currM}, k: {k}, Creating Graphs {DTS}");
                    double[] exprResults = new double[EXPERIMENTS];
                    var graphs = Range(0, 50).AsParallel().Select(i => Graph.NewBaGraph(N, currM, random: rands[i])).ToArray();
                    Console.WriteLine($"Starting m: {currM}, k: {k}, Creating Rank Collections {DTS}");
                    var vertexRanksCollection = new Dictionary<Vertex, int>[50];
                    Parallel.For(0, 50, i =>
                    {
                        int currRank = 0;
                        vertexRanksCollection[i] = graphs[i].Vertices.OrderBy(v => v.Degree).ToDictionary(v => v, v => ++currRank);
                    });
                    Console.WriteLine($"Starting m: {currM}, k: {k}, Starting Sampling {DTS}");
                    Parallel.For(0, EXPERIMENTS, thrd =>
                    {
                        var graph = graphs[thrd % 50];
                        var vertexRanks = vertexRanksCollection[thrd % 50];
                        var rand = rands[thrd];
                        List<Vertex> collection = new List<Vertex>();

                        int remainingBudget = totalBudget;
                        while (remainingBudget > 0)
                        {
                            var firstVertex = graph.Vertices.ChooseRandomElement(rand);
                            remainingBudget--;
                            if (remainingBudget == 0)
                                break;
                            var neighbors = firstVertex.Neighbors.ChooseRandomSubset(k, random: rand);
                            foreach (var neighbor in neighbors)
                            {
                                collection.Add(neighbor);
                                remainingBudget--;
                                if (remainingBudget == 0)
                                    break;
                            }
                        }
                        exprResults[thrd] = vertexRanks[collection.OrderByDescending(v => v.Degree).First()];
                    });
                    results[maxM - (currM - 1)][k - 1] = exprResults.Average();
                    File.AppendAllText(SavedFilePath, k + "," + currM + "," + results[maxM - (currM - 1)][k - 1] + "\n");
                }
            }
            PyReporting.Py.CreatePyHeatmap(results, Range(1, maxK).Select(i => i.ToString()).ToArray(), Range(2, maxM - 1).Reverse().Select(i => i.ToString()).ToArray(), "Max Degree", "K values", "m Values");
            Console.WriteLine("This far...");
        }

        // YN 7-21-21 This is a repeate of a chart that is currently in the paper (with some bad labels). We will create BA n=5000, m=2,3,4,5, and 6, and sample
        // 120 vertices with ratios of N:V 0:12, 1:11 ... 11:1 and record the max-degree found.
        // UPDATE: We will produce three charts;
        // 1) Rank of max degree as percent of N
        // 2) Total degrees accumulated (as percent of total degrees in graph)
        // 3) Total unique degrees accumuldated (as percent of total degrees in graph)
        // Budget is fixed (though it is a variable), x axis is k, and y axis is success, each line is a different value of m
        static void Experiment02(int totalBudget = 140)
        {
            var EXPERIMENTS = 21000; // MUCH bigger, like 21000 or something.
            var N = 5000;
            var GRAPHS = 200; // MUCH bigger, like 200
            var rands = TSRandom.ArrayOfRandoms(EXPERIMENTS);
            var maxK = 16;

            Dictionary<int, double[]> maxDegreeResultsByMVal = new Dictionary<int, double[]>();
            Dictionary<int, double[]> TotalDegreeResultsByMVal = new Dictionary<int, double[]>();
            Dictionary<int, double[]> TotalUniqueDegreeResultsByMVal = new Dictionary<int, double[]>();

            Dictionary<int, Graph[]> BaGraphsByM = new Dictionary<int, Graph[]>(); 
            for (int currM = 2; currM <= 6; currM++)
            {
                Console.WriteLine($"Starting m={currM}, {DTS}");
                double[][] allResultsMaxDegree = Range(maxK).Select(i => new double[EXPERIMENTS]).ToArray();
                double[][] allResultsTotalDegrees = Range(maxK).Select(i => new double[EXPERIMENTS]).ToArray();
                double[][] allResultsTotalUniqueDegrees = Range(maxK).Select(i => new double[EXPERIMENTS]).ToArray();
                BaGraphsByM[currM] = Range(GRAPHS).AsParallel().Select(i => Graph.NewBaGraph(N, currM, random: rands[i])).ToArray();
                for (int k = 1; k <= 16; k++)
                {
                    Parallel.For(0, EXPERIMENTS, i =>
                    {
                        var rand = rands[i];
                        var graph = BaGraphsByM[currM][i % GRAPHS];
                        int currRank = 0;
                        
                        Dictionary<Vertex, int> vertexRanks = new Dictionary<Vertex, int>();
                        var totalDegreesInGraph = graph.Vertices.Sum(v => v.Degree);
                        var vertexGroups = graph.Vertices.GroupBy(v => v.Degree).OrderBy(g => g.Key);
                        foreach(var g in vertexGroups)
                        {
                            currRank += g.Count();
                            foreach (var v in g)
                                vertexRanks[v] = currRank;
                        }
                        
                        var currBudget = totalBudget;
                        int maxDegeeRank = -1;
                        int totalDegrees = 0;
                        int totalUniqueDegrees = 0;
                        HashSet<Vertex> collectedNeighbors = new HashSet<Vertex>();
                        while (currBudget > 0)
                        {
                            var firstVertex = graph.Vertices.ChooseRandomElement(rand);
                            if (--currBudget == 0)
                                break;
                            var neighbors = firstVertex.Neighbors.ChooseRandomSubset(Math.Min(k, currBudget), random: rand);
                            currBudget -= neighbors.Count();
                            maxDegeeRank = Math.Max(maxDegeeRank, vertexRanks[neighbors.OrderByDescending(v => v.Degree).First()]);
                            foreach(var neighbor in neighbors)
                            {
                                totalDegrees += neighbor.Degree;
                                if (collectedNeighbors.Add(neighbor))
                                    totalUniqueDegrees += neighbor.Degree;
                            }
                        }
                        allResultsMaxDegree[k-1][i] = maxDegeeRank / (double)N;
                        allResultsTotalDegrees[k - 1][i] = totalDegrees / (double)totalDegreesInGraph;
                        allResultsTotalUniqueDegrees[k - 1][i] = totalUniqueDegrees / (double)totalDegreesInGraph;
                    });
                }
                maxDegreeResultsByMVal[currM] = Range(allResultsMaxDegree.Length).Select(i => allResultsMaxDegree[i].Average()).ToArray();
                TotalDegreeResultsByMVal[currM] = Range(allResultsTotalDegrees.Length).Select(i => allResultsTotalDegrees[i].Average()).ToArray();
                TotalUniqueDegreeResultsByMVal[currM] = Range(allResultsTotalUniqueDegrees.Length).Select(i => allResultsTotalUniqueDegrees[i].Average()).ToArray();
            }

            PyReporting.Py.CreatePyPlot(PyReporting.Py.PlotType.plot, Range(1, maxK).Select(i => (double)i).ToArray(), maxDegreeResultsByMVal.Values.ToArray(),
                maxDegreeResultsByMVal.Keys.Select(k => $"Avg Deg = {2*k}").ToArray(), 
                title: $"Max Degree for kRN by k (BA n={N})", xlabel: "k Values", ylabel: "Max Degree Percentile", fileName: $"MaxDegree_Ba{N}Budget{totalBudget}.png");

            PyReporting.Py.CreatePyPlot(PyReporting.Py.PlotType.plot, Range(1, maxK).Select(i => (double)i).ToArray(), TotalDegreeResultsByMVal.Values.ToArray(),
               TotalDegreeResultsByMVal.Keys.Select(k => $"Avg Deg = {2 * k}").ToArray(),
               title: $"Total Degrees for kRN by k (BA n={N})", xlabel: "k Values", ylabel: "Total Degree Percentage", fileName: $"TotalDegrees_Ba{N}Budget{totalBudget}.png");

            PyReporting.Py.CreatePyPlot(PyReporting.Py.PlotType.plot, Range(1, maxK).Select(i => (double)i).ToArray(), TotalUniqueDegreeResultsByMVal.Values.ToArray(),
                TotalUniqueDegreeResultsByMVal.Keys.Select(k => $"Avg Deg = {2 * k}").ToArray(),
                title: $"Total Unique Degrees for kRN by k (BA n={N})", xlabel: "k Values", ylabel: "Total Unique Degrees Percentage", fileName: $"TotalUniqueDegrees_Ba{N}Budget{totalBudget}.png");

            File.WriteAllText($"Vectors_B{totalBudget}.txt", "");
            for (int m = 2; m <= 6; m++)
            {
                File.AppendAllText($"Vectors_B{totalBudget}.txt", $"MaxDeg m:{m}\t");
                File.AppendAllText($"Vectors_B{ totalBudget}.txt", String.Join(",", maxDegreeResultsByMVal[m]) + "\n");
            }

            for (int m = 2; m <= 6; m++)
            {
                File.AppendAllText($"Vectors_B{totalBudget}.txt", $"TotDeg m:{m}\t");
                File.AppendAllText($"Vectors_B{totalBudget}.txt", String.Join(",", TotalDegreeResultsByMVal[m]) + "\n");
            }
            for (int m = 2; m <= 6; m++)
            {
                File.AppendAllText($"Vectors_B{totalBudget}.txt", $"TotUnqDeg m:{m}\t");
                File.AppendAllText($"Vectors_B{totalBudget}.txt", String.Join(",", TotalUniqueDegreeResultsByMVal[m]) + "\n");
            }

            Console.WriteLine("HEY");
        }
    }
}
