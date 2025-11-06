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
                typeof(string)
            },
            modifiers: null)
        ?? throw new InvalidOperationException("Unable to resolve ImportService.GetRequiredColumnIndex via reflection.");

    private static int InvokeGetRequiredColumnIndex(
        Dictionary<int, string> headerMap,
        IEnumerable<string> candidates,
        string friendlyName)
    {
        var result = GetRequiredColumnIndexMethod.Invoke(
            null,
            new object?[]
            {
                headerMap,
                candidates,
                friendlyName
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

        var result = InvokeGetRequiredColumnIndex(headerMap, candidates, "FYTG Backlog");

        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public void GetRequiredColumnIndex_FallsBackToPartialMatchWhenExactMissing()
    {
        var headerMap = new Dictionary<int, string>
        {
            [0] = "engagement id",
            [1] = "fytg backlog details"
        };

        var candidates = new[] { "fytg backlog", "fiscal year to go backlog" };

        var result = InvokeGetRequiredColumnIndex(headerMap, candidates, "FYTG Backlog");

        Assert.AreEqual(1, result);
    }

    [TestMethod]
    public void GetRequiredColumnIndex_ThrowsWhenColumnMissing()
    {
        var headerMap = new Dictionary<int, string>
        {
            [0] = "engagement id"
        };

        var candidates = new[] { "fytg backlog", "fiscal year to go backlog" };

        try
        {
            InvokeGetRequiredColumnIndex(headerMap, candidates, "FYTG Backlog");
            Assert.Fail("Expected InvalidDataException.");
        }
        catch (TargetInvocationException ex)
        {
            Assert.IsNotNull(ex.InnerException);
            Assert.IsInstanceOfType(ex.InnerException, typeof(InvalidDataException));
        }
    }
}
