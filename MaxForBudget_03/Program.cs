using GraphLibYN_2019;
using PyReporting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilsYN;

namespace MaxForBudget_03
{
    class Program
    {
        /* YN 7/28/21 - Yet another redo of the beginning experiment for the introductory charts. Writing it now that we have PyReporting built in, etc.
         * Really the only goal is to redo the chart per Amotz's suggestion that we RVN for comparison, so this will be VERY hardcoded.
         */

        const bool debug = false;
        const int EXPERIMENTS = debug ? 1 : 200;
        const int GRAPHS = debug ? 1 : 50;
        static Random[] rands = TSRandom.ArrayOfRandoms(EXPERIMENTS);
        static string DTS => UtilsYN.Utils.DTS;
        static IEnumerable<int> Range(int start, int count) => Enumerable.Range(start, count);
        static IEnumerable<int> Range(int count) => Range(0, count);

        static void Main(string[] args)
        {
            var N = 10000;
            var M = 20;
            var MAX_BUDGET = 100;

            var graphs = Range(GRAPHS).AsParallel().Select(i => Graph.NewBaGraph(N, M, random: rands[i])).ToArray();

            var naiveRnMaxes = new double[MAX_BUDGET];
            var halfAndHalfMaxes = new double[MAX_BUDGET];
            var halfNeighborsMaxes = new double[MAX_BUDGET];
            var rvMaxes = new double[MAX_BUDGET];

            for (int currBudget = 1; currBudget <= MAX_BUDGET; currBudget++)
            {
                if (currBudget % 10 == 1)
                    Console.WriteLine($"Starting budget: {currBudget} {DTS}");

                double[] naiveRn = new double[EXPERIMENTS];

                var t1 = Task.Factory.StartNew(
                    () =>
                    {
                        Parallel.For(0, EXPERIMENTS, exp =>
                        {
                            var currMax = 0;
                            var currRand = rands[exp];
                            var graph = graphs[exp % GRAPHS];
                            for (int b = 0; b < currBudget; b++)
                            {
                                var vertex = graph.Vertices.ChooseRandomElement(currRand);
                                var neighbor = vertex.Neighbors.ChooseRandomElement(currRand);
                                currMax = Math.Max(currMax, neighbor.Degree);
                            }
                            naiveRn[exp] = currMax;

                        });
                        naiveRnMaxes[currBudget - 1] = naiveRn.Average();
                    });

                double[] halfAndHalf = new double[EXPERIMENTS];
                double[] halfNeighborsWithNoVertices = new double[EXPERIMENTS];

                var t2 = Task.Factory.StartNew(
                    () =>
                    {
                        Parallel.For(0, EXPERIMENTS, exp =>
                        {
                            var vertexAndNeighborCurrMax = 0;
                            var onlyNeighborsCurrMax = 0;
                            var currRand = rands[exp];
                            var graph = graphs[exp % GRAPHS];
                            var b = 0;
                            while (b < currBudget)
                            {
                                var vertex = graph.Vertices.ChooseRandomElement(currRand);
                                vertexAndNeighborCurrMax = Math.Max(vertexAndNeighborCurrMax, vertex.Degree);
                                b++;
                                if (b < currBudget)
                                {
                                    var neighbor = vertex.Neighbors.ChooseRandomElement(currRand);
                                    vertexAndNeighborCurrMax = Math.Max(vertexAndNeighborCurrMax, neighbor.Degree);
                                    onlyNeighborsCurrMax = Math.Max(onlyNeighborsCurrMax, neighbor.Degree);
                                    b++;
                                }
                            }
                            halfAndHalf[exp] = vertexAndNeighborCurrMax;
                            halfNeighborsWithNoVertices[exp] = onlyNeighborsCurrMax;

                        });
                        halfAndHalfMaxes[currBudget - 1] = halfAndHalf.Average();
                        halfNeighborsMaxes[currBudget - 1] = halfNeighborsWithNoVertices.Average();
                    });

                double[] rv = new double[EXPERIMENTS];

                var t3 = Task.Factory.StartNew(
                    () =>
                    {
                        Parallel.For(0, EXPERIMENTS, exp =>
                        {
                            var currMax = 0;
                            var currRand = rands[exp];
                            var graph = graphs[exp % GRAPHS];
                            for (int b = 0; b < currBudget; b++)
                            {
                                var vertex = graph.Vertices.ChooseRandomElement(currRand);
                                currMax = Math.Max(currMax, vertex.Degree);
                            }
                            rv[exp] = currMax;

                        });
                        rvMaxes[currBudget - 1] = rv.Average();
                    });
                t1.Wait();
                t2.Wait();
                t3.Wait();
            }

            Py.CreatePyPlot(
                Py.PlotType.scatter,
                Range(1, MAX_BUDGET).Select(i => (double)i).ToArray(),
                new[] { naiveRnMaxes, halfAndHalfMaxes, rvMaxes },
                new[] { "b neighbors", "b/2 vertices and b/2 neighbors", "b vertices" },
                new[] { "b", "r", "g" },
                "Max Degree by Budget\\nBA Graph n=10,000 m=20",
                "Budget",
                "Max Degree",
               "MaxDegreeByBudget_BNgh-HalfNghHalfVrt-BVrt"
                );

            Py.CreatePyPlot(
                Py.PlotType.scatter,
                Range(1, MAX_BUDGET).Select(i => (double)i).ToArray(),
                new[] { halfAndHalfMaxes, halfNeighborsMaxes },
                new[] { "b/2 vertices and b/2 neighbors", "b/2 neighbors" },
                new[] { "b", "r" },
                "Max Degree by Budget\\nBA Graph n=10,000 m=20",
                "Budget",
                "Max Degree",
               "MaxDegreeByBudget_BNgh-HalfNghHalfVrt"
                );

            Py.CreatePyPlot(
               Py.PlotType.scatter,
               Range(1, MAX_BUDGET).Select(i => (double)i).ToArray(),
               new[] { naiveRnMaxes, halfNeighborsMaxes, rvMaxes },
               new[] { "b neighbors", "b/2 neighbors", "b vertices" },
               new[] { "b", "r", "g" },
               "Max Degree by Budget\\nBA Graph n=10,000 m=20",
               "Budget",
               "Max Degree",
               "MaxDegreeByBudget_BNgh-HalfNgh-BVrt"
               );
        }


    }
}
