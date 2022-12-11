using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilsYN;

namespace CsAsFunctionOfTime_01
{
    class Program
    {
        /* YN 7/23/21 - Somewhat just trying to get a start on Cs...
         * What we'll try here, is to make Cs dynamic, each Cs costs more than the previous one. We'll consider
         * 1) logorithmic growth 2) linear growth 3) geometric growth 4) exponential growth
         * We'll sample with RN and RVN and test total unique degrees (for now, maybe we can add total degrees
         * later, max degree feels like it makes less sense but we'll see). The x axis will be results (or samples,
         * we can come back to that too), and the y axis will be cost per degree, and we'll have two lines on the plot,
         * one for RN, one for RVN, and see the cost per degree for each.
         */
        const int EXPERIMENTS = 400;
        const int GRAPHS = 100;
        static Random[] rands = TSRandom.ArrayOfRandoms(EXPERIMENTS);

        static string DTS => UtilsYN.Utils.DTS;
        static IEnumerable<int> Range(int start, int count) => Enumerable.Range(start, count);
        static IEnumerable<int> Range(int count) => Range(0, count);
        static void Main(string[] args)
        {
            PlotForLogNTry1();
            Console.ReadKey();
        }

        static void PlotForLogNTry1()
        {
            // An experiment with hardcoded values:
            var n = 1000;
            var m = 5;

            Func<int, double> log = i => 1 + Math.Log(i);
            Func<int, double> linear = i => 1 + i;
            Func<int, double> geometric = i => 1 + i * i;
            Func<int, double> exponential = i => 1 + Math.Pow(2, i);

            var graphs = Range(GRAPHS).AsParallel().Select(i => Graph.NewBaGraph(n, m, random: rands[i])).ToArray();
            Dictionary<double, double>[] rnResults = new Dictionary<double, double>[EXPERIMENTS];
            Dictionary<double, double>[] rvnResults = new Dictionary<double, double>[EXPERIMENTS];

            Parallel.For(0, EXPERIMENTS, i =>
            {
                var result = GetCostPerUniqueDegreeVectors(graphs[i % GRAPHS], exponential, rands[i]);
                rnResults[i] = result.Item1;
                rvnResults[i] = result.Item2;
            });

            var rnResultsAverages = rnResults.First().Keys.OrderBy(k => k).Select(k => rnResults.Average(d => d[k])).ToArray();
            var rvnResultsAverages = rvnResults.First().Keys.OrderBy(k => k).Select(k => rvnResults.Average(d => d[k])).ToArray();

            PyReporting.Py.CreatePyPlot(PyReporting.Py.PlotType.plot, rnResults.First().Keys.ToArray(),
                new[] { rnResultsAverages, rvnResultsAverages }, new[] { "RN", "RVN" }, new[] { "b", "r" }, "Cost Per Unique Degree", "Percent of Total Degrees", "Cost per Unique Degrees");
               
        }

        static Tuple<Dictionary<double, double>, Dictionary<double, double>> GetCostPerUniqueDegreeVectors(Graph graph, Func<int, double> csGrowthFunc, Random rand)
        {
            // RN
            HashSet<Vertex> rnCollectedVertices = new HashSet<Vertex>();
            int rnCollectedDegrees = 0;
            int rnCsIterations = 0;
            double rnTotalCost = 0;
            Dictionary<double, double> rnResultCostsPerDegree = new Dictionary<double, double>();
            for (double d = 0.0005; d <= 1.0; d += 0.0005)
            {
                var goal = graph.Vertices.Count() * d;
                while (rnCollectedDegrees < goal)
                {
                    var vertex = graph.Vertices.ChooseRandomElement(rand);
                    var neighbor = vertex.Neighbors.ChooseRandomElement(rand);
                    rnTotalCost += 2; // Cv+Cn
                    if (rnCollectedVertices.Add(neighbor))
                    {
                        rnCollectedDegrees += neighbor.Degree;
                        rnTotalCost += csGrowthFunc(++rnCsIterations);
                    }
                }
                rnResultCostsPerDegree[d] = rnTotalCost / rnCollectedDegrees;
            }

            // RVN
            HashSet<Vertex> rvnCollectedVertices = new HashSet<Vertex>();
            int rvnCollectedDegrees = 0;
            int rvnCsIterations = 0;
            double rvnTotalCost = 0;
            Dictionary<double, double> rvnResultCostsPerDegree = new Dictionary<double, double>();
            for (double d = 0.0005; d <= 1.0; d += 0.0005)
            {
                var goal = graph.Vertices.Count() * d;
                while (rvnCollectedDegrees < goal)
                {
                    var vertex = graph.Vertices.ChooseRandomElement(rand);
                    var neighbor = vertex.Neighbors.ChooseRandomElement(rand);
                    rvnTotalCost += 2; // Cv+Cn
                    if (rnCollectedVertices.Add(neighbor))
                    {
                        rvnCollectedDegrees += neighbor.Degree;
                        rvnTotalCost += csGrowthFunc(++rvnCsIterations);
                    }
                }
                rvnResultCostsPerDegree[d] = rvnTotalCost / rvnCollectedDegrees;
            }

            return new Tuple<Dictionary<double, double>, Dictionary<double, double>>(rnResultCostsPerDegree, rvnResultCostsPerDegree);
        }
    }
}
