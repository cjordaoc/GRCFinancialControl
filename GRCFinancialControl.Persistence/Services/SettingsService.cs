using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly SettingsDbContext _context;

        public SettingsService(SettingsDbContext context)
        {
            _context = context;
        }

        public async Task<Dictionary<string, string>> GetAllAsync()
        {
            return await _context.Settings.ToDictionaryAsync(s => s.Key, s => s.Value);
        }

        public async Task SaveAllAsync(Dictionary<string, string> settings)
        {
            foreach (var setting in settings)
            {
                var existingSetting = await _context.Settings.FirstOrDefaultAsync(s => s.Key == setting.Key);
                if (existingSetting != null)
                {
                    existingSetting.Value = setting.Value;
                }
                else
                {
                    await _context.Settings.AddAsync(new Setting { Key = setting.Key, Value = setting.Value });
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}