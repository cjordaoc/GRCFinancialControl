using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence;
using Invoices.Core.Enums;
using Invoices.Core.Models;
using Invoices.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Invoices.Data.Tests;

public class InvoicePlanRepositoryTests
{
    [Fact]
    public void SavePlanAndReload_PersistsItemsAndEmails()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new ApplicationDbContext(options))
        {
            context.Database.EnsureCreated();
        }

        var factory = new TestDbContextFactory(options);
        var repository = new InvoicePlanRepository(factory, NullLogger<InvoicePlanRepository>.Instance);

        var plan = new InvoicePlan
        {
            EngagementId = "ENG-001",
            Type = InvoicePlanType.ByDate,
            NumInvoices = 2,
            PaymentTermDays = 30,
            CustomerFocalPointName = "Alice Example",
            CustomerFocalPointEmail = "alice@example.com",
            FirstEmissionDate = new DateTime(2025, 1, 15),
        };

        plan.Items.Add(new InvoiceItem
        {
            SeqNo = 1,
            Percentage = 50m,
            Amount = 5000m,
            PayerCnpj = "12345678901234",
            EmissionDate = new DateTime(2025, 1, 15),
            DueDate = new DateTime(2025, 2, 14),
        });

        plan.Items.Add(new InvoiceItem
        {
            SeqNo = 2,
            Percentage = 50m,
            Amount = 5000m,
            PayerCnpj = "12345678901234",
            EmissionDate = new DateTime(2025, 2, 15),
            DueDate = new DateTime(2025, 3, 17),
        });

        plan.AdditionalEmails.Add(new InvoicePlanEmail
        {
            Email = "cc@example.com",
        });

        var saveResult = repository.SavePlan(plan);

        Assert.Equal(1, saveResult.Created);
        Assert.Equal(0, saveResult.Updated);

        var reloaded = Assert.Single(repository.ListPlansForEngagement("ENG-001"));
        Assert.Equal(2, reloaded.Items.Count);
        Assert.Single(reloaded.AdditionalEmails);
        Assert.Equal(10000m, reloaded.Items.Sum(item => item.Amount));

        var updatedPlan = new InvoicePlan
        {
            Id = reloaded.Id,
            EngagementId = reloaded.EngagementId,
            Type = InvoicePlanType.ByDelivery,
            NumInvoices = 2,
            PaymentTermDays = 45,
            CustomerFocalPointName = reloaded.CustomerFocalPointName,
            CustomerFocalPointEmail = reloaded.CustomerFocalPointEmail,
            CustomInstructions = "Updated",
            FirstEmissionDate = reloaded.FirstEmissionDate,
        };

        var existingItem = reloaded.Items.OrderBy(item => item.SeqNo).First();
        updatedPlan.Items.Add(new InvoiceItem
        {
            Id = existingItem.Id,
            SeqNo = 1,
            Percentage = 60m,
            Amount = 6000m,
            EmissionDate = existingItem.EmissionDate,
            DueDate = existingItem.EmissionDate?.AddDays(45),
            DeliveryDescription = "Kick-off",
            PayerCnpj = existingItem.PayerCnpj,
        });

        updatedPlan.Items.Add(new InvoiceItem
        {
            SeqNo = 2,
            Percentage = 40m,
            Amount = 4000m,
            EmissionDate = existingItem.EmissionDate?.AddMonths(1),
            DueDate = existingItem.EmissionDate?.AddMonths(1).AddDays(45),
            DeliveryDescription = "Wrap-up",
            PayerCnpj = "98765432100000",
        });

        var existingEmail = reloaded.AdditionalEmails.Single();
        updatedPlan.AdditionalEmails.Add(new InvoicePlanEmail
        {
            Id = existingEmail.Id,
            Email = "updated@example.com",
        });

        var updateResult = repository.SavePlan(updatedPlan);

        Assert.Equal(1, updateResult.Updated);

        var afterUpdate = repository.GetPlan(reloaded.Id) ?? throw new InvalidOperationException("Plan not found");
        Assert.Equal(2, afterUpdate.Items.Count);
        Assert.Equal(10000m, afterUpdate.Items.Sum(item => item.Amount));
        Assert.All(afterUpdate.Items, item => Assert.Equal(InvoiceItemStatus.Planned, item.Status));
        Assert.Equal("updated@example.com", afterUpdate.AdditionalEmails.Single().Email);

        using (var verification = factory.CreateDbContext())
        {
            var persisted = verification.InvoicePlans
                .Include(p => p.Items)
                .Single(p => p.Id == reloaded.Id);

            Assert.Equal(2, persisted.Items.Count);
        }
    }

    [Fact]
    public void RequestAndUndo_FlowsBetweenPlannedAndRequested()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new ApplicationDbContext(options))
        {
            context.Database.EnsureCreated();
        }

        var factory = new TestDbContextFactory(options);
        var repository = new InvoicePlanRepository(factory, NullLogger<InvoicePlanRepository>.Instance);

        var plan = new InvoicePlan
        {
            EngagementId = "ENG-REQ",
            Type = InvoicePlanType.ByDate,
            NumInvoices = 1,
            PaymentTermDays = 30,
            CustomerFocalPointName = "Requester",
            CustomerFocalPointEmail = "requester@example.com",
            FirstEmissionDate = new DateTime(2025, 3, 1),
        };

        plan.Items.Add(new InvoiceItem
        {
            SeqNo = 1,
            Percentage = 100m,
            Amount = 1000m,
            PayerCnpj = "11111111000199",
            EmissionDate = new DateTime(2025, 3, 1),
            DueDate = new DateTime(2025, 3, 31),
        });

        repository.SavePlan(plan);

        var persistedPlan = repository.ListPlansForEngagement("ENG-REQ").Single();
        var persistedItem = persistedPlan.Items.Single();

        var requestDate = new DateTime(2025, 3, 5);
        var requestUpdate = new InvoiceRequestUpdate
        {
            ItemId = persistedItem.Id,
            RitmNumber = "RITM123456",
            CoeResponsible = "Maria Manager",
            RequestDate = requestDate,
        };

        var requestResult = repository.MarkItemsAsRequested(persistedPlan.Id, new[] { requestUpdate });

        Assert.Equal(1, requestResult.Updated);

        var requestedPlan = repository.GetPlan(persistedPlan.Id) ?? throw new InvalidOperationException("Plan not found");
        var requestedItem = requestedPlan.Items.Single();

        Assert.Equal(InvoiceItemStatus.Requested, requestedItem.Status);
        Assert.Equal("RITM123456", requestedItem.RitmNumber);
        Assert.Equal("Maria Manager", requestedItem.CoeResponsible);
        Assert.Equal(requestDate.Date, requestedItem.RequestDate);

        var undoResult = repository.UndoRequest(persistedPlan.Id, new[] { requestedItem.Id });

        Assert.Equal(1, undoResult.Updated);

        var afterUndo = repository.GetPlan(persistedPlan.Id) ?? throw new InvalidOperationException("Plan not found");
        var lineAfterUndo = afterUndo.Items.Single();

        Assert.Equal(InvoiceItemStatus.Planned, lineAfterUndo.Status);
        Assert.Null(lineAfterUndo.RitmNumber);
        Assert.Null(lineAfterUndo.CoeResponsible);
        Assert.Null(lineAfterUndo.RequestDate);
    }

    [Fact]
    public void CloseRequestedItems_MovesToClosed()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new ApplicationDbContext(options))
        {
            context.Database.EnsureCreated();
        }

        var factory = new TestDbContextFactory(options);
        var repository = new InvoicePlanRepository(factory, NullLogger<InvoicePlanRepository>.Instance);

        var plan = new InvoicePlan
        {
            EngagementId = "ENG-EMIT",
            Type = InvoicePlanType.ByDate,
            NumInvoices = 1,
            PaymentTermDays = 30,
            CustomerFocalPointName = "Emitter",
            CustomerFocalPointEmail = "emit@example.com",
            FirstEmissionDate = new DateTime(2025, 4, 1),
        };

        plan.Items.Add(new InvoiceItem
        {
            SeqNo = 1,
            Percentage = 100m,
            Amount = 5000m,
            PayerCnpj = "99999999000100",
            EmissionDate = new DateTime(2025, 4, 1),
            DueDate = new DateTime(2025, 5, 1),
        });

        repository.SavePlan(plan);

        var persistedPlan = repository.ListPlansForEngagement("ENG-EMIT").Single();
        var item = persistedPlan.Items.Single();

        repository.MarkItemsAsRequested(persistedPlan.Id, new[]
        {
            new InvoiceRequestUpdate
            {
                ItemId = item.Id,
                RitmNumber = "RITM-EMIT",
                CoeResponsible = "Carlos",
                RequestDate = new DateTime(2025, 4, 5),
            },
        });

        var emittedOn = new DateTime(2025, 4, 20);

        var result = repository.CloseItems(persistedPlan.Id, new[]
        {
            new InvoiceEmissionUpdate
            {
                ItemId = item.Id,
                BzCode = "BZ-1234",
                EmittedAt = emittedOn,
            },
        });

        Assert.Equal(1, result.Updated);

        var closedPlan = repository.GetPlan(persistedPlan.Id) ?? throw new InvalidOperationException("Plan not found");
        var closedItem = closedPlan.Items.Single();

        Assert.Equal(InvoiceItemStatus.Closed, closedItem.Status);
        Assert.Equal("BZ-1234", closedItem.BzCode);
        Assert.Equal(emittedOn.Date, closedItem.EmittedAt);
        Assert.Equal("RITM-EMIT", closedItem.RitmNumber);
    }

    [Fact]
    public void CancelAndReissue_AddsPlannedReplacement()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new ApplicationDbContext(options))
        {
            context.Database.EnsureCreated();
        }

        var factory = new TestDbContextFactory(options);
        var repository = new InvoicePlanRepository(factory, NullLogger<InvoicePlanRepository>.Instance);

        var plan = new InvoicePlan
        {
            EngagementId = "ENG-CAN",
            Type = InvoicePlanType.ByDate,
            NumInvoices = 1,
            PaymentTermDays = 45,
            CustomerFocalPointName = "Canceller",
            CustomerFocalPointEmail = "cancel@example.com",
            FirstEmissionDate = new DateTime(2025, 6, 1),
        };

        plan.Items.Add(new InvoiceItem
        {
            SeqNo = 1,
            Percentage = 100m,
            Amount = 12000m,
            PayerCnpj = "88888888000100",
            EmissionDate = new DateTime(2025, 6, 1),
            DueDate = new DateTime(2025, 7, 16),
        });

        repository.SavePlan(plan);

        var persistedPlan = repository.ListPlansForEngagement("ENG-CAN").Single();
        var item = persistedPlan.Items.Single();

        repository.MarkItemsAsRequested(persistedPlan.Id, new[]
        {
            new InvoiceRequestUpdate
            {
                ItemId = item.Id,
                RitmNumber = "RITM-CAN",
                CoeResponsible = "Luana",
                RequestDate = new DateTime(2025, 6, 3),
            },
        });

        var replacementEmission = new DateTime(2025, 7, 10);

        var result = repository.CancelAndReissue(persistedPlan.Id, new[]
        {
            new InvoiceReissueRequest
            {
                ItemId = item.Id,
                CancelReason = "Client asked for changes",
                ReplacementEmissionDate = replacementEmission,
            },
        });

        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Updated);

        var reissuedPlan = repository.GetPlan(persistedPlan.Id) ?? throw new InvalidOperationException("Plan not found");
        Assert.Equal(2, reissuedPlan.Items.Count);

        var canceledItem = reissuedPlan.Items.Single(i => i.Id == item.Id);
        var replacementItem = reissuedPlan.Items.Single(i => i.Id != item.Id);

        Assert.Equal(InvoiceItemStatus.Canceled, canceledItem.Status);
        Assert.Equal("Client asked for changes", canceledItem.CancelReason);
        Assert.NotNull(canceledItem.ReplacementItemId);
        Assert.Equal(replacementItem.Id, canceledItem.ReplacementItemId);

        Assert.Equal(InvoiceItemStatus.Planned, replacementItem.Status);
        Assert.Equal(item.Amount, replacementItem.Amount);
        Assert.Equal(replacementEmission.Date, replacementItem.EmissionDate);
        Assert.Equal(replacementEmission.Date.AddDays(persistedPlan.PaymentTermDays), replacementItem.DueDate);
        Assert.Null(replacementItem.RitmNumber);
        Assert.Equal(replacementItem.Id, canceledItem.ReplacementItem?.Id);
    }

    [Fact]
    public void SearchSummary_ReturnsGroupedTotalsAndSupportsFilters()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new ApplicationDbContext(options))
        {
            context.Database.EnsureCreated();
        }

        int customerOneId;
        int customerTwoId;

        using (var seed = new ApplicationDbContext(options))
        {
            var customerOne = new Customer
            {
                CustomerCode = "C001",
                Name = "ACME Corp",
            };

            var customerTwo = new Customer
            {
                CustomerCode = "C002",
                Name = "Globex", 
            };

            seed.Customers.AddRange(customerOne, customerTwo);
            seed.SaveChanges();

            customerOneId = customerOne.Id;
            customerTwoId = customerTwo.Id;

            seed.Engagements.AddRange(
                new Engagement
                {
                    EngagementId = "ENG-SUM1",
                    Description = "Engagement Alpha",
                    CustomerId = customerOneId,
                    Currency = "BRL",
                    Status = EngagementStatus.Active,
                    OpeningValue = 10000m,
                },
                new Engagement
                {
                    EngagementId = "ENG-SUM2",
                    Description = "Engagement Beta",
                    CustomerId = customerTwoId,
                    Currency = "BRL",
                    Status = EngagementStatus.Active,
                    OpeningValue = 8000m,
                });

            seed.SaveChanges();
        }

        var factory = new TestDbContextFactory(options);
        var repository = new InvoicePlanRepository(factory, NullLogger<InvoicePlanRepository>.Instance);

        var planOne = new InvoicePlan
        {
            EngagementId = "ENG-SUM1",
            Type = InvoicePlanType.ByDate,
            NumInvoices = 3,
            PaymentTermDays = 30,
            CustomerFocalPointName = "Alpha Focal",
            CustomerFocalPointEmail = "alpha@example.com",
            FirstEmissionDate = new DateTime(2025, 1, 5),
        };

        planOne.Items.Add(new InvoiceItem
        {
            SeqNo = 1,
            Percentage = 40m,
            Amount = 4000m,
            PayerCnpj = "11111111000100",
            EmissionDate = new DateTime(2025, 1, 5),
            DueDate = new DateTime(2025, 2, 4),
        });

        planOne.Items.Add(new InvoiceItem
        {
            SeqNo = 2,
            Percentage = 35m,
            Amount = 3500m,
            PayerCnpj = "11111111000100",
            EmissionDate = new DateTime(2025, 2, 5),
            DueDate = new DateTime(2025, 3, 7),
        });

        planOne.Items.Add(new InvoiceItem
        {
            SeqNo = 3,
            Percentage = 25m,
            Amount = 2500m,
            PayerCnpj = "11111111000100",
            EmissionDate = new DateTime(2025, 3, 5),
            DueDate = new DateTime(2025, 4, 4),
        });

        repository.SavePlan(planOne);

        var persistedPlanOne = repository.ListPlansForEngagement("ENG-SUM1").Single();
        var planOneItems = persistedPlanOne.Items.OrderBy(item => item.SeqNo).ToList();

        repository.MarkItemsAsRequested(persistedPlanOne.Id, new[]
        {
            new InvoiceRequestUpdate
            {
                ItemId = planOneItems[0].Id,
                RitmNumber = "RITM-ALPHA-1",
                CoeResponsible = "Ana",
                RequestDate = new DateTime(2025, 1, 10),
            },
            new InvoiceRequestUpdate
            {
                ItemId = planOneItems[1].Id,
                RitmNumber = "RITM-ALPHA-2",
                CoeResponsible = "Bruno",
                RequestDate = new DateTime(2025, 2, 8),
            },
        });

        repository.CloseItems(persistedPlanOne.Id, new[]
        {
            new InvoiceEmissionUpdate
            {
                ItemId = planOneItems[0].Id,
                BzCode = "BZ-ALPHA",
                EmittedAt = new DateTime(2025, 1, 18),
            },
        });

        var planTwo = new InvoicePlan
        {
            EngagementId = "ENG-SUM2",
            Type = InvoicePlanType.ByDelivery,
            NumInvoices = 2,
            PaymentTermDays = 45,
            CustomerFocalPointName = "Beta Focal",
            CustomerFocalPointEmail = "beta@example.com",
            FirstEmissionDate = new DateTime(2025, 2, 15),
        };

        planTwo.Items.Add(new InvoiceItem
        {
            SeqNo = 1,
            Percentage = 60m,
            Amount = 4800m,
            PayerCnpj = "22222222000100",
            EmissionDate = new DateTime(2025, 2, 15),
            DueDate = new DateTime(2025, 4, 1),
            DeliveryDescription = "Phase 1",
        });

        planTwo.Items.Add(new InvoiceItem
        {
            SeqNo = 2,
            Percentage = 40m,
            Amount = 3200m,
            PayerCnpj = "22222222000100",
            EmissionDate = new DateTime(2025, 3, 15),
            DueDate = new DateTime(2025, 4, 29),
            DeliveryDescription = "Phase 2",
        });

        repository.SavePlan(planTwo);

        var persistedPlanTwo = repository.ListPlansForEngagement("ENG-SUM2").Single();
        var planTwoItems = persistedPlanTwo.Items.OrderBy(item => item.SeqNo).ToList();

        repository.MarkItemsAsRequested(persistedPlanTwo.Id, new[]
        {
            new InvoiceRequestUpdate
            {
                ItemId = planTwoItems[0].Id,
                RitmNumber = "RITM-BETA-1",
                CoeResponsible = "Clara",
                RequestDate = new DateTime(2025, 2, 20),
            },
            new InvoiceRequestUpdate
            {
                ItemId = planTwoItems[1].Id,
                RitmNumber = "RITM-BETA-2",
                CoeResponsible = "Daniel",
                RequestDate = new DateTime(2025, 3, 20),
            },
        });

        repository.CancelAndReissue(persistedPlanTwo.Id, new[]
        {
            new InvoiceReissueRequest
            {
                ItemId = planTwoItems[1].Id,
                CancelReason = "Client rescheduled",
                ReplacementEmissionDate = new DateTime(2025, 4, 5),
            },
        });

        var result = repository.SearchSummary(new InvoiceSummaryFilter());

        Assert.Equal(2, result.Groups.Count);

        var alphaGroup = Assert.Single(result.Groups.Where(group => group.EngagementId == "ENG-SUM1"));
        Assert.Equal("ACME Corp", alphaGroup.CustomerName);
        Assert.Equal("C001", alphaGroup.CustomerCode);
        Assert.Equal(customerOneId, alphaGroup.CustomerId);
        Assert.Equal(3, alphaGroup.Items.Count);
        Assert.Equal(10000m, alphaGroup.TotalAmount);
        Assert.Equal(3, alphaGroup.PlannedCount + alphaGroup.RequestedCount + alphaGroup.ClosedCount + alphaGroup.CanceledCount + alphaGroup.EmittedCount + alphaGroup.ReissuedCount);
        Assert.Equal(1, alphaGroup.PlannedCount);
        Assert.Equal(1, alphaGroup.RequestedCount);
        Assert.Equal(1, alphaGroup.ClosedCount);

        var betaGroup = Assert.Single(result.Groups.Where(group => group.EngagementId == "ENG-SUM2"));
        Assert.Equal("Globex", betaGroup.CustomerName);
        Assert.Equal("C002", betaGroup.CustomerCode);
        Assert.Equal(customerTwoId, betaGroup.CustomerId);
        Assert.Equal(3, betaGroup.Items.Count);
        Assert.Equal(11200m, betaGroup.TotalAmount);
        Assert.Equal(1, betaGroup.CanceledCount);
        Assert.Equal(1, betaGroup.PlannedCount);

        var filteredByCustomer = repository.SearchSummary(new InvoiceSummaryFilter
        {
            CustomerId = customerOneId,
        });

        var singleGroup = Assert.Single(filteredByCustomer.Groups);
        Assert.Equal("ENG-SUM1", singleGroup.EngagementId);

        var closedOnly = repository.SearchSummary(new InvoiceSummaryFilter
        {
            Statuses = new[] { InvoiceItemStatus.Closed },
        });

        var closedGroup = Assert.Single(closedOnly.Groups);
        Assert.Single(closedGroup.Items);
        Assert.All(closedGroup.Items, item => Assert.Equal(InvoiceItemStatus.Closed, item.Status));
        Assert.Equal(closedGroup.TotalAmount, closedGroup.Items.Sum(item => item.Amount));
    }

    [Fact]
    public void PreviewNotifications_ReturnsEntriesForSelectedDate()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new ApplicationDbContext(options))
        {
            context.Database.EnsureCreated();
            CreateNotificationPreviewView(context);
        }

        using (var seed = new ApplicationDbContext(options))
        {
            var customer = new Customer
            {
                CustomerCode = "C-NOTIFY",
                Name = "Notification Customer",
            };

            seed.Customers.Add(customer);
            seed.SaveChanges();

            var engagement = new Engagement
            {
                EngagementId = "ENG-NOTIFY",
                Description = "Notification Demo",
                CustomerId = customer.Id,
                Currency = "BRL",
                Status = EngagementStatus.Active,
                OpeningValue = 12000m,
            };

            seed.Engagements.Add(engagement);
            seed.SaveChanges();

            var manager = new Manager
            {
                Name = "Primary Manager",
                Email = "manager@example.com",
                Position = ManagerPosition.Manager,
            };

            seed.Managers.Add(manager);
            seed.SaveChanges();

            seed.EngagementManagerAssignments.Add(new EngagementManagerAssignment
            {
                EngagementId = engagement.Id,
                ManagerId = manager.Id,
                BeginDate = new DateTime(2024, 1, 1),
            });

            seed.SaveChanges();
        }

        var factory = new TestDbContextFactory(options);
        var repository = new InvoicePlanRepository(factory, NullLogger<InvoicePlanRepository>.Instance);

        var plan = new InvoicePlan
        {
            EngagementId = "ENG-NOTIFY",
            Type = InvoicePlanType.ByDate,
            NumInvoices = 2,
            PaymentTermDays = 35,
            CustomerFocalPointName = "Notified Contact",
            CustomerFocalPointEmail = "contact@example.com",
            FirstEmissionDate = new DateTime(2025, 5, 20),
        };

        var emissionOne = new DateTime(2025, 5, 20);
        var emissionTwo = new DateTime(2025, 5, 21);

        plan.Items.Add(new InvoiceItem
        {
            SeqNo = 1,
            Percentage = 55m,
            Amount = 6600m,
            PayerCnpj = "99999999000100",
            EmissionDate = emissionOne,
            DueDate = null,
        });

        plan.Items.Add(new InvoiceItem
        {
            SeqNo = 2,
            Percentage = 45m,
            Amount = 5400m,
            PayerCnpj = "99999999000100",
            EmissionDate = emissionTwo,
            DueDate = null,
        });

        plan.AdditionalEmails.Add(new InvoicePlanEmail
        {
            Email = "notify-extra@example.com",
        });

        repository.SavePlan(plan);

        var notifyDate = CalculateNotificationDate(emissionOne);
        var previews = repository.PreviewNotifications(notifyDate.AddHours(9));

        Assert.Equal(2, previews.Count);

        var first = previews[0];
        var second = previews[1];

        Assert.Equal(notifyDate, first.NotifyDate.Date);
        Assert.Equal(notifyDate, second.NotifyDate.Date);
        Assert.Equal(1, first.SeqNo);
        Assert.Equal(2, second.SeqNo);
        Assert.Equal("ENG-NOTIFY", first.EngagementId);
        Assert.Equal("Notified Contact", first.CustomerFocalPointName);
        Assert.Equal("contact@example.com", first.CustomerFocalPointEmail);
        Assert.Equal("notify-extra@example.com", first.ExtraEmails);
        Assert.Equal("manager@example.com", first.ManagerEmails);
        Assert.Equal(new DateTime(2025, 6, 24), first.ComputedDueDate);
        Assert.Equal(new DateTime(2025, 6, 25), second.ComputedDueDate);
        Assert.Equal(6600m, first.Amount);
        Assert.Equal(5400m, second.Amount);
        Assert.Equal("Notification Customer", first.CustomerName);

        var empty = repository.PreviewNotifications(notifyDate.AddDays(14));
        Assert.Empty(empty);
    }

    private static DateTime CalculateNotificationDate(DateTime emissionDate)
    {
        var baseDate = emissionDate.Date.AddDays(-7);
        var daysToSubtract = ((int)baseDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return baseDate.AddDays(-daysToSubtract);
    }

    private static void CreateNotificationPreviewView(ApplicationDbContext context)
    {
        const string dropSql = "DROP VIEW IF EXISTS vw_InvoiceNotifyOnDate;";
        const string createSql = @"CREATE VIEW vw_InvoiceNotifyOnDate AS
SELECT
  ii.Id AS InvoiceItemId,
  date(
      date(ii.EmissionDate, '-7 day'),
      printf('-%d day', ((CAST(strftime('%w', date(ii.EmissionDate, '-7 day')) AS INTEGER) + 6) % 7))
  ) AS NotifyDate,
  ip.Id AS PlanId,
  ip.EngagementId,
  ip.NumInvoices,
  ip.PaymentTermDays,
  e.Id AS EngagementIntId,
  e.Description AS EngagementDescription,
  c.Name AS CustomerName,
  ii.SeqNo,
  date(ii.EmissionDate) AS EmissionDate,
  date(COALESCE(ii.DueDate, date(ii.EmissionDate, printf('+%d day', ip.PaymentTermDays)))) AS ComputedDueDate,
  ii.Amount,
  ip.CustomerFocalPointName,
  ip.CustomerFocalPointEmail,
  GROUP_CONCAT(ipe.Email, ';') AS ExtraEmails,
  GROUP_CONCAT(m.Email, ';') AS ManagerEmails,
  GROUP_CONCAT(m.Name, ';') AS ManagerNames,
  ii.PoNumber,
  ii.FrsNumber,
  ii.RitmNumber
FROM InvoiceItem ii
JOIN InvoicePlan ip ON ip.Id = ii.PlanId
LEFT JOIN InvoicePlanEmail ipe ON ipe.PlanId = ip.Id
LEFT JOIN Engagements e ON e.EngagementId = ip.EngagementId
LEFT JOIN Customers c ON c.Id = e.CustomerId
LEFT JOIN EngagementManagerAssignments ema ON ema.EngagementId = e.Id
    AND ema.BeginDate <= datetime(ii.EmissionDate)
    AND (ema.EndDate IS NULL OR ema.EndDate >= datetime(ii.EmissionDate))
LEFT JOIN Managers m ON m.Id = ema.ManagerId
WHERE ii.Status IN ('Planned','Requested')
GROUP BY ii.Id, NotifyDate, ip.Id, ip.EngagementId, ip.NumInvoices, ip.PaymentTermDays, e.Id,
         e.Description, c.Name, ii.SeqNo, EmissionDate, ComputedDueDate, ii.Amount,
         ip.CustomerFocalPointName, ip.CustomerFocalPointEmail, ii.PoNumber, ii.FrsNumber, ii.RitmNumber;";

        context.Database.ExecuteSqlRaw(dropSql);
        context.Database.ExecuteSqlRaw(createSql);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        {
            _options = options;
        }

        public ApplicationDbContext CreateDbContext()
        {
            return new ApplicationDbContext(_options);
        }
    }
}
