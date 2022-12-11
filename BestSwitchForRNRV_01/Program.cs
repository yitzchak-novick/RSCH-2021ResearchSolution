using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilsYN;

namespace BestSwitchForRNRV_01
{
    class Program
    {
        /* YN 7/13/21 - We will repeatedly sample from a graph, first with RN, then switch to RV at some p of n log n.
         * We will track the total unique edges accumulated as a percent of the graph and try to see which p
         * gives the fastest ascent to getting all edges.
         */
        static readonly double PCT_OF_NLOGN = 0.95;
        const int EXPERIMENTS_PER_GRAPH = 115;
        static Random[] rands = TSRandom.ArrayOfRandoms(EXPERIMENTS_PER_GRAPH);
        static string DTS => UtilsYN.Utils.DTS;

        static void Main(string[] args)
        {
            Console.WriteLine($"Starting experiment 1 {DTS}");
            Experiment_01();
            Console.WriteLine($"Done {DTS}");
            Console.ReadKey();
        }

        static void Experiment_01()
        {
            var graph = Graph.NewBaGraph(1000, 5, random: rands[0]);
            List<double[]> allResults = new List<double[]>();
            List<string> lineLabels = new List<string>();
            for (double d = 0.0; d <= 0.25; d += 0.05)
            {
                Console.WriteLine($"Starting d={d}, {DTS}");
                lineLabels.Add(d.ToString("0.#0"));
                allResults.Add(GetAveragePctOfEdges(graph, d));
            }
            allResults = allResults.Select(l => l.Skip((int)(l.Length * 0.85)).ToArray()).ToList();
            PyReporting.Py.CreatePyPlot(PyReporting.Py.PlotType.plot,
                Enumerable.Range(1, allResults[0].Length).Select(i => (double)i).ToArray(),
                allResults.Select(l => l.ToArray()).ToArray(),
                lineLabels.ToArray(),
                title: "RNRV Switch Percentages",
                xlabel: "Total Samples Taken",
                ylabel: "Total Pct of Edges",
                fileName: "ExperimentalResults.png");

        }

        // This is one approach to try different pcts at which we will switch and see the percent of the graph
        // that has been sampled at each sampling for a specific pct at which we switch from RN to RV. Note that
        // this will perform multiple experiments on the same graph, should probably rewrite so it takes an
        // IEnumerable<Graph>
        static double[] GetAveragePctOfEdges(Graph origgraph, double pctToSwitch)
        {

            List<double> pctsFound = new List<double>();
            var totalIterations = (int)(origgraph.Vertices.Count() * Math.Log(origgraph.Vertices.Count()) * PCT_OF_NLOGN);

            double[][] allResults = Enumerable.Range(0, EXPERIMENTS_PER_GRAPH).Select(n => new double[totalIterations]).ToArray();

            Parallel.For(0, EXPERIMENTS_PER_GRAPH, thrd => 
            {
                var rand = rands[thrd];
                var graph = origgraph.Clone();
                var totalEdges = 2.0 * graph.Edges.Count();

                HashSet<Vertex> collectedVertices = new HashSet<Vertex>();
                int collectedEdges = 0;

                int i;
                for (i = 0; i < (int)(totalIterations * pctToSwitch); i++)
                {
                    var vertex = graph.Vertices.ChooseRandomElement(rand);
                    var neighbor = vertex.Neighbors.ChooseRandomElement(rand);
                    if (collectedVertices.Add(neighbor))
                        collectedEdges += neighbor.Degree;
                    allResults[thrd][i] = collectedEdges / totalEdges;
                }
                for (int j = i; j < totalIterations; j++)
                {
                    var vertex = graph.Vertices.ChooseRandomElement(rand);
                    if (collectedVertices.Add(vertex))
                        collectedEdges += vertex.Degree;
                    allResults[thrd][j] = collectedEdges / totalEdges;
                }
            });
            return Enumerable.Range(0, allResults[0].Length).Select(indx => allResults.Average(arr => arr[indx])).ToArray();
        }


    }
}
