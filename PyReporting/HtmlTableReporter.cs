using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PyReporting
{
    public class HtmlTableReporter
    {
        public class Cell
        {
            public List<String> textLines = new List<string>();
            public string imageFileName = "";

            public Cell() { }

            public Cell(IEnumerable<string> textLines, string imgFileName)
            {
                this.textLines = textLines.ToList();
                imageFileName = imgFileName;
            }

            public Cell(string textLine, string imgFileName) : this(new[] { textLine }, imgFileName) { }

            public void AddTextLine(string text) => textLines.Add(text);
            public void SetImageFileName(string imgFileName) => imageFileName = imgFileName;

            public String AsHtml()
            {
                string html = "<td><p><center>";
                textLines.ForEach(l => html += l + "<br />");
                html += "</center></p>";
                html += $"<img src='{imageFileName}' />";
                html += "</td>";
                return html;
            }
        }

        private string Preamble;
        private string[] colHeaders;
        private string[] rowHeaders;
        private List<Cell> htmlCells = new List<Cell>();

        public HtmlTableReporter(string preamble, string[] colHeaders, string[] rowHeaders)
        {
            Preamble = preamble;
            this.colHeaders = colHeaders;
            this.rowHeaders = rowHeaders;
        }

        public void AddCell(Cell cell) => htmlCells.Add(cell);

        public void AddCell(IEnumerable<string> textLines, string imgFileName) => AddCell(new Cell(textLines, imgFileName));

        public void AddCell(string textLine, string imgFileName) => AddCell(new Cell(textLine, imgFileName));

        public string GetTableHtml()
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine($"<h1>{Preamble}</h1>");
            html.AppendLine("<table>");

            // Enter the column headers
            html.AppendLine("\t<tr>");
            foreach (var colHeader in colHeaders)
                html.AppendLine($"\t\t<th>{colHeader}</th>");
            html.AppendLine("\t</tr>");

            int i = 0;
            foreach(var cell in htmlCells)
            {
                if (i % (colHeaders.Length - 1) == 0)
                {
                    if (i != 0)
                        html.AppendLine($"\t</tr>");
                    html.AppendLine("\t<tr>");
                    html.AppendLine($"\t\t<th>{rowHeaders[i / (colHeaders.Length - 1)]}</th>");
                }
                html.AppendLine($"\t\t{cell.AsHtml()}");
                i++;
            }


            html.AppendLine("\t</tr>");
            html.AppendLine("</table>");

            return html.ToString();
        }
    }
}
