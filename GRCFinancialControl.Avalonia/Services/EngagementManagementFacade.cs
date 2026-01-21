using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.Services
{
    /// <summary>
    /// Facade service consolidating engagement-related operations.
    /// Reduces ViewModel coupling by grouping related service calls into cohesive operations.
    /// </summary>
    public interface IEngagementManagementFacade
    {
        /// <summary>Gets the underlying engagement service for AllocationEditorViewModel compatibility.</summary>
        IEngagementService EngagementService { get; }

        /// <summary>Gets all engagements.</summary>
        Task<IList<Engagement>> GetAllEngagementsAsync();

        /// <summary>Gets a specific engagement by ID with all related data.</summary>
        Task<Engagement?> GetEngagementAsync(int id);

        /// <summary>Adds a new engagement.</summary>
        Task AddEngagementAsync(Engagement engagement);

        /// <summary>Updates an existing engagement.</summary>
        Task UpdateEngagementAsync(Engagement engagement);

        /// <summary>Deletes an engagement.</summary>
        Task DeleteEngagementAsync(int id);

        /// <summary>Deletes all financial data for an engagement (reverse import).</summary>
        Task DeleteEngagementDataAsync(int id);

        /// <summary>Gets all customers for the editor.</summary>
        Task<IList<Customer>> GetAllCustomersAsync();

        /// <summary>Gets all closing periods for the editor.</summary>
        Task<IList<ClosingPeriod>> GetAllClosingPeriodsAsync();

        /// <summary>Gets all available PAPDs.</summary>
        Task<IList<Papd>> GetAllPapdsAsync();

        /// <summary>Gets available (not assigned) PAPDs for an engagement.</summary>
        Task<IList<Papd>> GetAvailablePapdsAsync(Engagement engagement);

        /// <summary>Assigns a PAPD to an engagement.</summary>
        Task AssignPapdAsync(int engagementId, int papdId);

        /// <summary>Removes a PAPD assignment from an engagement.</summary>
        Task RemovePapdAsync(int engagementId, int papdId);

        /// <summary>Gets all available managers.</summary>
        Task<IList<Manager>> GetAllManagersAsync();

        /// <summary>Gets available (not assigned) managers for an engagement.</summary>
        Task<IList<Manager>> GetAvailableManagersAsync(Engagement engagement);

        /// <summary>Assigns a manager to an engagement.</summary>
        Task AssignManagerAsync(int engagementId, int managerId);

        /// <summary>Removes a manager assignment from an engagement.</summary>
        Task RemoveManagerAsync(int engagementId, int managerId);
    }

    /// <summary>
    /// Implementation of IEngagementManagementFacade.
    /// Consolidates 8 different services into a cohesive engagement management interface.
    /// </summary>
    public class EngagementManagementFacade : IEngagementManagementFacade
    {
        private readonly IEngagementService _engagementService;
        private readonly ICustomerService _customerService;
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly IPapdService _papdService;
        private readonly IPapdAssignmentService _papdAssignmentService;
        private readonly IManagerService _managerService;
        private readonly IManagerAssignmentService _managerAssignmentService;

        /// <summary>Gets the underlying engagement service for AllocationEditorViewModel compatibility.</summary>
        public IEngagementService EngagementService => _engagementService;

        public EngagementManagementFacade(
            IEngagementService engagementService,
            ICustomerService customerService,
            IClosingPeriodService closingPeriodService,
            IPapdService papdService,
            IPapdAssignmentService papdAssignmentService,
            IManagerService managerService,
            IManagerAssignmentService managerAssignmentService)
        {
            _engagementService = engagementService ?? throw new ArgumentNullException(nameof(engagementService));
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
            _closingPeriodService = closingPeriodService ?? throw new ArgumentNullException(nameof(closingPeriodService));
            _papdService = papdService ?? throw new ArgumentNullException(nameof(papdService));
            _papdAssignmentService = papdAssignmentService ?? throw new ArgumentNullException(nameof(papdAssignmentService));
            _managerService = managerService ?? throw new ArgumentNullException(nameof(managerService));
            _managerAssignmentService = managerAssignmentService ?? throw new ArgumentNullException(nameof(managerAssignmentService));
        }

        public async Task<IList<Engagement>> GetAllEngagementsAsync()
        {
            return await _engagementService.GetAllAsync();
        }

        public async Task<Engagement?> GetEngagementAsync(int id)
        {
            return await _engagementService.GetByIdAsync(id);
        }

        public async Task AddEngagementAsync(Engagement engagement)
        {
            await _engagementService.AddAsync(engagement);
        }

        public async Task UpdateEngagementAsync(Engagement engagement)
        {
            await _engagementService.UpdateAsync(engagement);
        }

        public async Task DeleteEngagementAsync(int id)
        {
            await _engagementService.DeleteAsync(id);
        }

        public async Task DeleteEngagementDataAsync(int id)
        {
            await _engagementService.DeleteDataAsync(id);
        }

        public async Task<IList<Customer>> GetAllCustomersAsync()
        {
            return await _customerService.GetAllAsync();
        }

        public async Task<IList<ClosingPeriod>> GetAllClosingPeriodsAsync()
        {
            return await _closingPeriodService.GetAllAsync();
        }

        public async Task<IList<Papd>> GetAllPapdsAsync()
        {
            return await _papdService.GetAllAsync();
        }

        public async Task<IList<Papd>> GetAvailablePapdsAsync(Engagement engagement)
        {
            ArgumentNullException.ThrowIfNull(engagement);

            var papds = await _papdService.GetAllAsync();
            var assignedIds = engagement.EngagementPapds.Select(ep => ep.PapdId).ToHashSet();
            return papds.Where(p => !assignedIds.Contains(p.Id)).ToList();
        }

        public async Task AssignPapdAsync(int engagementId, int papdId)
        {
            var currentAssignments = await _papdAssignmentService.GetByEngagementIdAsync(engagementId);
            var papdIds = currentAssignments.Select(pa => pa.PapdId).ToList();
            papdIds.Add(papdId);
            await _papdAssignmentService.UpdateAssignmentsForEngagementAsync(engagementId, papdIds);
        }

        public async Task RemovePapdAsync(int engagementId, int papdId)
        {
            var currentAssignments = await _papdAssignmentService.GetByEngagementIdAsync(engagementId);
            var papdIds = currentAssignments
                .Where(pa => pa.PapdId != papdId)
                .Select(pa => pa.PapdId)
                .ToList();
            await _papdAssignmentService.UpdateAssignmentsForEngagementAsync(engagementId, papdIds);
        }

        public async Task<IList<Manager>> GetAllManagersAsync()
        {
            return await _managerService.GetAllAsync();
        }

        public async Task<IList<Manager>> GetAvailableManagersAsync(Engagement engagement)
        {
            ArgumentNullException.ThrowIfNull(engagement);

            var managers = await _managerService.GetAllAsync();
            var assignedIds = engagement.ManagerAssignments.Select(ma => ma.ManagerId).ToHashSet();
            return managers.Where(m => !assignedIds.Contains(m.Id)).ToList();
        }

        public async Task AssignManagerAsync(int engagementId, int managerId)
        {
            var assignment = new EngagementManagerAssignment
            {
                EngagementId = engagementId,
                ManagerId = managerId
            };
            await _managerAssignmentService.AddAsync(assignment);
        }

        public async Task RemoveManagerAsync(int engagementId, int managerId)
        {
            var currentAssignments = await _managerAssignmentService.GetByEngagementIdAsync(engagementId);
            var assignment = currentAssignments.FirstOrDefault(ma => ma.ManagerId == managerId);
            if (assignment != null)
            {
                await _managerAssignmentService.DeleteAsync(assignment.Id);
            }
        }
    }
}
