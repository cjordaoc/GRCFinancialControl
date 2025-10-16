using System;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public sealed class UnsupportedApplicationDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext()
        {
            throw new NotSupportedException("ApplicationDbContext is not available when DATA_BACKEND=Dataverse.");
        }
    }
}
