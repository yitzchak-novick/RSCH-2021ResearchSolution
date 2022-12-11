using PyReporting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UtilsYN;

namespace CriticalCnCalpha_01
{
    /* YN 7/27/21 - Changed strategies from CCs to CAlpha instead, so have to rewrite the code for CAlpha based on the basic code that
     * we wrote for CCn. But probably still need to regenerate some CCn charts anyway, so will try to rewrite the whole thing and see if
     * anything else improves along with it. (Also, need to rename 'total edges' to 'total degrees'.)
     */

    class Program
    {
        static readonly string RESULTS_DIR = @"C:\Users\Yitzchak\Desktop\Visual Studio 2021\2021Research_Sol01\TotalDegreesOverCostBySamplesTaken_02\bin\Debug\Results";
        static string GetGraphResultsFileName(String type, int n, int m) => $"{RESULTS_DIR}\\{type.ToUpper()}_N_{n}_M_{m}_results.tsv";

        const bool debug = false;
        const int EXPERIMENTS = debug ? 1 : 400;
        const int GRAPHS = debug ? 1 : 100;
        static Random[] rands = TSRandom.ArrayOfRandoms(EXPERIMENTS);
        static string DTS => UtilsYN.Utils.DTS;
        static IEnumerable<int> Range(int start, int count) => Enumerable.Range(start, count);
        static IEnumerable<int> Range(int count) => Range(0, count);
        static string SYN_GRAPH_NAME(string type, int n, int m) => $"{type.ToUpper()}_N_{n}_M_{m}.png";

        const int RvTotalDegreesIndex = 53;
        const int RnTotalDegreesIndex = 54;
        const int RvnTotalDegreesIndex = 58;

        const int RvTotalUniqueDegreesIndex = 60;
        const int RnTotalUniqueDegreesIndex = 61;
        const int RvnTotalUniqueDegreesIndex = 65;

        const int RvMaxDegreeIndex = 74;
        const int RnMaxDegreeIndex = 75;
        const int RvnMaxDegreeIndex = 79;

        const int RnTotalSelectionsIndex = 68;
        const int RvnTotalSelectionsIndex = 72;

        const int MetaFileLineIndex = 44;

        const double TAKE_PCT = 0.98;

        enum ReportType
        {
            CCnBySamples,
            CCnByResults,
            CAlphaBySamples,
            CAlphaByResults
        }

        static void Main(string[] args)
        {
            LazyMethodToCreateCAlphaImages();
        }

        static void LazyMethodToCreateCAlphaImages()
        {
            var nVals = new[] { 500, 1000, 2000, 5000, 7500 };
            var mVals = new[] { 2, 5, 8, 15, 25, 50, 100 };

            var mnTuples = Range(mVals.Length).SelectMany(mIndx => Range(nVals.Length).Select(nIndx => new Tuple<int, int>(mIndx, nIndx)))
                .OrderBy(t => Math.Max(t.Item1, t.Item2)).ThenBy(t => Math.Min(t.Item1, t.Item2)).ToList();

            foreach (var type in new[] { "ER", "BA" })
            {
                Console.WriteLine($"Starting type: {type} {DTS}");
                foreach (var d in new[] { 1.0, .33 })
                {
                    Console.WriteLine($"Starting d: {d} {DTS}");
                    Parallel.ForEach(mnTuples, mnTuple =>
                    {
                        var mVal = mVals[mnTuple.Item1];
                        var nVal = nVals[mnTuple.Item2];

                        var byTotDegSamplesStrings = CreateCAlphaBySamplesCell(GetGraphResultsFileName(type, nVal, mVal), RvnTotalDegreesIndex, RnTotalDegreesIndex,
                            $"{type}_n{nVal}m{mVal}TotDegBySamp({d:0.0#}pct).png", $"{type} N={nVal} M={mVal}", false);
                        var byTotUnqDegSamplesStrings = CreateCAlphaBySamplesCell(GetGraphResultsFileName(type, nVal, mVal), RvnTotalUniqueDegreesIndex, RnTotalUniqueDegreesIndex,
                            $"{type}_n{nVal}m{mVal}TotUnqDegBySamp({d:0.0#}pct).png", $"{type} N={nVal} M={mVal}", false);
                        var byMaxDegSamplesStrings = CreateCAlphaBySamplesCell(GetGraphResultsFileName(type, nVal, mVal), RvnMaxDegreeIndex, RnMaxDegreeIndex,
                            $"{type}_n{nVal}m{mVal}MaxDegBySamp({d:0.0#}pct).png", $"{type} N={nVal} M={mVal}", false);

                        if (d == 1.0)
                        {
                            var byTotDegResultStrings = CreateCAlphaByResultsCell(GetGraphResultsFileName(type, nVal, mVal), RvnTotalDegreesIndex, RnTotalDegreesIndex,
                                $"{type}_n{nVal}m{mVal}TotDegByRes.png", $"{type} N={nVal} M={mVal}", false);
                            var byTotUnqDegSamplesResult = CreateCAlphaByResultsCell(GetGraphResultsFileName(type, nVal, mVal), RvnTotalUniqueDegreesIndex, RnTotalUniqueDegreesIndex,
                                $"{type}_n{nVal}m{mVal}TotUnqDegByRes.png", $"{type} N={nVal} M={mVal}", false);
                            var byMaxDegSamplesResult = CreateCAlphaByResultsCell(GetGraphResultsFileName(type, nVal, mVal), RvnMaxDegreeIndex, RnMaxDegreeIndex,
                                $"{type}_n{nVal}m{mVal}MaxDegByRes.png", $"{type} N={nVal} M={mVal}", false);
                        }
                    });
                }
            }
        }

        public static String[] CreateCAlphaBySamplesCell(String graphFileFullName, int rvnLineIndex, int rnLineIndex, String imgOutputFullDirAndName, String graphDesc, bool onlySelections, double pctSamples = 1.0)
        {
            var fileLines = File.ReadAllLines(graphFileFullName);
            var meta = fileLines[MetaFileLineIndex].Substring(21).Split(';').ToDictionary(l => Regex.Split(l, @"\s+")[0], l => Regex.Split(l, @"\s+")[1]);
            var rv = double.Parse(meta["RV"]);
            var rn = double.Parse(meta["RN"]);
            var prediction = (rn/rv)-1;
            var rvnVals = Regex.Split(fileLines[rvnLineIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
            var rnVals = Regex.Split(fileLines[rnLineIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
            rvnVals = rvnVals.Take((int)(rvnVals.Count * pctSamples)).ToList();
            rnVals = rnVals.Take((int)(rnVals.Count * pctSamples)).ToList();
            var rvnSelections = Regex.Split(fileLines[RvnTotalSelectionsIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
            var rnSelections = Regex.Split(fileLines[RnTotalSelectionsIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();

            List<double> criticalCAlphaSamplesTaken = new List<double>();
            List<double> undefinedCAlphaSamplesTaken = new List<double>();

            List<double> criticalCAlphaValues = new List<double>();

            for (int i = 0; i < rvnVals.Count; i++)
            {
                var criticalCAlphaValue = (2 * (rnVals[i] - rvnVals[i])) / (2 * rvnVals[i] - rnVals[i]);

                if (Double.IsInfinity(criticalCAlphaValue) || Double.IsNaN(criticalCAlphaValue))
                    undefinedCAlphaSamplesTaken.Add(i + 1);
                else
                {
                    criticalCAlphaSamplesTaken.Add(i + 1);
                    criticalCAlphaValues.Add(criticalCAlphaValue);
                }
            }

            var midRange = (criticalCAlphaValues.Max() - criticalCAlphaValues.Min()) / 2.0 + criticalCAlphaValues.Min();

            string resultDesc = rvnLineIndex == RvnTotalDegreesIndex ? "Degrees" : rvnLineIndex == RvnTotalUniqueDegreesIndex ? "Unique Degrees" : "Max Degree";

            var rnValsARR = rnVals.Take((int)(rnVals.Count() * TAKE_PCT)).ToArray();
            var criticalCAlphaSamplesTakenARR = criticalCAlphaSamplesTaken.Take((int)(criticalCAlphaSamplesTaken.Count() * TAKE_PCT)).ToArray();
            var criticalCAlphaValuesARR = criticalCAlphaValues.Take(criticalCAlphaSamplesTakenARR.Length).ToArray();
            var undefinedCAlphaSamplesTakenARR = undefinedCAlphaSamplesTaken.Take((int)(undefinedCAlphaSamplesTaken.Count() * TAKE_PCT)).ToArray();

            Py.CreatePyPlot(
                new[] { Py.PlotType.plot, Py.PlotType.plot, Py.PlotType.scatter },
                new[] { Enumerable.Range(1, rnValsARR.Length).Select(i => (double)i).ToArray(), criticalCAlphaSamplesTakenARR, undefinedCAlphaSamplesTakenARR },
                new[] { Enumerable.Range(1, rnValsARR.Length).Select(i => prediction).ToArray(), criticalCAlphaValuesARR, undefinedCAlphaSamplesTakenARR.Select(i => midRange).ToArray() },
                new[] { "1 - (E[RV] / E[RN])", "Critical Alpha", "Undefined Critical Alpha" }, new[] { "r", "b", "g" },
                $"{graphDesc} - Critical Alpha ({resultDesc}) by Samples", "Samples", $"Critical Alpha ({resultDesc})", imgOutputFullDirAndName);
            return new[] { $"RV: {rv}", $"RN: {rn}", $"Prediction: {prediction}" };
        }

        public static string[] CreateCAlphaByResultsCell(
            String graphFileFullName, int rvnLineIndex, int rnLineIndex, String imgOutputFullDirAndName, String graphDesc, bool onlySelections, bool usePercentOfMaxOfLine = false)
        {
            var fileLines = File.ReadAllLines(graphFileFullName);
            var meta = fileLines[MetaFileLineIndex].Substring(21).Split(';').ToDictionary(l => Regex.Split(l, @"\s+")[0], l => Regex.Split(l, @"\s+")[1]);
            var rv = double.Parse(meta["RV"]);
            var rn = double.Parse(meta["RN"]);
            var prediction = 1 - (rv / rn);

            var rvnVals = Regex.Split(fileLines[rvnLineIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
            var rnVals = Regex.Split(fileLines[rnLineIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
            if (usePercentOfMaxOfLine)
            {
                var maxRnLineVal = rnVals.Max();
                var maxRvnLineVal = rvnVals.Max();
                rnVals = rnVals.Select(v => v / maxRnLineVal).ToList();
                rvnVals = rvnVals.Select(v => v / maxRvnLineVal).ToList();
            }
            var rnSelections = Regex.Split(fileLines[RnTotalSelectionsIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();
            var rvnSelections = Regex.Split(fileLines[RvnTotalSelectionsIndex], @"\s+").Skip(1).Select(s => double.Parse(s)).ToList();

            List<double> definedResultValues = new List<double>();
            List<double> undefinedResultValues = new List<double>();
            List<double> allResultValues = new List<double>();

            List<double> definedCAlphaValues = new List<double>();

            var currRvnIndex = 0;
            var maxRvnVal = rvnVals.Max();
            var maxRnVal = rnVals.Max();
            var prevRnVal = double.MinValue;

            for (int currRnIndex = 0; currRnIndex < rnVals.Count; currRnIndex++)
            {
                var currRnVal = rnVals[currRnIndex];
                if (currRnVal == prevRnVal)
                    continue;
                prevRnVal = currRnVal;

                if (currRnVal > maxRvnVal)
                    break;
                var currRvnVal = rvnVals[currRvnIndex];
                while (currRvnVal < currRnVal)
                    currRvnVal = rvnVals[++currRvnIndex];

                var currCAlpha = 2.0 * (currRvnIndex - currRnIndex) / (2.0 * currRnIndex - currRvnIndex);

                allResultValues.Add(currRnVal);

                if (Double.IsInfinity(currCAlpha) || Double.IsNaN(currCAlpha))
                    undefinedResultValues.Add(currRnVal);
                else
                {
                    definedResultValues.Add(currRnVal);
                    definedCAlphaValues.Add(currCAlpha);
                }
            }

            var midRange = (definedCAlphaValues.Max() - definedCAlphaValues.Min()) / 2.0 + definedCAlphaValues.Min();

            string resultDesc = rvnLineIndex == RvnTotalDegreesIndex ? "Degrees" : rvnLineIndex == RvnTotalUniqueDegreesIndex ? "Unique Degrees" : "Max Degree";

            var allResultValuesARR = allResultValues.Take((int)(allResultValues.Count() * TAKE_PCT)).ToArray();
            var definedResultsARR = definedResultValues.Take((int)(definedResultValues.Count() * TAKE_PCT)).ToArray();
            var definedCAlphaValuesARR = definedCAlphaValues.Take(definedResultsARR.Length).ToArray();
            var undefinedResultsARR = undefinedResultValues.Take((int)(undefinedResultValues.Count() * TAKE_PCT)).ToArray();


            Py.CreatePyPlot(
                new[] { Py.PlotType.plot, Py.PlotType.plot, Py.PlotType.scatter },
                new[] { allResultValuesARR, definedResultsARR, undefinedResultsARR },
                new[] { allResultValuesARR.Select(i => prediction).ToArray(), definedCAlphaValuesARR, undefinedResultsARR.Select(d => midRange).ToArray() },
                new[] { "1 - (E[RV] / E[RN])", "Critical Alpha", "Undefined Critical Alpha" }, new[] { "r", "b", "g" },
                $"{graphDesc} - Critical Alpha by {resultDesc}", resultDesc, "Critical Alpha", imgOutputFullDirAndName);
            return new[] { $"RV: {rv}", $"RN: {rn}", $"Prediction: {prediction}" };
        }
    }
}
