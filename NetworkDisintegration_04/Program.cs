using _2021ResearchDll;
using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilsYN;
using static _2021ResearchDll.GraphMethods;

namespace NetworkDisintegration_04
{
    class Program
    {
        /* YN 5/12/21 - This will be long...
         * 
         * NetworkDisintegration_02 disintegrated graphs and reported the Max and Second largest components at every step for different sampling methods.
         * One of the things I tried to use this for in Python was to find a "critical Cs" that makes two methods equal assuming Cv=Cn=1. We used three
         * different metrics to find the "point of disintegration", when the 2nd largest component hits its maximal value, when the max component goes below
         * sqrt(n) for the first time, and when the max comoponent goes below log(n) for the first time. We found the cost of RN at this point, and the cost
         * of RVRN at this point, and determined the Cs that made the total cost of each equal.
         * 
         * The problem was that repeating the exeripent 50 times gave very different values than 200 times, so I don't know if the results mean anything or
         * not. What I want to do here first, is just to repeat the experiment multiple times for multiple numbers of graphs and see if there is a number where
         * the results come back consistently. We won't do all graphs, to save time we'll pick a few and just report the critical Cs values for each
         * 
         * Unlike the first version, we won't be using Python here at all, we'll do everything in C# and calculate the CCs values here and report them.
         * 
         * The other thing we will do is sample WITHOUT replacement to save time. So we will sort the vertices in a random order and then pick them in that order. This raises
         * an issue for picking a vertex that has already had all its friends removed, to start we will NOT charge Cv for 0-degree vertices. So we will work through
         * in order and just not charge for any 0-degree vertex we find. From previous experiments, we know you can be left with small components at the end, but
         * this shouldn't matter.
         */

        static readonly String[] SELECTED_RW_GRAPHS = new[] {"edit-gdwiki", "edit-hawiktionary", "edit-pswikibooks", "opsahl-openflights", "wikipedia_link_bug",
            "edit-kbdwiki", "edit-etwikisource", "wikipedia_link_ko", "wiki_talk_sv", "edit-dzwiktionary"};

        static readonly Tuple<GraphType, int, int>[] SELECTED_SYN_GRAPHS = new Tuple<GraphType, int, int>[] {
            new Tuple<GraphType, int, int>(GraphType.ER, 500, 8),
            new Tuple<GraphType, int, int>(GraphType.BA, 500, 8),
            new Tuple<GraphType, int, int>(GraphType.ER, 1000, 5),
            new Tuple<GraphType, int, int>(GraphType.ER, 1000, 15),
            new Tuple<GraphType, int, int>(GraphType.BA, 1000, 15),
            new Tuple<GraphType, int, int>(GraphType.BA, 1000, 25),
            new Tuple<GraphType, int, int>(GraphType.ER, 7500, 15),
            new Tuple<GraphType, int, int>(GraphType.BA, 7500, 15),
            new Tuple<GraphType, int, int>(GraphType.BA, 7500, 50)
        };

        static readonly int THREADS = 200;
        static StringBuilder AllResults = new StringBuilder();
        Object AllResults_LOCK = new Object();

        static Random[] rands = TSRandom.ArrayOfRandoms(THREADS);
        static void Main(string[] args)
        {
            AllResults.AppendLine($"Graph\tN\tM\tRV\tSecondComp\tSqrt\tLogN");
        }

        static void DisintegrateGraphs(Graph[] graphs)
        {
            double N = graphs.Average(g => g.Vertices.Count());
            double M = graphs.Average(g => g.Edges.Count());
            double RV = graphs.Average(g => g.Vertices.Average(v => v.Degree));
            double[] secondCompVals = new double[THREADS];
            double[] sqrtVals = new double[THREADS];
            double[] logNVals = new double[THREADS];

            Parallel.For(0, THREADS, currThread =>
            {
                var rand = rands[currThread];

                // Code for RN:
                var graph = graphs[currThread].Clone();

                int[] MaxComponentsByCost;
                int[] MaxSecondComponentsByCost;

                DisintegrateByRn(graph, out MaxComponentsByCost, out MaxSecondComponentsByCost, rand);


                int RnMaxSecondComponentCost = MaxSecondComponentsByCost.ToList().IndexOf(MaxSecondComponentsByCost.Max());
                int RnSqrtNCost = MaxComponentsByCost.ToList().IndexOf(MaxComponentsByCost.First(i => i < Math.Sqrt(graph.Vertices.Count())));
                int RnLogNCost = MaxComponentsByCost.ToList().IndexOf(MaxComponentsByCost.First(i => i < Math.Sqrt(graph.Vertices.Count())));

                DisintegrateByRvRn(graph, out MaxComponentsByCost, out MaxSecondComponentsByCost, rand);

                int RvRnMaxSecondComponentCost = MaxSecondComponentsByCost.ToList().IndexOf(MaxSecondComponentsByCost.Max());
                int RvRnSqrtNCost = MaxComponentsByCost.ToList().IndexOf(MaxComponentsByCost.First(i => i < Math.Sqrt(graph.Vertices.Count())));
                int RvRnLogNCost = MaxComponentsByCost.ToList().IndexOf(MaxComponentsByCost.First(i => i < Math.Sqrt(graph.Vertices.Count())));


            });
        }

        static void DisintegrateByRn(Graph graph, out int[] MaxComponentsByCostArray, out int[] MaxSecondComponentsByCostArray, Random rand)
        {
            graph = graph.Clone();
            var vertices = graph.Vertices.OrderBy(v => rand.Next()).ToList();

            List<Tuple<string, string>>[] RnEdgesRemovedByCost = new List<Tuple<string, string>>[vertices.Count + 1];
            Parallel.For(0, RnEdgesRemovedByCost.Length, i => RnEdgesRemovedByCost[i] = new List<Tuple<string, string>>()); // think we need this to avoid a runtime error...
            int currCost = 2;

            foreach (var vertex in vertices)
            {
                if (vertex.Degree == 0)
                    continue;
                var neighbor = vertex.Neighbors.ChooseRandomElement(rand);
                RnEdgesRemovedByCost[currCost] = neighbor.Edges.Select(e => new Tuple<string, string>(e.v1.Id, e.v2.Id)).ToList();
                neighbor.Neighbors.ToList().ForEach(n => graph.RemoveEdge(vertex.Id, n.Id));
                currCost += 2;
            }

            MaxComponentsByCostArray = GraphDisintegration.rebuildMaxComponents(graph, RnEdgesRemovedByCost, GraphDisintegration.UnionFindRank.Max);
            MaxSecondComponentsByCostArray = GraphDisintegration.rebuildMaxComponents(graph, RnEdgesRemovedByCost, GraphDisintegration.UnionFindRank.Max);
        }

        static void DisintegrateByRvRn(Graph graph, out int[] MaxComponentsByCostArray, out int[] MaxSecondComponentsByCostArray, Random rand)
        {
            graph = graph.Clone();
            var vertices = graph.Vertices.OrderBy(v => rand.Next()).ToList();

            List<Tuple<string, string>>[] RvRnEdgesRemovedByCost = new List<Tuple<string, string>>[vertices.Count + 1];
            Parallel.For(0, RvRnEdgesRemovedByCost.Length, i => RvRnEdgesRemovedByCost[i] = new List<Tuple<string, string>>()); // think we need this to avoid a runtime error...
            int currCost = 1;
            foreach (var vertex in vertices)
            {
                if (vertex.Degree == 0)
                    continue;
                var neighbor = vertex.Neighbors.ChooseRandomElement(rand);

                RvRnEdgesRemovedByCost[currCost] = vertex.Edges.Select(e => new Tuple<string, string>(e.v1.Id, e.v2.Id)).ToList();
                vertex.Neighbors.ToList().ForEach(n => graph.RemoveEdge(vertex.Id, n.Id));
                currCost++;

                if (neighbor.Degree == 0)
                    continue;

                RvRnEdgesRemovedByCost[currCost] = neighbor.Edges.Select(e => new Tuple<string, string>(e.v1.Id, e.v2.Id)).ToList();
                neighbor.Neighbors.ToList().ForEach(n => graph.RemoveEdge(vertex.Id, n.Id));
                currCost++;
            }

            MaxComponentsByCostArray = GraphDisintegration.rebuildMaxComponents(graph, RvRnEdgesRemovedByCost, GraphDisintegration.UnionFindRank.Max);
            MaxSecondComponentsByCostArray = GraphDisintegration.rebuildMaxComponents(graph, RvRnEdgesRemovedByCost, GraphDisintegration.UnionFindRank.Max);
        }

    }
}
