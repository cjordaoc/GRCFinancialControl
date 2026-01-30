using System.Collections.Generic;
using System.Threading.Tasks;

namespace GRC.Shared.Core.Services
{
    /// <summary>
    /// Service for exporting data to tab-delimited text format.
    /// </summary>
    public interface ITabDelimitedExportService
    {
        /// <summary>
        /// Exports the given headers and data rows to a tab-delimited text file.
        /// </summary>
        /// <param name="filePath">The destination file path.</param>
        /// <param name="headers">The column headers.</param>
        /// <param name="rows">The data rows, where each row is a collection of values.</param>
        /// <returns>A task representing the async export operation.</returns>
        Task ExportAsync(string filePath, IEnumerable<string> headers, IEnumerable<IEnumerable<string>> rows);
    }
}
