using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FindMinAndMaxAssortForBaAlpha
{
    class Program
    {
        /* YN 5/4/22 - Entirely unnecessary... Will rewire sets of BA graphs that are generated with different
         * alpha values for min and max assortativity, and then see what is the max and min possible assortativity
         * for each alpha
         */

        const int N = 700;
        const int M = 2;
        const int GRAPHS = 50;
        const int THREADS = 50;
        static decimal[] ALPHAS = new[] { 1.0 };// Enumerable.Range(0, 500).Select(i => i * .05m).ToArray();
        static Graph[] graphs = new Graph[GRAPHS];
        static int TotalRewirings = 2000;
        static void Main(string[] args)
        {
        }

        static double RewireGraphForAssortativity(Graph graph, Random rand, bool increaseAssortativity)
        {
            for (int i = 0; i < TotalRewirings; i++)
            {

            }
        }
    }
}
