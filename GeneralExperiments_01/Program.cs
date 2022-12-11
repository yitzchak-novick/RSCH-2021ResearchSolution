using PyReporting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GeneralExperiments_01
{
    class Program
    {
        /* YN 6/28/21 - This is more of a general, almost sandbox, program. To start with, want to generate some charts from the
         * sampling results that were already accumulated in "TotalDegreesOverCostBySamplesTaken_02". But we may eventually just
         * run a few other simple experiments from here, we'll see. So no exact direction at the moment.
         */

        static readonly string RESULTS_DIR = @"C:\Users\Yitzchak\Desktop\Visual Studio 2021\2021Research_Sol01\TotalDegreesOverCostBySamplesTaken_02\bin\Debug\Results";
        static string GetFileName(String type, int n, int m) => $"{RESULTS_DIR}\\{type.ToUpper()}_N_{n}_M_{m}_results.tsv";

        static void Main(string[] args)
        {
            GetCriticalCsForGraphs_AllMeasures();
        }

        static void GetCriticalCsForGraphs_AllMeasures()
        {
            string preamble = "Calculating for what value of Cs do we get the same results for RN and for RVRN, if we fix Cv=Cn=1. Here we are " +
                "simply measuring total accumulated edges, so you would predict it will be -2RV/(RV-RN), the plots should be a flat line.";
            GetCriticalCsForGraphs(preamble, 53, 54, "CriticalCsAccumulatedEdgesResults");

            preamble = "Calculating for what value of Cs do we get the same results for RN and for RVRN, if we fix Cv=Cn=1. Here we are " +
                "measuring total UNIQUE accumulated edges, will see how it compares to the predicted -2RV/(RV-RN).";
            GetCriticalCsForGraphs(preamble, 60, 61, "CriticalCsAccumulatedUNQEdgesResults", true);

            preamble = "Calculating for what value of Cs do we get the same results for RN and for RVRN, if we fix Cv=Cn=1. Here we are " +
                "measuring max degree, will see how it compares to the predicted -2RV/(RV-RN).";
            GetCriticalCsForGraphs(preamble, 74, 75, "CriticalCsMaxDegreeResults");

            Console.WriteLine("Done");
            Console.ReadKey();
        }

        static void GetCriticalCsForGraphs(string preamble, int rvLineIndex, int rnLineIndex, string outputdir, bool onlyUniqueCs = false)
        {
            /* YN 6/28 - Will calculate a critical Cs, if we fix Cv=Cn=1, what Cs gives the exact same results for
             * RN and RVRN, in other words when is it worth it to keep the first vertex. This should be calculable
             * as -2RV/(RV-RN).
             */

            if (!Directory.Exists(outputdir))
                Directory.CreateDirectory(outputdir);

            var colHeaders = new[] { "n vals", "m=2", "m=5", "m=8", "m=15", "m=25", "m=50", "m=100" };
            var rowHeaders = new[] { "n=500", "n=1000", "n=2000", "n=5000", "n=7500" };

            var nVals = new[] { 500, 1000, 2000, 5000, 7500 };
            var mVals = new[] { 2, 5, 8, 15, 25, 50, 100 };


            foreach (var type in new[] { "ER", "BA" })
            {
                HtmlTableReporter html = new HtmlTableReporter(preamble, colHeaders, rowHeaders);
                foreach (var nVal in nVals)
                    foreach (var mVal in mVals)
                    {
                        var fileName = GetFileName(type, nVal, mVal);
                        if (File.Exists(fileName))
                        {
                            var fileLines = File.ReadAllLines(fileName);
                            var meta = fileLines[44].Substring(21).Split(';').ToDictionary(l => Regex.Split(l, @"\s+")[0], l => Regex.Split(l, @"\s+")[1]);
                            var rv = double.Parse(meta["RV"]);
                            var rn = double.Parse(meta["RN"]);
                            var prediction = -2*rv/(rv-rn);
                            var rvVals = Regex.Split(fileLines[rvLineIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
                            var rnVals = Regex.Split(fileLines[rnLineIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
                            double[] criticalVals = null;
                            if (!onlyUniqueCs)
                                criticalVals = Enumerable.Range(0, rvVals.Count).Select(i => 2*rvVals[i]/(rvVals[i] - rnVals[i])).ToArray();
                            else
                            {
                                // have to use the actual Cs costs that were paid
                                var rnCsCostsPaid = Regex.Split(fileLines[68], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
                                var rvrnCsCostsPaid = Regex.Split(fileLines[72], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
                                criticalVals = Enumerable.Range(0, rvVals.Count).Select(i => 2*rvVals[i]/((rvVals[i] - rnVals[i]))).ToArray();
                            }
                            var imgName = $"{type}_N_{nVal}_M_{mVal}.png";
                            Py.CreatePyPlot(Py.PlotType.plot, Enumerable.Range(0, rvVals.Count).Select(i => (double)i).ToArray(),
                                new[] {criticalVals.Select(i => prediction).ToArray(), criticalVals  }, new[] { "-2*rv/(rv-rn)","Critical Cs"  }, new[] { "r","b"  },
                                $"{type} N={nVal} M={mVal}", "Samples", "Critical Cs", $"{outputdir}\\{imgName}");
                            var text = new[] { $"RV: {rv}", $"RN: {rn}", $"Prediction: {prediction}" };
                            html.AddCell(text, imgName);
                        }
                        else
                        {
                            html.AddCell($"No file found for {type}, n={nVal}, m={mVal}", "");
                        }
                    }

                File.WriteAllText($"{outputdir}\\{type}_Results.html", html.GetTableHtml());
            }



        }

        static void GetCriticalCnForGraphs_AllMeasures()
        {
            string preamble = "Calculating for what value of Cn do we get the same results for RV and for RN. Here we are " +
                "simply measuring total accumulated edges, so you would predict it will be (RN-RV)/RV, the plots should be a flat line.";
            GetCriticalCnForGraphs(preamble, 53, 54, "AccumulatedEdgesResults");

            preamble = "Calculating for what value of Cn do we get the same results for RV and for RN. Here we are " +
                "measuring total UNIQUE accumulated edges. Will this change the prediction of (RN-RV)/RV?";
            GetCriticalCnForGraphs(preamble, 60, 61, "AccumulatedUNQEdgesResults");

            preamble = "Calculating for what value of Cn do we get the same results for RV and for RN. Here we are " +
                "measuring max degree of a collection. Will this change the prediction of (RN-RV)/RV?";
            GetCriticalCnForGraphs(preamble, 74, 75, "MaxDegreeResults");

            Console.WriteLine("Done");
            Console.ReadKey();
        }

        static void GetCriticalCnForGraphs(string preamble, int rvLineIndex, int rnLineIndex, string outputdir)
        {
            /* YN 6/28/21 - Will calculate for what Cn we would break even for graphs (start with ER and BA). For total degrees
             * accumulated, presumably this will be a simple calculation based on the difference between the expected degree of
             * RV and RN, so to start we will just chart this value for every value of accumulated degrees and see if we get a 
             * flat line.
             */

            if (!Directory.Exists(outputdir))
                Directory.CreateDirectory(outputdir);

            var colHeaders = new[] { "n vals", "m=2", "m=5", "m=8", "m=15", "m=25", "m=50", "m=100" };
            var rowHeaders = new[] { "n=500", "n=1000", "n=2000", "n=5000", "n=7500" };

            var nVals = new[] { 500, 1000, 2000, 5000, 7500 };
            var mVals = new[] { 2, 5, 8, 15, 25, 50, 100 };


            foreach (var type in new[] {"ER", "BA" })
            {
                HtmlTableReporter html = new HtmlTableReporter(preamble, colHeaders, rowHeaders);
                foreach (var nVal in nVals)
                    foreach (var mVal in mVals)
                    {
                        var fileName = GetFileName(type, nVal, mVal);
                        if (File.Exists(fileName))
                        {
                            var fileLines = File.ReadAllLines(fileName);
                            var meta = fileLines[44].Substring(21).Split(';').ToDictionary(l => Regex.Split(l, @"\s+")[0], l => Regex.Split(l, @"\s+")[1]);
                            var rv = double.Parse(meta["RV"]);
                            var rn = double.Parse(meta["RN"]);
                            var prediction = (rn - rv) / rv;
                            var rvVals = Regex.Split(fileLines[rvLineIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
                            var rnVals = Regex.Split(fileLines[rnLineIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
                            var criticalVals = Enumerable.Range(0, rvVals.Count).Select(i => (rnVals[i] - rvVals[i]) / rvVals[i]).ToArray();
                            var imgName = $"{type}_N_{nVal}_M_{mVal}.png";
                            Py.CreatePyPlot(Py.PlotType.plot, Enumerable.Range(0, rvVals.Count).Select(i => (double)i).ToArray(), 
                                new[] { criticalVals, criticalVals.Select(i => prediction).ToArray() }, new[] { "Critical Cn", "(RN-RV)/RV" }, new[] { "b", "r" },
                                $"{type} N={nVal} M={mVal}", "Samples", "Critical Cn", $"{outputdir}\\{imgName}");
                            var text = new[] { $"RV: {rv}", $"RN: {rn}", $"Prediction: {prediction}" };
                            html.AddCell(text, imgName);
                        }
                        else
                        {
                            html.AddCell($"No file found for {type}, n={nVal}, m={mVal}", "");
                        }
                    }

                File.WriteAllText($"{outputdir}\\{type}_Results.html", html.GetTableHtml());
            }


        }
    }
}
