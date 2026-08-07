using System.Collections.ObjectModel;
using HomeService.Company.Mobile.Services;
using HomeService.Contracts.CompanyPortal;

namespace HomeService.Company.Mobile.Pages;

public partial class ProvidersPage : ContentPage
{
    private readonly CompanyMobileApiClient apiClient;
    private readonly CompanySessionStore sessionStore;
    private bool isReviewingCandidate;

    public ObservableCollection<PendingApprovalRow> PendingApprovals { get; } = [];
    public ObservableCollection<ProviderRow> Providers { get; } = [];

    public ProvidersPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current!.Services.GetRequiredService<CompanyMobileApiClient>();
        sessionStore = IPlatformApplication.Current.Services.GetRequiredService<CompanySessionStore>();
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var token = await sessionStore.GetTokenAsync();
        var companyId = await sessionStore.GetCompanyIdAsync();
        if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue)
        {
            return;
        }

        var candidatesTask = apiClient.GetInterimCandidatesAsync(token, companyId.Value);
        var providersTask = apiClient.GetProvidersAsync(token, companyId.Value);
        await Task.WhenAll(candidatesTask, providersTask);

        PendingApprovals.Clear();
        var candidatesResult = await candidatesTask;
        if (candidatesResult.IsSuccess)
        {
            foreach (var candidate in (candidatesResult.Response ?? [])
                         .Where(item => string.Equals(item.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                         .OrderByDescending(item => item.RequestedAt))
            {
                PendingApprovals.Add(PendingApprovalRow.From(candidate));
            }
        }

        var providersResult = await providersTask;
        if (providersResult.IsSuccess)
        {
            var providerItems = providersResult.Response ?? [];
            foreach (var provider in providerItems
                         .Where(IsAwaitingCompanyApproval)
                         .OrderByDescending(item => item.CreatedAt))
            {
                PendingApprovals.Add(PendingApprovalRow.From(provider));
            }

            Providers.Clear();
            foreach (var provider in providerItems
                         .Where(item => !IsAwaitingCompanyApproval(item))
                         .OrderByDescending(item => item.IsAvailable)
                         .ThenBy(item => item.FirstName))
            {
                Providers.Add(ProviderRow.From(provider));
            }
        }

        PendingHeader.IsVisible = PendingApprovals.Count > 0;
        SectionsSeparator.IsVisible = PendingApprovals.Count > 0;
        PendingCountLabel.Text = PendingApprovals.Count.ToString();
        TeamCountLabel.Text = Providers.Count.ToString();
        TeamEmptyLabel.IsVisible = Providers.Count == 0;
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadAsync();
        RefreshHost.IsRefreshing = false;
    }

    private async void OnApproveCandidateClicked(object? sender, EventArgs e)
    {
        if (isReviewingCandidate || (sender as Button)?.CommandParameter is not PendingApprovalRow row)
        {
            return;
        }

        var confirmed = await DisplayAlert(
            "Accepter ce prestataire ?",
            $"Vous confirmez avoir vérifié le profil, les pièces et les compétences de {row.FullName}. Il rejoindra ensuite votre équipe.",
            "Accepter",
            "Annuler");
        if (!confirmed)
        {
            return;
        }

        await ReviewCandidateAsync(row, approve: true, note: "Profil et compétences validés depuis l’application Entreprise.");
    }

    private async void OnRejectCandidateClicked(object? sender, EventArgs e)
    {
        if (isReviewingCandidate || (sender as Button)?.CommandParameter is not PendingApprovalRow row)
        {
            return;
        }

        var note = await DisplayPromptAsync(
            "Refuser la candidature",
            $"Indiquez brièvement la raison du refus pour {row.FullName}.",
            "Refuser",
            "Annuler",
            "Motif du refus",
            maxLength: 250);
        if (note is null)
        {
            return;
        }

        await ReviewCandidateAsync(row, approve: false, note);
    }

    private async Task ReviewCandidateAsync(PendingApprovalRow row, bool approve, string? note)
    {
        var token = await sessionStore.GetTokenAsync();
        var companyId = await sessionStore.GetCompanyIdAsync();
        if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue)
        {
            return;
        }

        isReviewingCandidate = true;
        try
        {
            ApiCallResult<bool> result;
            if (row.IsAffiliationRequest)
            {
                var request = new CompanyReviewInterimCandidateRequest(note, approve);
                result = approve
                    ? await apiClient.ApproveInterimCandidateAsync(token, companyId.Value, row.RequestId!.Value, request)
                    : await apiClient.RejectInterimCandidateAsync(token, companyId.Value, row.RequestId!.Value, request);
            }
            else
            {
                result = approve
                    ? await apiClient.ApproveEmployeeAsync(token, companyId.Value, row.ProviderId)
                    : await apiClient.DeactivateEmployeeAsync(token, companyId.Value, row.ProviderId);
            }

            if (!result.IsSuccess)
            {
                await DisplayAlert("Action impossible", result.ErrorMessage ?? "Réessayez dans un instant.", "OK");
                return;
            }

            await LoadAsync();
            await DisplayAlert(
                approve ? "Prestataire accepté" : "Candidature refusée",
                approve ? $"{row.FullName} apparaît maintenant dans votre équipe." : "La candidature a bien été traitée.",
                "OK");
        }
        finally
        {
            isReviewingCandidate = false;
        }
    }

    private async void OnCallClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is ProviderRow row && !string.IsNullOrWhiteSpace(row.PhoneNumber))
        {
            await Launcher.Default.OpenAsync($"tel:{row.PhoneNumber}");
        }
    }

    private async void OnMessageClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is ProviderRow row && !string.IsNullOrWhiteSpace(row.PhoneNumber))
        {
            await Launcher.Default.OpenAsync($"sms:{row.PhoneNumber}");
        }
    }

    private static bool IsAwaitingCompanyApproval(CompanyEmployeeResponse provider)
        => provider.Status is "Invited" or "ProfileIncomplete" or "PendingPlatformReview";

    public sealed record PendingApprovalRow(
        Guid? RequestId,
        Guid ProviderId,
        bool IsAffiliationRequest,
        string FullName,
        string TypeLabel,
        string ServiceLabel,
        string ExperienceLabel,
        string MessageLabel,
        bool HasMessage,
        string DetailLabel)
    {
        public static PendingApprovalRow From(CompanyInterimCandidateResponse candidate)
        {
            var services = string.Join(", ", candidate.Services.Take(3).Select(item => item.ServiceName));
            var message = candidate.Message?.Trim() ?? string.Empty;
            return new PendingApprovalRow(
                candidate.RequestId,
                candidate.ProviderId,
                true,
                $"{candidate.FirstName} {candidate.LastName}".Trim(),
                "CANDIDATURE",
                string.IsNullOrWhiteSpace(services) ? "Services à vérifier" : services,
                candidate.YearsOfExperience > 0
                    ? $"{candidate.YearsOfExperience} an{(candidate.YearsOfExperience > 1 ? "s" : string.Empty)} d’expérience"
                    : "Expérience à vérifier",
                message,
                message.Length > 0,
                $"Demande reçue le {candidate.RequestedAt.ToLocalTime():dd/MM/yyyy à HH:mm}");
        }

        public static PendingApprovalRow From(CompanyEmployeeResponse provider)
        {
            var services = string.Join(", ", provider.Services.Take(3).Select(item => item.ServiceName));
            var hasIdentity = !string.IsNullOrWhiteSpace(provider.IdentityDocumentUrl);
            var hasDiploma = provider.HasDiploma;
            var documentStatus = hasIdentity
                ? hasDiploma ? "Pièce d’identité et diplôme joints" : "Pièce d’identité jointe"
                : "Pièce d’identité manquante";

            return new PendingApprovalRow(
                null,
                provider.Id,
                false,
                $"{provider.FirstName} {provider.LastName}".Trim(),
                provider.EmploymentType == "TemporaryWorker" ? "INTÉRIMAIRE" : "EMPLOYÉ",
                string.IsNullOrWhiteSpace(services) ? "Services à compléter" : services,
                provider.YearsOfExperience > 0
                    ? $"{provider.YearsOfExperience} an{(provider.YearsOfExperience > 1 ? "s" : string.Empty)} d’expérience"
                    : "Expérience à vérifier",
                documentStatus,
                true,
                $"Créé le {provider.CreatedAt.ToLocalTime():dd/MM/yyyy à HH:mm} • {ProviderStatusLabel(provider.Status)}");
        }

        private static string ProviderStatusLabel(string status) => status switch
        {
            "Invited" => "invitation envoyée",
            "ProfileIncomplete" => "profil incomplet",
            "PendingPlatformReview" => "dossier à valider",
            _ => "en attente"
        };
    }

    public sealed record ProviderRow(
        Guid Id,
        string FullName,
        string PhoneNumber,
        string ServiceLabel,
        string MissionLabel,
        string AvailabilityLabel,
        Color AvailabilityColor)
    {
        public static ProviderRow From(CompanyEmployeeResponse provider)
        {
            var services = string.Join(", ", provider.Services.Take(2).Select(item => item.ServiceName));
            return new ProviderRow(
                provider.Id,
                $"{provider.FirstName} {provider.LastName}".Trim(),
                provider.PhoneNumber,
                string.IsNullOrWhiteSpace(services) ? "Services à compléter" : services,
                provider.CurrentMission is null ? $"{provider.CompletedMissionCount} missions terminées" : provider.CurrentMission.ServiceName,
                provider.CurrentMission is not null ? "EN MISSION" : provider.IsAvailable ? "DISPONIBLE" : "HORS LIGNE",
                provider.CurrentMission is not null
                    ? Color.FromArgb("#155EEF")
                    : provider.IsAvailable ? Color.FromArgb("#16B364") : Color.FromArgb("#667085"));
        }
    }
}
