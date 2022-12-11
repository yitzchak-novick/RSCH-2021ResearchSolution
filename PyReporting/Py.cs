using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PyReporting
{
    public class Py
    {
        public static void RunPython(String pycode, int timeoutSeconds = 30)
        {
            string pyCodeFileName = $"tmpPy_{Guid.NewGuid().ToString()}_{Thread.CurrentThread.ManagedThreadId}.py";
            File.WriteAllText(pyCodeFileName, pycode.ToString());
            ProcessStartInfo procInfo = new ProcessStartInfo("py", pyCodeFileName);
            procInfo.UseShellExecute = false;
            procInfo.RedirectStandardOutput = true;
            procInfo.RedirectStandardError = true;
            Process proc = Process.Start(procInfo);
            String err = proc.StandardError.ReadToEnd();
            try
            {
                if (!string.IsNullOrWhiteSpace(err))
                {
                    throw new Exception("ERROR: " + err);
                }
                proc.WaitForExit(timeoutSeconds * 1000);
            }
            finally
            {
                try
                {
                    //File.Delete(pyCodeFileName);
                }
                catch { }
            }
        }

        public enum PlotType
        {
            scatter,
            plot
        }

        // A wrapper to allow both a single plot type and a single xVals vector
        public static void CreatePyPlot(PlotType plotType, double[] xVals, double[][] yVals, string[] labels = null, string[] colors = null,
           string title = null, string xlabel = null, string ylabel = null,
           string fileName = null, int timeoutSeconds = 30)
        {
            var plotTypesArray = Enumerable.Range(0, yVals.Length).Select(i => plotType).ToArray();
            CreatePyPlot(plotTypesArray, xVals, yVals, labels, colors, title, xlabel, ylabel, fileName, timeoutSeconds);
        }

        // A wrapper for when all plot lines are of the same plot type
        public static void CreatePyPlot(PlotType plotType, double[][] xVals, double[][] yVals, string[] labels = null, string[] colors = null,
           string title = null, string xlabel = null, string ylabel = null,
           string fileName = null, int timeoutSeconds = 30)
        {
            var plotTypesArray = Enumerable.Range(0, xVals.Length).Select(i => plotType).ToArray();
            CreatePyPlot(plotTypesArray, xVals, yVals, labels, colors, title, xlabel, ylabel, fileName, timeoutSeconds);
        }

        // A wrapper for when all xVal vectors are the same, accept it as a single array and create a multi-dimensional array repeating it as the elements
        public static void CreatePyPlot(PlotType[] plotType, double[] xVals, double[][] yVals, string[] labels = null, string[] colors = null,
            string title = null, string xlabel = null, string ylabel = null,
            string fileName = null, int timeoutSeconds = 30)
        {
            var xVals2d = Enumerable.Range(0, yVals.Length).Select(i => xVals).ToArray();
            CreatePyPlot(plotType, xVals2d, yVals, labels, colors, title, xlabel, ylabel, fileName, timeoutSeconds);
        }

        public static void CreatePyPlot(PlotType[] plotType, double[][] xVals, double[][] yVals, string[] labels = null, string[] colors = null, 
            string title = null, string xlabel = null, string ylabel = null,
            string fileName = null, int timeoutSeconds = 30)
        {
            StringBuilder pycode = new StringBuilder();
            pycode.AppendLine("import matplotlib.pyplot as plt");
            pycode.AppendLine("plt.clf()");
            for (int i = 0; i < yVals.Length; i++)
            {
                var xValsLength = xVals[i].Length; // TAKE THIS LINE OUT

                pycode.Append($"plt.{plotType[i]}({xVals[i].ToPyValList()}, {yVals[i].ToPyValList()}"); // PUT THIS BACK
                //pycode.Append($"plt.{plotType[i]}({xVals[i].Skip((int)(xValsLength * .01)).Take((int)(xValsLength * .98)).ToPyValList()}, {yVals[i].Skip((int)(xValsLength * .01)).Take((int)(xValsLength * .98)).ToPyValList()}"); // TAKE THIS OUT
                if (labels != null)
                    pycode.Append($", label='{labels[i]}'");
                if (colors != null)
                    pycode.Append($", color='{colors[i]}'");
                pycode.AppendLine(")");
            }
            if (title != null)
                pycode.AppendLine($"plt.title('{title}')");
            if (xlabel != null)
                pycode.AppendLine($"plt.xlabel('{xlabel}')");
            if (ylabel != null)
                pycode.AppendLine($"plt.ylabel('{ylabel}')");
            pycode.AppendLine("plt.legend()");
            if (fileName != null)
                pycode.AppendLine($"plt.savefig('{fileName}', pad_inches=0.3, bbox_inches='tight')");
            else
                pycode.AppendLine("plt.show()");
            RunPython(pycode.ToString());
        }

        public static void CreatePyHeatmap(
            double[][] values, string[] xticklabels = null, string[] yticklabels = null, string title = null, string xlabel = null, string ylabel = null, string fileName = null)
        {
            StringBuilder pycode = new StringBuilder();
            pycode.AppendLine("import numpy as np");
            pycode.AppendLine("import pandas as pd");
            pycode.AppendLine("import matplotlib");
            pycode.AppendLine("import matplotlib.pyplot as plt");
            pycode.AppendLine("import seaborn as sb");
            pycode.AppendLine();
            pycode.AppendLine("plt.clf()");


            // Another hardcoded line that has to be made into a param at some point, this sets the font size
            pycode.AppendLine("plt.rcParams.update({ 'font.size' : 16})");

            pycode.AppendLine();

            pycode.Append($"vals = pd.DataFrame(np.array([{String.Join(",\n                 ", values.Select(v => v.ToPyValList()))}])");
            if (xticklabels != null)
                pycode.Append($", columns=[{xticklabels.ToPyStrList()}]");
            if (yticklabels != null)
                pycode.Append($", index=[{yticklabels.ToPyStrList()}]");
            pycode.AppendLine(")");

            List<string> xtickmarks = new List<string>();
            for (decimal i = 0; i <= 1; i += .01m)
                xtickmarks.Add(new[] { 0m, .2m, .4m, .6m, .8m, 1 }.Contains(i) ? "'" + i.ToString(".#") + "'" : "' '");
            var ytickmarks = xtickmarks.ToList();
            ytickmarks.Reverse();

            // Hardcoding in color scheme, can come back to this
            // Also hardcoding in tickmarks for a very specific case, this really has to be made into a param if this code will be used again.
            pycode.Append($"sb.heatmap(vals, cmap='OrRd', xticklabels=[{string.Join(", ", xtickmarks)}], yticklabels=[{string.Join(", ", ytickmarks)}]");
            
            //pycode.Append($"{(xticklabels != null ? ", xticklabels=" + xticklabels.ToPyStrList() : "")}");
            //pycode.Append($"{(yticklabels != null ? ", yticklabels=" + yticklabels.ToPyStrList() : "")}");
            
            pycode.AppendLine(")");


            //pycode.AppendLine("plt.locator_params(axis='x', nbins=5)");
            //pycode.AppendLine("plt.locator_params(axis='y', nbins=5)");

            //pycode.AppendLine("plt.xticks([0, .2, .4, .6, .8, 1])");
            //pycode.AppendLine("plt.yticks([0, .2, .4, .6, .8, 1])");




            if (title != null)
                pycode.AppendLine($"plt.title('{title}')");
            if (xlabel != null)
                pycode.AppendLine($"plt.xlabel('{xlabel}')");
            if (ylabel != null)
                pycode.AppendLine($"plt.ylabel('{ylabel}')");
            if (fileName != null)
                pycode.AppendLine($"plt.savefig('{fileName}', pad_inches=0.3, bbox_inches='tight')");
            else
                pycode.AppendLine("plt.show()");
            RunPython(pycode.ToString());
        }
    }
}
