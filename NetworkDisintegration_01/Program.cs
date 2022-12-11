using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilsYN;

namespace NetworkDisintegration_01
{
    class Program
    {
        // YN 2/10/2021 - Writing code that will disintegrate a network and track the cost of getting to each
        // point. I think some of this code has been written already, but it feels easier to rewrite it now.
        static void Main(string[] args)
        {
            Random rand = new Random();
            Graph graph = Graph.NewBaGraph(2000, 4);
            Console.WriteLine($"RV: {graph.RV}, RN: {graph.RN}, Ratio: {graph.RN / graph.RV}");
            Graph graphForDesc = graph.Clone();
            Graph graphForRv = graph.Clone();
            Graph graphForRn = graph.Clone();
            var descDisintegration =
                GetComponentSizesByCostForDisintegration(DisintegrateGraph(graphForDesc, graphForDesc.PositiveDegreeVertices.OrderBy(v => v.Degree).Select(v => new Tuple<Vertex, decimal>(v, 1))));
            var rvDisintegration =
                
                GetComponentSizesByCostForDisintegration(DisintegrateGraph(graphForDesc, graphForRv.PositiveDegreeVertices.OrderBy(v =>  rand.Next()).Select(v => new Tuple<Vertex, decimal>(v, 1))));
            List<Tuple<Vertex, decimal>> orderedVerticesForRN = new List<Tuple<Vertex, decimal>>();
            var vertices = new HashSet<Vertex>(graphForRn.PositiveDegreeVertices);
            //var rnDisintegration =
            //    GetComponentSizesByCostForDisintegration(DisintegrateGraph(graphForDesc, graphForDesc.PositiveDegreeVertices.OrderBy(v => rand.Next()).Select(v => new Tuple<Vertex, decimal>(v.Neighbors.ChooseRandomElement, 1))));
            Console.ReadKey();
        }

        // This function will disintegrate a graph be removing all edges in the order specified in the IEnumerable,
        // it will track the cost as specified also, a cost for each vertex to allow for cases where the vertices have
        // different costs. Will return a set of the steps, each total cost with the edges that were removed for that cost.
        public static List<Tuple<decimal, List<Tuple<string, string>>>> DisintegrateGraph(Graph graph, IEnumerable<Tuple<Vertex, decimal>> verticesInOrder)
        {
            decimal totalCost = 0;
            List<Tuple<decimal, List<Tuple<string, string>>>> disintegrationCosts = new List<Tuple<decimal, List<Tuple<string, string>>>>();
            foreach(var vertexCostTuple in verticesInOrder)
            {
                var vertex = vertexCostTuple.Item1;
                totalCost += vertexCostTuple.Item2;
                var edges = vertex.Edges.Select(e => new Tuple<string, string>(e.v1.Id, e.v2.Id)).ToList();
                disintegrationCosts.Add(new Tuple<decimal, List<Tuple<string, string>>>(totalCost, edges));
                edges.ForEach(e => graph.RemoveEdge(e.Item1, e.Item2));
            }
            return disintegrationCosts;
        }

        /// <summary>
        /// Pass in a List of steps, where each step is the total cost through and including that step, and the set of edges that
        /// were removed at that step. Returns an array of Tuples, each showing the total cost at a step, and the resulting
        /// largest component size and second largest component size.
        /// </summary>
        /// <param name="EdgesRemovedByCost"></param>
        /// <returns></returns>
        static Tuple<decimal, int, int>[] GetComponentSizesByCostForDisintegration(List<Tuple<decimal, List<Tuple<string, string>>>> EdgesRemovedByCost)
        {
            UnionFind<string> vertexUf = new UnionFind<string>();
            vertexUf.AddElements(EdgesRemovedByCost.SelectMany(t1 => t1.Item2.SelectMany(t2 => new[] { t2.Item1, t2.Item2 }))); // add the individual ids
            Tuple<decimal, int, int>[] ComponentSizesByCost = new Tuple<decimal, int, int>[EdgesRemovedByCost.Count()];
            for (int i = EdgesRemovedByCost.Count() - 1; i >= 0; i--)
            {
                ComponentSizesByCost[i] = new Tuple<decimal, int, int>(EdgesRemovedByCost[i].Item1, vertexUf.GetMaxSetCount(), vertexUf.GetSecondLargestSetCount());
                EdgesRemovedByCost[i].Item2.ForEach(t => vertexUf.Union(t.Item1, t.Item2));
            }
            return ComponentSizesByCost;
        }


    }
}
