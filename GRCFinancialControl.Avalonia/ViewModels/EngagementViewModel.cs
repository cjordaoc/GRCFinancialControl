using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Core.Data;
using GRCFinancialControl.Core.Services;
using ReactiveUI;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public class EngagementViewModel : ViewModelBase
    {
        private readonly EngagementService _service;
        private EngagementDto? _selectedEngagement;
        private string _engagementId = string.Empty;
        private string _engagementTitle = string.Empty;
        private string? _engagementPartner;
        private string? _engagementManager;
        private decimal _openingMargin;
        private bool _isNew;
        private bool _isEngagementIdReadOnly;

        public ObservableCollection<EngagementDto> Engagements { get; } = new();

        public EngagementDto? SelectedEngagement
        {
            get => _selectedEngagement;
            set => this.RaiseAndSetIfChanged(ref _selectedEngagement, value);
        }

        public string EngagementId
        {
            get => _engagementId;
            set => this.RaiseAndSetIfChanged(ref _engagementId, value);
        }

        public string EngagementTitle
        {
            get => _engagementTitle;
            set => this.RaiseAndSetIfChanged(ref _engagementTitle, value);
        }

        public string? EngagementPartner
        {
            get => _engagementPartner;
            set => this.RaiseAndSetIfChanged(ref _engagementPartner, value);
        }

        public string? EngagementManager
        {
            get => _engagementManager;
            set => this.RaiseAndSetIfChanged(ref _engagementManager, value);
        }

        public decimal OpeningMargin
        {
            get => _openingMargin;
            set => this.RaiseAndSetIfChanged(ref _openingMargin, value);
        }

        public bool IsEngagementIdReadOnly
        {
            get => _isEngagementIdReadOnly;
            set => this.RaiseAndSetIfChanged(ref _isEngagementIdReadOnly, value);
        }

        public ReactiveCommand<Unit, Unit> NewCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

        // Parameterless constructor for the XAML designer
        public EngagementViewModel()
        {
            Engagements = new ObservableCollection<EngagementDto>
            {
                new() { EngagementId = "E-123456", EngagementTitle = "Design Time Engagement 1" },
                new() { EngagementId = "E-654321", EngagementTitle = "Design Time Engagement 2" }
            };
            IsEngagementIdReadOnly = true;
        }

        public EngagementViewModel(AppConfig config)
        {
            var context = DbContextFactory.CreateMySqlContext(config);
            _service = new EngagementService(context);

            this.WhenAnyValue(x => x.SelectedEngagement)
                .Where(eng => eng != null)
                .Subscribe(PopulateEditors);

            var canSave = this.WhenAnyValue(
                x => x.EngagementId,
                x => x.EngagementTitle,
                (id, title) =>
                    !string.IsNullOrWhiteSpace(id) &&
                    !string.IsNullOrWhiteSpace(title) &&
                    (_isNew || IsDirty()));

            NewCommand = ReactiveCommand.Create(EnterNewMode);
            SaveCommand = ReactiveCommand.Create(Save, canSave);
            DeleteCommand = ReactiveCommand.Create(Delete, this.WhenAnyValue(x => x.SelectedEngagement, eng => eng != null));

            LoadEngagements();
        }

        private void LoadEngagements()
        {
            Engagements.Clear();
            var engagements = _service.LoadAllEngagements();
            foreach (var engagement in engagements)
            {
                Engagements.Add(engagement);
            }
        }

        private void EnterNewMode()
        {
            _isNew = true;
            IsEngagementIdReadOnly = false;
            SelectedEngagement = null;
            EngagementId = string.Empty;
            EngagementTitle = string.Empty;
            EngagementPartner = string.Empty;
            EngagementManager = string.Empty;
            OpeningMargin = 0;
        }

        private void PopulateEditors(EngagementDto? engagement)
        {
            if (engagement == null)
            {
                EnterNewMode();
                return;
            }
            _isNew = false;
            IsEngagementIdReadOnly = true;
            EngagementId = engagement.EngagementId;
            EngagementTitle = engagement.EngagementTitle;
            EngagementPartner = engagement.EngagementPartner;
            EngagementManager = engagement.EngagementManager;
            OpeningMargin = engagement.OpeningMargin;
        }

        private bool IsDirty()
        {
            if (SelectedEngagement == null) return true;

            return SelectedEngagement.EngagementTitle != EngagementTitle ||
                   SelectedEngagement.EngagementPartner != EngagementPartner ||
                   SelectedEngagement.EngagementManager != EngagementManager ||
                   SelectedEngagement.OpeningMargin != OpeningMargin;
        }

        private void Save()
        {
            var entity = new DimEngagement
            {
                EngagementId = EngagementId,
                EngagementTitle = EngagementTitle,
                EngagementPartner = EngagementPartner,
                EngagementManager = EngagementManager,
                OpeningMargin = OpeningMargin,
                IsActive = SelectedEngagement?.IsActive ?? true
            };

            if (_isNew)
            {
                _service.Insert(entity);
            }
            else
            {
                _service.Update(entity);
            }
            LoadEngagements();
            SelectedEngagement = Engagements.FirstOrDefault(e => e.EngagementId == entity.EngagementId);
        }

        private void Delete()
        {
            if (SelectedEngagement == null) return;

            _service.Delete(SelectedEngagement.EngagementId);
            LoadEngagements();
            EnterNewMode();
        }
    }
}