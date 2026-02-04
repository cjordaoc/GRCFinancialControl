using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GRC.Shared.UI.Services
{
    /// <summary>
    /// Service for exporting data to tab-delimited text format.
    /// </summary>
    public sealed class TabDelimitedExportService : ITabDelimitedExportService
    {
        /// <summary>
        /// Exports the given headers and data rows to a tab-delimited text file.
        /// </summary>
        /// <param name="filePath">The destination file path.</param>
        /// <param name="headers">The column headers.</param>
        /// <param name="rows">The data rows, where each row is a collection of values.</param>
        /// <returns>A task representing the async export operation.</returns>
        public async Task ExportAsync(string filePath, IEnumerable<string> headers, IEnumerable<IEnumerable<string>> rows)
        {
            ArgumentNullException.ThrowIfNull(filePath);
            ArgumentNullException.ThrowIfNull(headers);
            ArgumentNullException.ThrowIfNull(rows);

            var sb = new StringBuilder();

            // Write headers
            var headerList = headers.ToList();
            if (headerList.Count > 0)
            {
                sb.AppendLine(string.Join("\t", headerList));
            }

            // Write data rows
            foreach (var row in rows)
            {
                var rowList = row?.ToList() ?? new List<string>();
                sb.AppendLine(string.Join("\t", rowList));
            }

            // Write to file
            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
        }
    }
}
