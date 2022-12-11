using GraphLibYN_2019;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilsYN;

namespace OptimalKForKRNSampling_02
{
    class Program
    {
        /* YN 7/21/21 - Trying another approach to determine the best k for kRN sampling. Trying to produce a heatmap
         * plot where the x-axis is different values of k, the y axis is some measure of success (how many total degrees
         * for example) and the heat is the cost per unit of success.
         */

        static string DTS => UtilsYN.Utils.DTS;
        static IEnumerable<int> Range(int start, int count) => Enumerable.Range(start, count);
        static IEnumerable<int> Range(int count) => Range(0, count);

        static void Main(string[] args)
        {
        }

        static void SaveTotalUniqueDegreeBySamplesHeatMapForGraphs(Graph[] graphs, int totalExperiments, int maxK, int maxBudget)
        {
            var rands = TSRandom.ArrayOfRandoms(totalExperiments);
            double[][][] costs = Range(maxBudget).Select(i => Range(maxK).Select(j => new double[totalExperiments]).ToArray()).ToArray();
            for (int currK = 1; currK <= maxK; currK++)
            {
                Parallel.For(0, totalExperiments, exp =>
                {
                   
                });
            }
        }
    }
}
