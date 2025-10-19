using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Services.People;
using Invoices.Core.Enums;
using Invoices.Core.Models;
using Invoices.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GRCFinancialControl.Persistence.Tests;

public sealed class InvoicePlanRepositoryAccessTests : IDisposable
{
    private readonly SqliteDbContextFactory _factory = new();

    [Fact]
    public void GetPlan_ReturnsNull_WhenScopeHasNoAssignments()
    {
        var planId = SeedInvoicePlan("ENG-001");
        var repository = CreateRepository(FakeInvoiceAccessScope.Deny());

        var plan = repository.GetPlan(planId);

        Assert.Null(plan);
    }

    [Fact]
    public void GetPlan_ReturnsPlan_WhenScopeAllowsEngagement()
    {
        var planId = SeedInvoicePlan("ENG-ALLOW");
        var repository = CreateRepository(FakeInvoiceAccessScope.Allow("ENG-ALLOW"));

        var plan = repository.GetPlan(planId);

        Assert.NotNull(plan);
        Assert.Equal("ENG-ALLOW", plan!.EngagementId);
    }

    [Fact]
    public void ListEngagementsForPlanning_ReturnsEmpty_WhenScopeDenied()
    {
        SeedInvoicePlan("ENG-200");
        var repository = CreateRepository(FakeInvoiceAccessScope.Deny());

        var engagements = repository.ListEngagementsForPlanning();

        Assert.Empty(engagements);
    }

    [Fact]
    public void ListEngagementsForPlanning_ReturnsAssignments_WhenScopeAllows()
    {
        SeedInvoicePlan("ENG-300");
        var repository = CreateRepository(FakeInvoiceAccessScope.Allow("ENG-300"));

        var engagements = repository.ListEngagementsForPlanning();

        var engagement = Assert.Single(engagements);
        Assert.Equal("ENG-300", engagement.EngagementId);
    }

    private int SeedInvoicePlan(string engagementId)
    {
        _factory.Seed(context =>
        {
            var engagement = new Engagement
            {
                EngagementId = engagementId,
                Description = engagementId + " Description",
                Currency = "BRL",
            };

            context.Engagements.Add(engagement);

            context.InvoicePlans.Add(new InvoicePlan
            {
                EngagementId = engagementId,
                Type = InvoicePlanType.ByDate,
                NumInvoices = 1,
                PaymentTermDays = 30,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        });

        using var context = _factory.CreateDbContext();
        return context.InvoicePlans
            .AsNoTracking()
            .Where(plan => plan.EngagementId == engagementId)
            .Select(plan => plan.Id)
            .Single();
    }

    private InvoicePlanRepository CreateRepository(FakeInvoiceAccessScope accessScope)
    {
        return new InvoicePlanRepository(
            _factory,
            NullLogger<InvoicePlanRepository>.Instance,
            new NullPersonDirectory(),
            accessScope);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private sealed class SqliteDbContextFactory : IDbContextFactory<ApplicationDbContext>, IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public SqliteDbContextFactory()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = new ApplicationDbContext(_options);
            context.Database.EnsureCreated();
        }

        public ApplicationDbContext CreateDbContext()
        {
            return new ApplicationDbContext(_options);
        }

        public void Seed(Action<ApplicationDbContext> seeder)
        {
            using var context = new ApplicationDbContext(_options);
            seeder(context);
            context.SaveChanges();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }

    private sealed class FakeInvoiceAccessScope : IInvoiceAccessScope
    {
        private readonly HashSet<string> _engagements;
        private readonly bool _hasAssignments;
        private readonly string? _initializationError;
        private bool _initialised;

        private FakeInvoiceAccessScope(IEnumerable<string> engagements, bool hasAssignments, string? initializationError)
        {
            _engagements = new HashSet<string>(engagements ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            _hasAssignments = hasAssignments;
            _initializationError = initializationError;
        }

        public string? Login => "Test";

        public IReadOnlySet<string> EngagementIds => _engagements;

        public bool HasAssignments => _hasAssignments && _engagements.Count > 0;

        public bool IsInitialized => _initialised;

        public string? InitializationError => _initializationError;

        public void EnsureInitialized()
        {
            _initialised = true;
        }

        public bool IsEngagementAllowed(string? engagementId)
        {
            return !string.IsNullOrWhiteSpace(engagementId) && _engagements.Contains(engagementId);
        }

        public static FakeInvoiceAccessScope Allow(params string[] engagementIds)
        {
            return new FakeInvoiceAccessScope(engagementIds, hasAssignments: true, initializationError: null);
        }

        public static FakeInvoiceAccessScope Deny()
        {
            return new FakeInvoiceAccessScope(Array.Empty<string>(), hasAssignments: false, initializationError: null);
        }
    }
}
