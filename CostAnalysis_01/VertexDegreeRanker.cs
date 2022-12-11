using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CostAnalysis_01
{
    /* YN 7/22/21 - Need to class to track what percent of top vertices we've found.
     */

    public class VertexDegreeRanker
    {
        Graph graph;
        IGrouping<int, Vertex>[] VertexGroups;
        int currTotalFound = 0;
        int currGroupIndex = 0;
        Dictionary<Vertex, int> VertexGroupIndices = new Dictionary<Vertex, int>();
        HashSet<Vertex>[] groupMembersCurrentlyFound;

        public VertexDegreeRanker(Graph graph)
        {
            this.graph = graph;
            VertexGroups = graph.Vertices.GroupBy(v => v.Degree).OrderByDescending(g => g.Key).ToArray();
            for (int i = 0; i < VertexGroups.Length; i++)
                foreach (var vertex in VertexGroups[i])
                    VertexGroupIndices[vertex] = i;
            groupMembersCurrentlyFound = Enumerable.Range(0, VertexGroups.Length).Select(i => new HashSet<Vertex>()).ToArray();
        }

        public int GetCurrRankFound(Vertex vertex)
        {
            int vertexIndex = VertexGroupIndices[vertex];
            if (!groupMembersCurrentlyFound[vertexIndex].Add(vertex))
                return currTotalFound;

            if (vertexIndex == currGroupIndex) // In the current group, we've found one more "top" vertex and may have completed this group
            {
                currTotalFound++;
                while (groupMembersCurrentlyFound[currGroupIndex].Count == VertexGroups[currGroupIndex].Count() // Completed this group, take the total of the next group
                    && currGroupIndex < VertexGroups.Length - 1)                                                    // (unless this is the last group...)
                {
                    currTotalFound += groupMembersCurrentlyFound[++currGroupIndex].Count();
                }
            }
            return currTotalFound;
        }

        public double GetCurrRankFoundAsPct(Vertex vertex) => GetCurrRankFound(vertex) / (double)graph.Vertices.Count();
    }
}