using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace App.Presentation.Services
{
    /// <summary>
    /// Provides cached access to the embedded README markdown content.
    /// </summary>
    public static class ReadmeContentProvider
    {
        private static readonly object SyncLock = new();
        private static readonly Dictionary<string, Task<string?>> Cache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Retrieves the README markdown content embedded in the provided assembly.
        /// </summary>
        /// <param name="assembly">Assembly that contains the embedded README resource.</param>
        /// <returns>A task resolving with the markdown content, or <c>null</c> if not found.</returns>
        public static Task<string?> GetAsync(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            var key = assembly.FullName ?? assembly.GetName().Name ?? "default";

            lock (SyncLock)
            {
                if (!Cache.TryGetValue(key, out var cached))
                {
                    cached = Task.Run(() => LoadAsync(assembly));
                    Cache[key] = cached;
                }

                return cached;
            }
        }

        private static string? LoadAsync(Assembly assembly)
        {
            var resourceName = assembly
                .GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith("README.md", StringComparison.OrdinalIgnoreCase));

            if (resourceName is null)
            {
                return null;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                return null;
            }

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
