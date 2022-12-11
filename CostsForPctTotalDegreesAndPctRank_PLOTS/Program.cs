using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UtilsYN;

namespace CostsForPctTotalDegreesAndPctRank_PLOTS
{
    /* YN 2/6/2022 - The sister program of this program runs for a long time so making a separate program to generate some charts
     * on the results that have been recorded so far.
     */
    class Program
    {
        static string DTS => UtilsYN.Utils.DTS;
        static IEnumerable<int> Range(int start, int count) => Enumerable.Range(start, count);
        static IEnumerable<int> Range(int count) => Range(0, count);
        const String TOTDEG_RESULTS_FILE = @"C:\Users\Yitzchak\Desktop\Best K Value Temp\TotDegResults.csv";
        const String RANK_RESULTS_FILE = @"C:\Users\Yitzchak\Desktop\Best K Value Temp\RankResults.csv";

        public enum Metric
        {
            TD, RANK
        }

        public enum Method { RkN, RVkN}

        public enum Cost { Cv, Cn, Smp, Cs}

        static Dictionary<int, Dictionary<Cost, double[]>> RkN_TD_Costs = GetCostDictionary(Method.RkN, Metric.TD);
        static Dictionary<int, Dictionary<Cost, double[]>> RkN_Rank_Costs = GetCostDictionary(Method.RkN, Metric.RANK);
        static Dictionary<int, Dictionary<Cost, double[]>> RVkN_TD_Costs = GetCostDictionary(Method.RVkN, Metric.TD);
        static Dictionary<int, Dictionary<Cost, double[]>> RVkN_Rank_Costs = GetCostDictionary(Method.RVkN, Metric.RANK);


        static Dictionary<int, Dictionary<Cost, double[]>> GetCostDictionary(Method method, Metric metric)
        {

            var fileLines = File.ReadAllLines(metric == Metric.TD ? TOTDEG_RESULTS_FILE : RANK_RESULTS_FILE).Select(l => Regex.Split(l, ",")).ToList();
           
            Dictionary<int, Dictionary<Cost, double[]>> costs = new Dictionary<int, Dictionary<Cost, double[]>>();
            
            foreach (var fileLine in fileLines.Where(l => l[0].StartsWith(method.ToString())))
            {
                var currK = int.Parse(fileLine[0].Substring(method == Method.RkN ? 6 : 7));
                costs[currK] = new Dictionary<Cost, double[]>();
                costs[currK][Cost.Cv] = fileLine.Skip(1).Where((_, i) => i % 3 == 0).Select(v => double.Parse(v)).ToArray();
                costs[currK][Cost.Cn] = fileLine.Skip(1).Where((_, i) => i % 3 == 1).Select(v => double.Parse(v)).ToArray();
                costs[currK][Cost.Smp] = costs[currK][Cost.Cv].Select((CvCost, i) => CvCost + costs[currK][Cost.Cn][i]).ToArray();
                costs[currK][Cost.Cs] = fileLine.Skip(1).Where((_, i) => i % 3 == 2).Select(v => double.Parse(v)).ToArray();
            }

            return costs;
        }

        static void Main(string[] args)
        {
            var pcts = new[] { .15, .25, .75, .85 };
            PlotKValuesForCosts(pcts, 10);

            Console.ReadKey();

        }

        // YN 2/10/22 - Ran experiments for BA n=4000, m=3 and all possible k values for RkN and RVkN. This method will create a line plot
        // the x-axis will be increasing values of k, the y-axis the cost. Each plot should contain a few lines for different pcts of the graph.
        // We will need separate plots for Cv, Cn, Sampling, and Cs. So 2 metrics (pcttotdeg, rank) x 2 methods (RkN, RVkN), x 4 costs = 16 total charts?
        static void PlotKValuesForCosts(double[] pcts, int xAxisSize)
        {
            foreach (var method in new[] { Method.RkN, /*Method.RVkN*/ })
            {
                foreach(var metric in new[] { Metric.TD, /*Metric.RANK*/ })
                {
                    foreach(var cost in new[] { Cost.Cv, Cost.Cn, Cost.Smp, Cost.Cs })
                    {
                        var currDicitionary = method == Method.RkN ? (metric == Metric.RANK ? RkN_Rank_Costs : RkN_TD_Costs) :
                                                                      (metric == Metric.RANK ? RVkN_Rank_Costs : RVkN_TD_Costs);
                        var yVals = pcts
                            .Select(pct => currDicitionary.OrderBy(kvp => kvp.Key).Skip(0).Take(xAxisSize).Select(kvp => currDicitionary[kvp.Key][cost][(int)(100 * pct - 1)]).ToArray()).ToArray();

                        PyReporting.Py.CreatePyPlot(
                            PyReporting.Py.PlotType.plot,
                            Range(yVals[0].Length).Select(v => (double)v).ToArray(),
                            yVals,
                            pcts.Select(pct => pct.ToString()).ToArray(),
                            colors: null,
                            title: $"{method} {(metric == Metric.TD ? "Percent Total Unique Degrees" : "Percent High-Degree Vertices")} {cost}",
                            "K Values",
                            $"Total {cost} Cost"
                            );
                    }
                }
            }
        }

        /* Commenting out because it has to be rewritten to take the new type of dictionary so when we get to that I'll fix it bezrh
        static void HeatMapTry_01(Method method, Metric metric, String cost, int kMin = 2, int kMax = 100, int xRange = 100)
        {
            var currCostsCollection = GetCostDictionary(method, metric);


            double[][] totalCosts = new double[xRange][];
            for (int i = 0; i < xRange; i++)
            {
                totalCosts[i] = new double[xRange];
                decimal theoreticalKVal = kMin + (((kMax - kMin + 1) / (decimal)xRange) * i);
                decimal scalingVal = theoreticalKVal - (int)theoreticalKVal;
                for (int j = 0; j < xRange; j++)
                {                    
                    double prevKVal = 
                        cost != "SMP" ? 
                        currCostsCollection[(int)Math.Floor(theoreticalKVal)][cost][j] : 
                        currCostsCollection[(int)Math.Floor(theoreticalKVal)]["CV"][j] + currCostsCollection[(int)Math.Floor(theoreticalKVal)]["CN"][j];
                    double nextKVal =
                        cost != "SMP" ?
                        currCostsCollection[(int)Math.Ceiling(theoreticalKVal)][cost][j] :
                        currCostsCollection[(int)Math.Ceiling(theoreticalKVal)]["CV"][j] + currCostsCollection[(int)Math.Ceiling(theoreticalKVal)]["CN"][j];
                    double actualKVal = (nextKVal - prevKVal) * (double)scalingVal + prevKVal;
                    totalCosts[i][j] = actualKVal;
                }
            }

            PyReporting.Py.CreatePyHeatmap(
                totalCosts

                );
        }*/
    }
}
