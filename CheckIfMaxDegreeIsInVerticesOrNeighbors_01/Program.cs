using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilsYN;

namespace CheckIfMaxDegreeIsInVerticesOrNeighbors_01
{
    class Program
    {
        /* YN 7/22/21 - This should be a simple experiment to verify that when we collect a vertex and a neighbor, with what
         * probability is the max degree of the collection going to be one of the neighbors.
         * 
         * We will use ER and BA graphs with different m params and n params, and to start we will run it four times, sampling
         * .025n, .5n, .75n, and .1n (and each sample gives 2 vertices so the size of the collection will actually be
         * twice these values).
         * 
         * To keep things simple, for ER graphs we'll only sample from the positive degree vertices.
         */

        static string DTS => UtilsYN.Utils.DTS;
        static IEnumerable<int> Range(int start, int count) => Enumerable.Range(start, count);
        static IEnumerable<int> Range(int count) => Range(0, count);

        const int EXPERIMENTS = 750;
        const int GRAPHS = 150;
        static Random[] rands = TSRandom.ArrayOfRandoms(EXPERIMENTS);

        static void Main(string[] args)
        {
            //while (!new DirectoryInfo(@"C:\Users\Yitzchak\Desktop\Visual Studio 2021\2021Research_Sol01\OptimalKForKRNSampling_01\bin\Debug\").GetFiles().Any(fi => fi.Extension == "png"))
                //Thread.Sleep(60000);
            Experiment();
        }

        static void Experiment()
        {
            String fileName = "Results.csv";
            File.WriteAllText(fileName, "");
 
            StringBuilder results = new StringBuilder();
            void Append(string s)
            {
                results.Append(s);
                File.AppendAllText(fileName, s);
            }

            void AppendLine(string s = "")
            {
                results.AppendLine(s);
                Append(s + "\n");
            }


            int[] nVals = new[] { 500, 1000, 2500, 5000 };
            int[] mVals = new[] { 2, 5, 8, 15 };

            Dictionary<String, Graph[]> AllErGraphs = new Dictionary<String, Graph[]>();
            Dictionary<String, Graph[]> AllBaGraphs = new Dictionary<String, Graph[]>();

            for (var samplePct = .025; samplePct <= .1; samplePct += .025)
            {
                Console.WriteLine($"Staring sample pct: {samplePct}, {DTS}");

                AppendLine($"Sampling {samplePct} of N");

                foreach (var mVal in mVals)
                    Append(",");
                Append("ER,");
                foreach (var mVal in mVals)
                    Append(",");
                AppendLine("BA");

                Append("NVal,");
                foreach (var mVal in mVals)
                    Append($"Avg Deg = {2 * mVal},");
                Append("NVal,");
                Append(String.Join(",", mVals.Select(mVal => $"Avg Deg = {2 * mVal}")));
                AppendLine();

                foreach (var nVal in nVals)
                {
                    Console.WriteLine($"Starting nVal: {nVal}, {DTS}");
                    Append($"N={nVal},");
                    foreach (var mVal in mVals)
                    {
                        if (!AllErGraphs.ContainsKey($"{nVal}-{mVal}"))
                            AllErGraphs[$"{nVal}-{mVal}"] = Range(GRAPHS).AsParallel().Select(i => Graph.NewErGraphFromBaM(nVal, mVal, rands[i])).ToArray();
                        Append(PercentOfTimesMaxIsNeighbor(AllErGraphs[$"{nVal}-{mVal}"], (int)(nVal * samplePct), EXPERIMENTS) + ",");
                    }
                    Append($"N={nVal},");
                    for (int i = 0; i < mVals.Length; i++)
                    {
                        if (!AllBaGraphs.ContainsKey($"{nVal}-{mVals[i]}]"))
                            AllBaGraphs[$"{nVal}-{mVals[i]}"] = Range(GRAPHS).AsParallel().Select(j => Graph.NewBaGraph(nVal, mVals[i], random: rands[j])).ToArray();
                        Append(PercentOfTimesMaxIsNeighbor(AllBaGraphs[$"{nVal}-{mVals[i]}"], (int)(nVal * samplePct), EXPERIMENTS).ToString());
                        Append(i == mVals.Length - 1 ? "\n" : ",");
                    }
                }


            }
            var allResults = results.ToString();
            File.WriteAllText("Results2.csv", allResults);
            File.WriteAllText("Done.txt", $"Done {DTS}");
        }

        static double PercentOfTimesMaxIsNeighbor(Graph[] graphs, int verticesToSample, int experiments)
        {
            bool[] foundInNeighbor = new bool[experiments];

            Parallel.For(0, experiments, exp =>
            {
                var graph = graphs[exp % graphs.Length];
                HashSet<Vertex> vertices = new HashSet<Vertex>();
                HashSet<Vertex> neighbors = new HashSet<Vertex>();
                for (int i = 0; i < verticesToSample; i++)
                {
                    var vertex = graph.PositiveDegreeVertices.ChooseRandomElement(rands[i]);
                    var neighbor = vertex.Neighbors.ChooseRandomElement(rands[i]);
                    vertices.Add(vertex);
                    neighbors.Add(neighbor);
                }
                foundInNeighbor[exp] = neighbors.Max(n => n.Degree) >= vertices.Max(v => v.Degree);
            });
            return foundInNeighbor.Count(b => b) / (double)foundInNeighbor.Length;
        }
    }
}
