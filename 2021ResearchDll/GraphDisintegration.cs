using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilsYN;

namespace _2021ResearchDll
{
    /* YN 5/12/21 - These are some methods from a previous graph disintegration program that can probably be reused
     * so putting them here.
     */

    public class GraphDisintegration
    {
        public enum UnionFindRank { Max, SecondLargest }
        /// <summary>
        /// Pass in the final state of a graph after disintegration, and a collection of the edges removed in order, and it
        /// will return an array with the size of the max/second componenet at each step.
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="removedEdges"></param>
        /// <param name="rank"></param>
        /// <returns></returns>
        public static int[] rebuildMaxComponents(Graph graph, List<Tuple<String, String>>[] removedEdges, UnionFindRank rank)
        {
            int[] maxComponents = new int[removedEdges.Length];

            UnionFind<String> rebuildUnionFind = new UnionFind<string>();
            rebuildUnionFind.AddElements(graph.Vertices.Select(v => v.Id));
            graph.Edges.ToList().ForEach(e => rebuildUnionFind.Union(e.v1.Id, e.v2.Id));
            // Final state of the graph is the last entry
            maxComponents[removedEdges.Length - 1] = rank == UnionFindRank.Max ? rebuildUnionFind.GetMaxSetCount() : rebuildUnionFind.GetSecondLargestSetCount();
            // Loop backwards through removedEdges. Adding a set of removed edges gives the state of the PREVIOUS graph, last entry is irrelevant it just gives
            // the original graph.
            for (int i = removedEdges.Length - 1; i > 0; i--)
            {
                foreach (var edge in removedEdges[i])
                {
                    rebuildUnionFind.Union(edge.Item1, edge.Item2);
                }
                maxComponents[i - 1] = rank == UnionFindRank.Max ? rebuildUnionFind.GetMaxSetCount() : rebuildUnionFind.GetSecondLargestSetCount();
            }
            return maxComponents;
        }

    }
}
