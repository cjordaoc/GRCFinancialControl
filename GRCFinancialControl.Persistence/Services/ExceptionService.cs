using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class ExceptionService : IExceptionService
    {
        private readonly ApplicationDbContext _context;

        public ExceptionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ExceptionEntry>> GetAllAsync()
        {
            return await _context.Exceptions.ToListAsync();
        }
    }
}