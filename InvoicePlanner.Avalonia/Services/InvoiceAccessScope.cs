using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Persistence;
using Invoices.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvoicePlanner.Avalonia.Services;

public sealed class InvoiceAccessScope : IInvoiceAccessScope
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<InvoiceAccessScope> _logger;
    private readonly object _syncRoot = new();
    private HashSet<string> _engagementIds = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialised;

    public InvoiceAccessScope(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<InvoiceAccessScope> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string? Login { get; private set; }

    public IReadOnlySet<string> EngagementIds => _engagementIds;

    public bool HasAssignments => _engagementIds.Count > 0;

    public bool IsInitialized => _initialised;

    public string? InitializationError { get; private set; }

    public void EnsureInitialized()
    {
        if (_initialised)
        {
            return;
        }

        lock (_syncRoot)
        {
            if (_initialised)
            {
                return;
            }

            try
            {
                Login = ResolveLogin();

                if (string.IsNullOrWhiteSpace(Login))
                {
                    _logger.LogWarning("Unable to determine the operating system login; invoice data will be hidden.");
                    _engagementIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    using var context = _dbContextFactory.CreateDbContext();
                    var engagements = ResolveAccessibleEngagements(context, Login);
                    _engagementIds = new HashSet<string>(engagements, StringComparer.OrdinalIgnoreCase);

                    _logger.LogInformation(
                        "Invoice access scope initialised for {Login}. Accessible engagements: {Count}.",
                        Login,
                        _engagementIds.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialise the invoice access scope.");
                InitializationError = ex.Message;
                _engagementIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            finally
            {
                _initialised = true;
            }
        }
    }

    public bool IsEngagementAllowed(string? engagementId)
    {
        EnsureInitialized();

        if (string.IsNullOrWhiteSpace(engagementId))
        {
            return false;
        }

        if (!HasAssignments)
        {
            return false;
        }

        return _engagementIds.Contains(engagementId);
    }

    private static string? ResolveLogin()
    {
        try
        {
            var userName = Environment.UserName;
            if (string.IsNullOrWhiteSpace(userName))
            {
                return null;
            }

            string? domain = null;
            try
            {
                domain = Environment.UserDomainName;
            }
            catch (InvalidOperationException)
            {
            }
            catch (PlatformNotSupportedException)
            {
            }

            return string.IsNullOrWhiteSpace(domain)
                ? userName
                : $"{domain}\\{userName}";
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static IEnumerable<string> ResolveAccessibleEngagements(ApplicationDbContext context, string login)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (string.IsNullOrWhiteSpace(login))
        {
            return Array.Empty<string>();
        }

        var normalizedLogin = login.Trim();
        var today = DateTime.Today;
        var engagementIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var papdIds = context.Papds
            .AsNoTracking()
            .Where(p => p.WindowsLogin != null)
            .Select(p => new { p.Id, p.WindowsLogin })
            .ToList()
            .Where(p => string.Equals(p.WindowsLogin, normalizedLogin, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Id)
            .ToArray();

        if (papdIds.Length > 0)
        {
            var papdEngagements = (from assignment in context.EngagementPapds.AsNoTracking()
                                   join engagement in context.Engagements.AsNoTracking()
                                       on assignment.EngagementId equals engagement.Id
                                   where papdIds.Contains(assignment.PapdId)
                                   where assignment.EffectiveDate <= today
                                   select engagement.EngagementId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            foreach (var id in papdEngagements)
            {
                engagementIds.Add(id);
            }
        }

        var managerIds = context.Managers
            .AsNoTracking()
            .Where(m => m.WindowsLogin != null)
            .Select(m => new { m.Id, m.WindowsLogin })
            .ToList()
            .Where(m => string.Equals(m.WindowsLogin, normalizedLogin, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Id)
            .ToArray();

        if (managerIds.Length > 0)
        {
            var managerEngagements = (from assignment in context.EngagementManagerAssignments.AsNoTracking()
                                      join engagement in context.Engagements.AsNoTracking()
                                          on assignment.EngagementId equals engagement.Id
                                      where managerIds.Contains(assignment.ManagerId)
                                      where assignment.BeginDate <= today
                                      where !assignment.EndDate.HasValue || assignment.EndDate.Value >= today
                                      select engagement.EngagementId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            foreach (var id in managerEngagements)
            {
                engagementIds.Add(id);
            }
        }

        return engagementIds;
    }
}
