using System;

namespace GRCFinancialControl.Core.Models
{
    public class Employee
    {
        public string Gpn { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public bool IsEyEmployee { get; set; }
        public bool IsContractor { get; set; }
        public string? Office { get; set; }
        public string? CostCenter { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
