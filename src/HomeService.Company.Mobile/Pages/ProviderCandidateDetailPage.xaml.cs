using System.Collections.ObjectModel;
using HomeService.Company.Mobile.Services;
using HomeService.Contracts.CompanyPortal;

namespace HomeService.Company.Mobile.Pages;

[QueryProperty(nameof(RequestId), "requestId")]
[QueryProperty(nameof(ProviderId), "providerId")]
public partial class ProviderCandidateDetailPage : ContentPage
{
    private readonly CompanyMobileApiClient apiClient;
    private readonly CompanySessionStore sessionStore;
    private Guid requestId;
    private Guid providerId;
    private bool isAffiliationRequest;
    private bool isBusy;
    private string phoneNumber = string.Empty;

    public ObservableCollection<ServiceRow> Services { get; } = [];
    public ObservableCollection<DocumentRow> Documents { get; } = [];

    public string? RequestId
    {
        set { if (Guid.TryParse(value, out var parsed)) requestId = parsed; }
    }

    public string? ProviderId
    {
        set { if (Guid.TryParse(value, out var parsed)) providerId = parsed; }
    }

    public ProviderCandidateDetailPage()
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
        if (isBusy) return;
        isBusy = true;
        LoadingIndicator.IsRunning = true;
        ErrorLabel.IsVisible = false;
        try
        {
            var token = await sessionStore.GetTokenAsync();
            var companyId = await sessionStore.GetCompanyIdAsync();
            if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue)
            {
                ShowError("Votre session a expiré. Reconnectez-vous.");
                return;
            }

            var candidatesTask = apiClient.GetInterimCandidatesAsync(token, companyId.Value);
            var employeesTask = apiClient.GetProvidersAsync(token, companyId.Value);
            await Task.WhenAll(candidatesTask, employeesTask);

            var candidatesResult = await candidatesTask;
            var candidate = candidatesResult.Response?.FirstOrDefault(item =>
                (requestId != Guid.Empty && item.RequestId == requestId)
                || (requestId == Guid.Empty && providerId != Guid.Empty && item.ProviderId == providerId
                    && string.Equals(item.Status, "Pending", StringComparison.OrdinalIgnoreCase)));
            if (candidate is not null)
            {
                Bind(candidate);
                return;
            }

            var employeesResult = await employeesTask;
            var employee = employeesResult.Response?.FirstOrDefault(item => item.Id == providerId);
            if (employee is not null)
            {
                Bind(employee);
                return;
            }

            ShowError(candidatesResult.ErrorMessage ?? employeesResult.ErrorMessage ?? "Ce dossier n’est plus disponible.");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            isBusy = false;
            RefreshHost.IsRefreshing = false;
        }
    }

    private void Bind(CompanyInterimCandidateResponse candidate)
    {
        isAffiliationRequest = true;
        requestId = candidate.RequestId;
        providerId = candidate.ProviderId;
        phoneNumber = candidate.PhoneNumber;
        FullNameLabel.Text = $"{candidate.FirstName} {candidate.LastName}".Trim();
        CandidateTypeLabel.Text = "CANDIDATURE INTÉRIMAIRE";
        ExperienceLabel.Text = FormatExperience(candidate.YearsOfExperience);
        StatusLabel.Text = StatusText(candidate.Status);
        RequestedAtLabel.Text = $"Reçue le {candidate.RequestedAt.ToLocalTime():dd/MM/yyyy à HH:mm}";
        ApplicationMessageLabel.Text = string.IsNullOrWhiteSpace(candidate.Message)
            ? "Aucun message joint à la candidature."
            : candidate.Message.Trim();
        ApplicationCard.IsVisible = true;
        BindCommon(
            candidate.PhoneNumber,
            candidate.Email,
            candidate.Address,
            candidate.Gender,
            candidate.BirthDate,
            candidate.PhotoUrl,
            candidate.Services.Select(service => new ServiceRow(
                service.ServiceName,
                ExperienceLevelText(service.ExperienceLevel),
                FormatYears(service.YearsOfExperience))),
            candidate.Documents,
            string.Equals(candidate.Status, "Pending", StringComparison.OrdinalIgnoreCase));
    }

    private void Bind(CompanyEmployeeResponse employee)
    {
        isAffiliationRequest = false;
        providerId = employee.Id;
        phoneNumber = employee.PhoneNumber;
        FullNameLabel.Text = $"{employee.FirstName} {employee.LastName}".Trim();
        CandidateTypeLabel.Text = employee.EmploymentType == "TemporaryWorker" ? "INTÉRIMAIRE" : "EMPLOYÉ";
        ExperienceLabel.Text = FormatExperience(employee.YearsOfExperience);
        StatusLabel.Text = StatusText(employee.Status);
        ApplicationCard.IsVisible = false;
        BindCommon(
            employee.PhoneNumber,
            employee.Email,
            employee.Address,
            employee.Gender,
            employee.BirthDate,
            employee.PhotoUrl,
            employee.Services.Select(service => new ServiceRow(
                service.ServiceName,
                ExperienceLevelText(service.ExperienceLevel),
                FormatYears(service.YearsOfExperience))),
            employee.Documents,
            employee.Status is "Invited" or "ProfileIncomplete" or "PendingPlatformReview");
    }

    private void BindCommon(
        string phone,
        string? email,
        string? address,
        string gender,
        DateOnly? birthDate,
        string? photoUrl,
        IEnumerable<ServiceRow> services,
        IReadOnlyList<CompanyEmployeeDocumentResponse> documents,
        bool canReview)
    {
        PhoneLabel.Text = phone;
        EmailLabel.Text = string.IsNullOrWhiteSpace(email) ? "Email non renseigné" : email;
        AddressLabel.Text = string.IsNullOrWhiteSpace(address) ? "Adresse non renseignée" : address;
        IdentityLabel.Text = $"{GenderText(gender)} · {(birthDate.HasValue ? $"né(e) le {birthDate:dd/MM/yyyy}" : "date de naissance non renseignée")}";
        if (!string.IsNullOrWhiteSpace(photoUrl))
        {
            ProfileImage.Source = ImageSource.FromUri(apiClient.ResolveUri(photoUrl));
        }

        Services.Clear();
        foreach (var service in services) Services.Add(service);
        if (Services.Count == 0) Services.Add(new ServiceRow("Services à vérifier", "Aucune compétence déclarée", string.Empty));

        Documents.Clear();
        foreach (var document in documents)
        {
            Documents.Add(new DocumentRow(
                $"{DocumentTypeText(document.DocumentType)} · {document.OriginalFileName}",
                document.PreviewUrl));
        }

        DocumentsEmptyLabel.IsVisible = Documents.Count == 0;
        ProfileCard.IsVisible = true;
        InformationCard.IsVisible = true;
        ServicesCard.IsVisible = true;
        DocumentsCard.IsVisible = true;
        ActionsBar.IsVisible = canReview;
    }

    private async void OnApproveClicked(object? sender, EventArgs e)
    {
        var confirmed = await DisplayAlert(
            "Accepter ce prestataire ?",
            "Confirmez que vous avez consulté ses informations, ses pièces et validé ses compétences.",
            "Accepter",
            "Annuler");
        if (confirmed) await ReviewAsync(true, "Profil, pièces et compétences validés depuis l’application Entreprise.");
    }

    private async void OnRejectClicked(object? sender, EventArgs e)
    {
        var note = await DisplayPromptAsync(
            "Refuser la candidature",
            "Indiquez brièvement le motif du refus.",
            "Refuser",
            "Annuler",
            "Motif du refus",
            maxLength: 250);
        if (note is not null) await ReviewAsync(false, note);
    }

    private async Task ReviewAsync(bool approve, string? note)
    {
        if (isBusy) return;
        var token = await sessionStore.GetTokenAsync();
        var companyId = await sessionStore.GetCompanyIdAsync();
        if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue) return;

        isBusy = true;
        try
        {
            ApiCallResult<bool> result;
            if (isAffiliationRequest)
            {
                var review = new CompanyReviewInterimCandidateRequest(note, approve);
                result = approve
                    ? await apiClient.ApproveInterimCandidateAsync(token, companyId.Value, requestId, review)
                    : await apiClient.RejectInterimCandidateAsync(token, companyId.Value, requestId, review);
            }
            else
            {
                result = approve
                    ? await apiClient.ApproveEmployeeAsync(token, companyId.Value, providerId)
                    : await apiClient.DeactivateEmployeeAsync(token, companyId.Value, providerId);
            }

            if (!result.IsSuccess)
            {
                await DisplayAlert("Action impossible", result.ErrorMessage ?? "Réessayez dans un instant.", "OK");
                return;
            }

            await DisplayAlert(
                approve ? "Prestataire accepté" : "Candidature refusée",
                approve ? "Le prestataire rejoint maintenant votre équipe." : "La candidature a bien été traitée.",
                "OK");
            await Shell.Current.GoToAsync("//providers");
        }
        finally
        {
            isBusy = false;
        }
    }

    private async void OnDocumentClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is string url && !string.IsNullOrWhiteSpace(url))
        {
            await Launcher.Default.OpenAsync(apiClient.ResolveUri(url));
        }
    }

    private async void OnCallClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(phoneNumber)) await Launcher.Default.OpenAsync($"tel:{phoneNumber}");
    }

    private async void OnMessageClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(phoneNumber)) await Launcher.Default.OpenAsync($"sms:{phoneNumber}");
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnRefreshing(object? sender, EventArgs e) => await LoadAsync();

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
        ProfileCard.IsVisible = false;
        ApplicationCard.IsVisible = false;
        InformationCard.IsVisible = false;
        ServicesCard.IsVisible = false;
        DocumentsCard.IsVisible = false;
        ActionsBar.IsVisible = false;
    }

    private static string FormatExperience(int years) => years > 0 ? $"{years} an{(years > 1 ? "s" : string.Empty)} d’expérience" : "Expérience à vérifier";
    private static string FormatYears(int years) => years > 0 ? $"{years} an{(years > 1 ? "s" : string.Empty)}" : "Débutant";
    private static string ExperienceLevelText(string value) => value switch { "Expert" => "Expert", "Confirmed" => "Confirmé", "Intermediate" => "Intermédiaire", _ => "Niveau déclaré" };
    private static string GenderText(string value) => value switch { "Female" => "Femme", "Male" => "Homme", _ => "Genre non renseigné" };
    private static string StatusText(string value) => value switch { "Pending" => "Candidature à vérifier", "Approved" => "Candidature acceptée", "Rejected" => "Candidature refusée", "Invited" => "Invitation envoyée", "ProfileIncomplete" => "Profil à compléter", "PendingPlatformReview" => "Dossier à valider", _ => value };
    private static string DocumentTypeText(string value) => value switch { "Photo" => "Photo de profil", "IdentityDocument" => "Pièce d’identité", "Diploma" => "Diplôme ou certificat", _ => "Document" };

    public sealed record ServiceRow(string Name, string Level, string Years);
    public sealed record DocumentRow(string Label, string Url);
}
