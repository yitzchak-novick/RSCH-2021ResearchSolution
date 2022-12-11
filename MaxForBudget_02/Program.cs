using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilsYN;

namespace MaxForBudget_02
{
    class Program
    {
        // This is a redo of the very first experiment we did, where we show that the max of a collection of vertices
        // collected using RN with a budget b is not as impressive if you charge a cost for both vertices instead of
        // just the first one. Redoing it with two things in mind:
        // 1) Want the code to be more versatile so we can create charts for more graphs is we need to
        // 2) Want to see if there is really any difference between b/2 neighbors, and b/2 vertices + b/2 neighbors
        // You would think the max will come from the neighbors anyway, so can we say that both are really the same?
        // So the results will include b vertices, b neighbors, b/2 neighbors, and b/2 vertices + b/2 neighbors
        // so that we can plot any combination of the four for our comparisons.

        static string DTS => Utils.DTS;

        enum GraphType { ER, BA };
        static GraphType graphType = GraphType.BA;
        static int N = 10000;
        static int M = 20;
        // we are assuming 10% of the vertices is interesting, beyond that boring. Will collect 20% just in case it adds anything, but if we later decide we want
        // more, we'll have to redo everything.
        static int MAX_BUDGET => N/5; // Has to be a method when when we update params

        static int THREADS = 30; // how many simultanesous experiments
        static Graph[] graphs = new Graph[THREADS];
        static Random[] rands;

        static string FILENAME => $"{graphType}_N_{N}_M_{M}_results.tsv";

        static double[] RV_VALS;
        static double[] RN_VALS;
        static double[] B_OVER_2_NEIGHBORS;
        static double[] HALF_AND_HALF;

        static void Main(string[] args)
        {
            Console.WriteLine($"Starting program at {DTS}");

            foreach (var n in new[] { 10000 } /* {100, 500, 1000, 2000, 5000, 7500, 10000 }*/)
                foreach(var m in new[] { 20 } /* { 2, 5, 8, 15, 25, 50, 100}*/)
                {
                    if (m >= n) continue;
                    foreach(var gType in new[] { GraphType.ER, GraphType.BA })
                    {
                        N = n;
                        M = m;
                        graphType = gType;
                        Run();
                    }
                }

            Console.WriteLine($"Finished program at {DTS}, press any key to exit");
            Console.ReadKey();
        }

        static int totalRuns = 1;
        static void Run()
        {
            Console.WriteLine($"Starting run {totalRuns}, Type: {graphType}, N: {N}, M: {M}, Max Budget: {MAX_BUDGET}, at {DTS}");
            InitializeGraphs();
            Console.WriteLine($"Done initializing graphs {DTS}");

            // Write the x_vals we'll use when plotting the values in matplotlib
            File.WriteAllText(FILENAME, "xvals\t" + String.Join("\t", Enumerable.Range(1, MAX_BUDGET)) + "\n");

            CollectByBudget();

            WriteResults();

            Console.WriteLine($"Done with run {totalRuns++}, at {DTS}");
        }

        // Make this a method so we can switch from BR to ER (or whatever?) easily
        static void InitializeGraphs()
        {
            rands = TSRandom.ArrayOfRandoms(THREADS);
            switch (graphType)
            {
                case GraphType.BA:
                    Parallel.For(0, THREADS, i =>
                    {
                        graphs[i] = Graph.NewBaGraph(N, M, random: rands[i]);
                    });
                    break;
                case GraphType.ER:
                    Parallel.For(0, THREADS, i =>
                    {
                        graphs[i] = Graph.NewErGraphFromBaM(N, M, random: rands[i]);
                    });
                    break;
                default:
                    // eventually will probably want to run this for at least real-world graphs also...
                    throw new Exception($"No code for graph type: {graphType}");
            }

        }

        // It's a lot less efficient, but will collect for each budget separately. When dealing with an average of a collection,
        // you would assume an anomoly will be corrected pretty quickly. But when the question is the max of the collection, if
        // one collection stumbles on the max vertex early, it'll be in that collection for the rest of the samplings.
        static void CollectByBudget()
        {
            // Reset the global arrays based on the new budgets
            RV_VALS = new double[MAX_BUDGET + 1]; // won't include 0 in the output
            RN_VALS = new double[MAX_BUDGET + 1];
            B_OVER_2_NEIGHBORS = new double[MAX_BUDGET + 1];
            HALF_AND_HALF = new double[MAX_BUDGET + 1];

            var allRvVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_BUDGET + 1]).ToArray();
            var allRnVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_BUDGET + 1]).ToArray();
            var allHalfRnVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_BUDGET + 1]).ToArray();
            var allHalfAndHalfVals = Enumerable.Range(0, THREADS).Select(i => new int[MAX_BUDGET + 1]).ToArray();

            Parallel.For(0, THREADS, i =>
            {
                var graph = graphs[i];
                var rand = rands[i];

                for (int b = 1; b <= MAX_BUDGET; b++)
                {
                    var vertices = graph.PositiveDegreeVertices.ChooseRandomSubset(b, random: rand);
                    var neighbors = vertices.Select(v => v.Neighbors.ChooseRandomElement(random: rand));
                    allRvVals[i][b] = vertices.Max(v => v.Degree);
                    allRnVals[i][b] = neighbors.Max(v => v.Degree);
                    try
                    {
                        allHalfRnVals[i][b] = neighbors.Take(b / 2).Max(v => v.Degree);
                        allHalfAndHalfVals[i][b] = vertices.Take(b / 2).Union(neighbors.Take(b / 2)).Max(v => v.Degree);
                    }
                    catch { } // for b = 1 there will be nothing to take, leave the value 0 in the array
                    
                }
            });

            // Collapse the values:
            for (int b = 1; b <= MAX_BUDGET; b++)
            {
                RV_VALS[b] = allRvVals.Select(arr => arr[b]).Average();
                RN_VALS[b] = allRnVals.Select(arr => arr[b]).Average();
                B_OVER_2_NEIGHBORS[b] = allHalfRnVals.Select(arr => arr[b]).Average();
                HALF_AND_HALF[b] = allHalfAndHalfVals.Select(arr => arr[b]).Average();
            }
        }

        // Write the results
        static void WriteResults()
        {
            File.AppendAllText(FILENAME, "b-vertices\t" + String.Join("\t", RV_VALS.Skip(1)) + "\n");
            File.AppendAllText(FILENAME, "b/2-neighbors\t" + String.Join("\t", B_OVER_2_NEIGHBORS.Skip(1)) + "\n");
            File.AppendAllText(FILENAME, "b/2-vertices-b/2-neighbors\t" + String.Join("\t", HALF_AND_HALF.Skip(1)) + "\n");
            File.AppendAllText(FILENAME, "b-neighbors\t" + String.Join("\t", RN_VALS.Skip(1)) + "\n");
        }
    }
}
