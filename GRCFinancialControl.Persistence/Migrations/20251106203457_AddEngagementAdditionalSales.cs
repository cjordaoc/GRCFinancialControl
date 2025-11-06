using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GRCFinancialControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEngagementAdditionalSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CustomerCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Gpn = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EmployeeName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsEyEmployee = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    IsContractor = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Office = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CostCenter = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Gpn);
                });

            migrationBuilder.CreateTable(
                name: "Exceptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SourceFile = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    RowData = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exceptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FiscalYears",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AreaSalesTarget = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    AreaRevenueTarget = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    LockedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LockedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalYears", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvoicePlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EngagementId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    NumInvoices = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentTermDays = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerFocalPointName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CustomerFocalPointEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CustomInstructions = table.Column<string>(type: "TEXT", nullable: true),
                    AdditionalDetails = table.Column<string>(type: "TEXT", nullable: true),
                    FirstEmissionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoicePlan", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Managers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    Position = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    WindowsLogin = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Managers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Papds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Level = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    WindowsLogin = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Papds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RankMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RankCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RankName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SpreadsheetRank = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClosingPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FiscalYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClosingPeriods", x => x.Id);
                    table.UniqueConstraint("AK_ClosingPeriods_Name", x => x.Name);
                    table.ForeignKey(
                        name: "FK_ClosingPeriods_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    SeqNo = table.Column<int>(type: "INTEGER", nullable: false),
                    Percentage = table.Column<decimal>(type: "TEXT", precision: 9, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    EmissionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PayerCnpj = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PoNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    FrsNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CustomerTicket = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    AdditionalInfo = table.Column<string>(type: "TEXT", nullable: true),
                    DeliveryDescription = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    RitmNumber = table.Column<string>(type: "TEXT", nullable: true),
                    CoeResponsible = table.Column<string>(type: "TEXT", nullable: true),
                    RequestDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PaymentTypeCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceItem_InvoicePlan_PlanId",
                        column: x => x.PlanId,
                        principalTable: "InvoicePlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoicePlanEmail",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoicePlanEmail", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoicePlanEmail_InvoicePlan_PlanId",
                        column: x => x.PlanId,
                        principalTable: "InvoicePlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EngagementRankBudgetHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EngagementCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RankCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    FiscalYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClosingPeriodId = table.Column<int>(type: "INTEGER", nullable: false),
                    Hours = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngagementRankBudgetHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EngagementRankBudgetHistory_ClosingPeriods_ClosingPeriodId",
                        column: x => x.ClosingPeriodId,
                        principalTable: "ClosingPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EngagementRankBudgetHistory_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Engagements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EngagementId = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    MarginPctBudget = table.Column<decimal>(type: "TEXT", precision: 9, scale: 4, nullable: true),
                    MarginPctEtcp = table.Column<decimal>(type: "TEXT", precision: 9, scale: 4, nullable: true),
                    LastEtcDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProposedNextEtcDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StatusText = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    OpeningValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "GrcProject"),
                    OpeningExpenses = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    InitialHoursBudget = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    EtcpHours = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    ValueEtcp = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    ExpensesEtcp = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    UnbilledRevenueDays = table.Column<int>(type: "INTEGER", nullable: true),
                    LastClosingPeriodId = table.Column<int>(type: "INTEGER", nullable: true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Engagements", x => x.Id);
                    table.UniqueConstraint("AK_Engagements_EngagementId", x => x.EngagementId);
                    table.ForeignKey(
                        name: "FK_Engagements_ClosingPeriods_LastClosingPeriodId",
                        column: x => x.LastClosingPeriodId,
                        principalTable: "ClosingPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Engagements_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceEmission",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InvoiceItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    BzCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EmittedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CanceledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancelReason = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceEmission", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceEmission_InvoiceItem_InvoiceItemId",
                        column: x => x.InvoiceItemId,
                        principalTable: "InvoiceItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MailOutbox",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NotificationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    InvoiceItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ToEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CcCsv = table.Column<string>(type: "TEXT", nullable: true),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    BodyText = table.Column<string>(type: "TEXT", nullable: false),
                    SendToken = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailOutbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailOutbox_InvoiceItem_InvoiceItemId",
                        column: x => x.InvoiceItemId,
                        principalTable: "InvoiceItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActualsEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EngagementId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Hours = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ImportBatchId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PapdId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClosingPeriodId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActualsEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActualsEntries_ClosingPeriods_ClosingPeriodId",
                        column: x => x.ClosingPeriodId,
                        principalTable: "ClosingPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActualsEntries_Engagements_EngagementId",
                        column: x => x.EngagementId,
                        principalTable: "Engagements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActualsEntries_Papds_PapdId",
                        column: x => x.PapdId,
                        principalTable: "Papds",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EngagementAdditionalSales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EngagementId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    OpportunityId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Value = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngagementAdditionalSales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EngagementAdditionalSales_Engagements_EngagementId",
                        column: x => x.EngagementId,
                        principalTable: "Engagements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EngagementFiscalYearRevenueAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EngagementId = table.Column<int>(type: "INTEGER", nullable: false),
                    FiscalYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToGoValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ToDateValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    LastUpdateDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngagementFiscalYearRevenueAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EngagementFiscalYearRevenueAllocations_Engagements_EngagementId",
                        column: x => x.EngagementId,
                        principalTable: "Engagements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EngagementFiscalYearRevenueAllocations_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EngagementManagerAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EngagementId = table.Column<int>(type: "INTEGER", nullable: false),
                    ManagerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngagementManagerAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EngagementManagerAssignments_Engagements_EngagementId",
                        column: x => x.EngagementId,
                        principalTable: "Engagements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EngagementManagerAssignments_Managers_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "Managers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EngagementPapds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EngagementId = table.Column<int>(type: "INTEGER", nullable: false),
                    PapdId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngagementPapds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EngagementPapds_Engagements_EngagementId",
                        column: x => x.EngagementId,
                        principalTable: "Engagements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EngagementPapds_Papds_PapdId",
                        column: x => x.PapdId,
                        principalTable: "Papds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EngagementRankBudgets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EngagementId = table.Column<int>(type: "INTEGER", nullable: false),
                    FiscalYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    RankName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    BudgetHours = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    ConsumedHours = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    AdditionalHours = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    RemainingHours = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Green"),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngagementRankBudgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EngagementRankBudgets_Engagements_EngagementId",
                        column: x => x.EngagementId,
                        principalTable: "Engagements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EngagementRankBudgets_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FinancialEvolution",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClosingPeriodId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EngagementId = table.Column<int>(type: "INTEGER", nullable: false),
                    HoursData = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    ValueData = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    MarginData = table.Column<decimal>(type: "TEXT", precision: 9, scale: 4, nullable: true),
                    ExpenseData = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialEvolution", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialEvolution_Engagements_EngagementId",
                        column: x => x.EngagementId,
                        principalTable: "Engagements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlannedAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EngagementId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClosingPeriodId = table.Column<int>(type: "INTEGER", nullable: false),
                    AllocatedHours = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannedAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlannedAllocations_ClosingPeriods_ClosingPeriodId",
                        column: x => x.ClosingPeriodId,
                        principalTable: "ClosingPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlannedAllocations_Engagements_EngagementId",
                        column: x => x.EngagementId,
                        principalTable: "Engagements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MailOutboxLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OutboxId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttemptAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailOutboxLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailOutboxLog_MailOutbox_OutboxId",
                        column: x => x.OutboxId,
                        principalTable: "MailOutbox",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActualsEntries_ClosingPeriodId",
                table: "ActualsEntries",
                column: "ClosingPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_ActualsEntries_EngagementId_Date",
                table: "ActualsEntries",
                columns: new[] { "EngagementId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ActualsEntries_ImportBatchId",
                table: "ActualsEntries",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ActualsEntries_PapdId",
                table: "ActualsEntries",
                column: "PapdId");

            migrationBuilder.CreateIndex(
                name: "IX_ClosingPeriods_FiscalYearId",
                table: "ClosingPeriods",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustomerCode",
                table: "Customers",
                column: "CustomerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CostCenter",
                table: "Employees",
                column: "CostCenter");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Office",
                table: "Employees",
                column: "Office");

            migrationBuilder.CreateIndex(
                name: "IX_EngagementAdditionalSales_EngagementId",
                table: "EngagementAdditionalSales",
                column: "EngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_EngagementFiscalYearRevenueAllocations_EngagementId",
                table: "EngagementFiscalYearRevenueAllocations",
                column: "EngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_EngagementFiscalYearRevenueAllocations_FiscalYearId",
                table: "EngagementFiscalYearRevenueAllocations",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_EngagementManagerAssignments_EngagementId_ManagerId",
                table: "EngagementManagerAssignments",
                columns: new[] { "EngagementId", "ManagerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EngagementManagerAssignments_ManagerId",
                table: "EngagementManagerAssignments",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_EngagementPapds_EngagementId_PapdId",
                table: "EngagementPapds",
                columns: new[] { "EngagementId", "PapdId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EngagementPapds_PapdId",
                table: "EngagementPapds",
                column: "PapdId");

            migrationBuilder.CreateIndex(
                name: "IX_EngagementRankBudgetHistory_ClosingPeriodId",
                table: "EngagementRankBudgetHistory",
                column: "ClosingPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_EngagementRankBudgetHistory_FiscalYearId",
                table: "EngagementRankBudgetHistory",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_History_Key",
                table: "EngagementRankBudgetHistory",
                columns: new[] { "EngagementCode", "RankCode", "FiscalYearId", "ClosingPeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EngagementRankBudgets_EngagementId_FiscalYearId_RankName",
                table: "EngagementRankBudgets",
                columns: new[] { "EngagementId", "FiscalYearId", "RankName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EngagementRankBudgets_FiscalYearId",
                table: "EngagementRankBudgets",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Engagements_CustomerId",
                table: "Engagements",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Engagements_LastClosingPeriodId",
                table: "Engagements",
                column: "LastClosingPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEvolution_EngagementId_ClosingPeriodId",
                table: "FinancialEvolution",
                columns: new[] { "EngagementId", "ClosingPeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceEmission_Item",
                table: "InvoiceEmission",
                column: "InvoiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItem_EmissionDate",
                table: "InvoiceItem",
                column: "EmissionDate");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItem_Status",
                table: "InvoiceItem",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UQ_InvoiceItem_PlanSeq",
                table: "InvoiceItem",
                columns: new[] { "PlanId", "SeqNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePlan_Engagement",
                table: "InvoicePlan",
                column: "EngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePlanEmail_Plan",
                table: "InvoicePlanEmail",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MailOutbox_InvoiceItemId",
                table: "MailOutbox",
                column: "InvoiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MailOutbox_Notification",
                table: "MailOutbox",
                column: "NotificationDate");

            migrationBuilder.CreateIndex(
                name: "IX_MailOutbox_Pending",
                table: "MailOutbox",
                columns: new[] { "NotificationDate", "SentAt", "SendToken" });

            migrationBuilder.CreateIndex(
                name: "IX_MailOutboxLog_Outbox",
                table: "MailOutboxLog",
                column: "OutboxId");

            migrationBuilder.CreateIndex(
                name: "IX_Managers_WindowsLogin",
                table: "Managers",
                column: "WindowsLogin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Papds_WindowsLogin",
                table: "Papds",
                column: "WindowsLogin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlannedAllocations_ClosingPeriodId",
                table: "PlannedAllocations",
                column: "ClosingPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_PlannedAllocations_EngagementId",
                table: "PlannedAllocations",
                column: "EngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_RankMappings_IsActive",
                table: "RankMappings",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RankMappings_RankCode",
                table: "RankMappings",
                column: "RankCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RankMappings_RankName",
                table: "RankMappings",
                column: "RankName");

            migrationBuilder.CreateIndex(
                name: "IX_RankMappings_SpreadsheetRank",
                table: "RankMappings",
                column: "SpreadsheetRank");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActualsEntries");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "EngagementAdditionalSales");

            migrationBuilder.DropTable(
                name: "EngagementFiscalYearRevenueAllocations");

            migrationBuilder.DropTable(
                name: "EngagementManagerAssignments");

            migrationBuilder.DropTable(
                name: "EngagementPapds");

            migrationBuilder.DropTable(
                name: "EngagementRankBudgetHistory");

            migrationBuilder.DropTable(
                name: "EngagementRankBudgets");

            migrationBuilder.DropTable(
                name: "Exceptions");

            migrationBuilder.DropTable(
                name: "FinancialEvolution");

            migrationBuilder.DropTable(
                name: "InvoiceEmission");

            migrationBuilder.DropTable(
                name: "InvoicePlanEmail");

            migrationBuilder.DropTable(
                name: "MailOutboxLog");

            migrationBuilder.DropTable(
                name: "PlannedAllocations");

            migrationBuilder.DropTable(
                name: "RankMappings");

            migrationBuilder.DropTable(
                name: "Managers");

            migrationBuilder.DropTable(
                name: "Papds");

            migrationBuilder.DropTable(
                name: "MailOutbox");

            migrationBuilder.DropTable(
                name: "Engagements");

            migrationBuilder.DropTable(
                name: "InvoiceItem");

            migrationBuilder.DropTable(
                name: "ClosingPeriods");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "InvoicePlan");

            migrationBuilder.DropTable(
                name: "FiscalYears");
        }
    }
}
