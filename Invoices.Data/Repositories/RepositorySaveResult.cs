namespace Invoices.Data.Repositories;

public readonly record struct RepositorySaveResult(int Created, int Updated, int Deleted, int AffectedRows)
{
    public static RepositorySaveResult Empty { get; } = new(0, 0, 0, 0);
}
