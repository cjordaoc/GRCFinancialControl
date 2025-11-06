using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GRCFinancialControl.Persistence.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GRCFinancialControl.Avalonia.Tests;

[TestClass]
public class ImportServiceBacklogTests
{
    private static readonly MethodInfo GetRequiredColumnIndexMethod = typeof(ImportService)
        .GetMethod(
            "GetRequiredColumnIndex",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[]
            {
                typeof(Dictionary<int, string>),
                typeof(IEnumerable<string>),
                typeof(string),
                typeof(Func<string, bool>)
            },
            modifiers: null)
        ?? throw new InvalidOperationException("Unable to resolve ImportService.GetRequiredColumnIndex via reflection.");

    private static int InvokeGetRequiredColumnIndex(
        Dictionary<int, string> headerMap,
        IEnumerable<string> candidates,
        string friendlyName,
        Func<string, bool> predicate)
    {
        var result = GetRequiredColumnIndexMethod.Invoke(
            null,
            new object?[]
            {
                headerMap,
                candidates,
                friendlyName,
                predicate
            });

        return (int)result!;
    }

    [TestMethod]
    public void GetRequiredColumnIndex_PrefersLocalCurrencyBacklogColumn()
    {
        var headerMap = new Dictionary<int, string>
        {
            [0] = "engagement id",
            [1] = "fytg backlog (opp currency)",
            [2] = "fytg backlog (r$)",
            [3] = "future fy backlog"
        };

        var candidates = new[] { "fytg backlog", "fiscal year to go backlog" };
        Func<string, bool> filter = header => !header.Contains("opp currency", StringComparison.Ordinal) && !header.Contains("lead", StringComparison.Ordinal);

        var result = InvokeGetRequiredColumnIndex(headerMap, candidates, "FYTG Backlog", filter);

        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public void GetRequiredColumnIndex_ThrowsWhenOnlyUnsupportedFormatsExist()
    {
        var headerMap = new Dictionary<int, string>
        {
            [0] = "engagement id",
            [1] = "fytg backlog (opp currency)"
        };

        var candidates = new[] { "fytg backlog", "fiscal year to go backlog" };
        Func<string, bool> filter = header => !header.Contains("opp currency", StringComparison.Ordinal) && !header.Contains("lead", StringComparison.Ordinal);

        try
        {
            InvokeGetRequiredColumnIndex(headerMap, candidates, "FYTG Backlog", filter);
            Assert.Fail("Expected the invocation to throw an InvalidDataException.");
        }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidDataException)
        {
            // Expected path.
        }
    }
}
