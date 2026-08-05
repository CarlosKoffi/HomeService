using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;

namespace HomeService.Provider.Mobile.Pages;

public partial class MissionsPage : ContentPage
{
    private const string AccessTokenPreferenceKey = "ProviderAccessToken";
    private static readonly TimeSpan LocationUpdateInterval = TimeSpan.FromSeconds(10);
    private readonly ProviderMobileApiClient? apiClient;
    private string? accessToken;
    private Guid? selectedAssignmentId;
    private ProviderMobileMissionOfferResponse? liveOffer;
    private ProviderMobileMissionDetailResponse? missionDetail;
    private CancellationTokenSource? locationUpdateCancellation;

    public MissionsPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMissionAsync();
    }

    protected override void OnDisappearing()
    {
        StopLiveLocationUpdates();
        base.OnDisappearing();
    }

    private async Task LoadMissionAsync()
    {
        if (apiClient is null)
        {
            ShowMessage("Configuration mobile incomplete. Client API introuvable.");
            RenderEmptyState();
            return;
        }

        accessToken = Preferences.Default.Get(AccessTokenPreferenceKey, string.Empty);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            ShowMessage("Connectez-vous pour consulter vos missions.");
            RenderEmptyState();
            return;
        }

        SetBusy(true);
        var homeResult = await apiClient.GetHomeResultAsync(accessToken);
        SetBusy(false);

        if (!homeResult.IsSuccess || homeResult.Response is null)
        {
            ShowMessage(homeResult.ErrorMessage ?? "Impossible de charger vos missions.");
            RenderEmptyState();
            return;
        }

        HideMessage();
        liveOffer = homeResult.Response.LiveOffer;
        selectedAssignmentId = liveOffer?.AssignmentId ?? homeResult.Response.UpcomingMission?.AssignmentId;

        if (selectedAssignmentId is null)
        {
            RenderEmptyState();
            return;
        }

        var detailResult = await apiClient.GetMissionDetailAsync(accessToken, selectedAssignmentId.Value);
        missionDetail = detailResult.Response;
        if (missionDetail is not null)
        {
            RenderMissionDetail(missionDetail);
            return;
        }

        if (liveOffer is not null)
        {
            RenderLiveOffer(liveOffer);
            return;
        }

        ShowMessage(detailResult.ErrorMessage ?? "Detail mission indisponible.");
    }

    private void RenderMissionDetail(ProviderMobileMissionDetailResponse detail)
    {
        MissionStateLabel.Text = detail.AssignmentStatus.ToUpperInvariant();
        MissionTitleLabel.Text = string.IsNullOrWhiteSpace(detail.PrestationName)
            ? detail.ServiceName
            : $"{detail.ServiceName} - {detail.PrestationName}";
        MissionCompanyLabel.Text = $"Entreprise : {detail.CompanyName}";
        MissionLocationLabel.Text = $"{detail.LocationLabel} - {FormatDistance(detail.DistanceKm)}";
        MissionCountdownLabel.Text = detail.Actions.CanAccept
            ? $"Temps restant pour accepter : {FormatSeconds(detail.SecondsToRespond)}"
            : FormatMissionTime(detail.ScheduledFor);
        MissionCustomerLabel.Text = detail.CanCallCustomer && !string.IsNullOrWhiteSpace(detail.CustomerPhoneNumber)
            ? $"Client : {detail.CustomerDisplayName} - {detail.CustomerPhoneNumber}"
            : $"Client : {detail.CustomerDisplayName}";
        MissionDescriptionLabel.Text = string.IsNullOrWhiteSpace(detail.Description) ? string.Empty : detail.Description;

        OfferActionsGrid.IsVisible = detail.Actions.CanAccept || detail.Actions.CanRefuse;
        AcceptButton.IsVisible = detail.Actions.CanAccept;
        RefuseButton.IsVisible = detail.Actions.CanRefuse;
        FieldActionsStack.IsVisible = detail.Actions.CanVerifyArrival || detail.Actions.CanStart || detail.Actions.CanComplete;
        VerifyArrivalButton.IsVisible = detail.Actions.CanVerifyArrival;
        StartButton.IsVisible = detail.Actions.CanStart;
        CompleteButton.IsVisible = detail.Actions.CanComplete;

        ArrivalStatusLabel.Text = detail.Arrival.IsVerified
            ? "Arrivee : verifiee"
            : $"Arrivee : {detail.Arrival.Status}";
        ArrivalDetailLabel.Text = detail.Arrival.DistanceMeters is null
            ? $"Tolerance : {detail.Arrival.ToleranceMeters} m."
            : $"Distance mesuree : {detail.Arrival.DistanceMeters} m. Tolerance : {detail.Arrival.ToleranceMeters} m.";

        RestartLiveLocationUpdates(detail);
    }

    private void RenderLiveOffer(ProviderMobileMissionOfferResponse offer)
    {
        MissionStateLabel.Text = "MISSION A CONFIRMER";
        MissionTitleLabel.Text = offer.ServiceName;
        MissionCompanyLabel.Text = $"Entreprise : {offer.CompanyName}";
        MissionLocationLabel.Text = $"{offer.LocationLabel} - {FormatDistance(offer.DistanceKm)}";
        MissionCountdownLabel.Text = $"Temps restant pour accepter : {FormatSeconds(offer.SecondsToRespond)}";
        MissionCustomerLabel.Text = $"Client : {offer.CustomerDisplayName}";
        MissionDescriptionLabel.Text = offer.Instruction;
        OfferActionsGrid.IsVisible = true;
        AcceptButton.IsVisible = true;
        RefuseButton.IsVisible = true;
        FieldActionsStack.IsVisible = false;
        ArrivalStatusLabel.Text = "Arrivee : non verifiee";
        ArrivalDetailLabel.Text = "Acceptez d'abord la mission, puis verifiez votre arrivee sur place.";
    }

    private void RenderEmptyState()
    {
        StopLiveLocationUpdates();
        selectedAssignmentId = null;
        liveOffer = null;
        missionDetail = null;
        MissionStateLabel.Text = "MISSIONS";
        MissionTitleLabel.Text = "Aucune mission";
        MissionCompanyLabel.Text = "Les missions affectees apparaitront ici.";
        MissionLocationLabel.Text = string.Empty;
        MissionCountdownLabel.Text = string.Empty;
        MissionCustomerLabel.Text = string.Empty;
        MissionDescriptionLabel.Text = string.Empty;
        OfferActionsGrid.IsVisible = false;
        FieldActionsStack.IsVisible = false;
        ArrivalStatusLabel.Text = "Arrivee : non verifiee";
        ArrivalDetailLabel.Text = "Aucune mission active pour le moment.";
    }

    private async void OnAcceptClicked(object? sender, EventArgs e)
    {
        if (apiClient is null || selectedAssignmentId is null || string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        SetBusy(true);
        var location = await TryGetLocationAsync();
        var result = await apiClient.AcceptMissionAsync(accessToken, selectedAssignmentId.Value, ToAcceptRequest(location));
        SetBusy(false);

        if (!result.IsSuccess)
        {
            ShowMessage(result.ErrorMessage ?? "Acceptation impossible.");
            return;
        }

        ShowMessage("Mission acceptee.");
        await LoadMissionAsync();
    }

    private async void OnRefuseClicked(object? sender, EventArgs e)
    {
        if (apiClient is null || selectedAssignmentId is null || string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        SetBusy(true);
        var result = await apiClient.RefuseMissionAsync(
            accessToken,
            selectedAssignmentId.Value,
            new ProviderRefuseMissionRequest("Unavailable", "Refus depuis l'application mobile."));
        SetBusy(false);

        if (!result.IsSuccess)
        {
            ShowMessage(result.ErrorMessage ?? "Refus impossible.");
            return;
        }

        ShowMessage("Mission refusee.");
        await LoadMissionAsync();
    }

    private async void OnVerifyArrivalClicked(object? sender, EventArgs e)
    {
        await SendLocationActionAsync("Arrivee verifiee.", (token, id, request) => apiClient!.VerifyArrivalAsync(token, id, request));
    }

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        await SendLocationActionAsync("Prestation demarree.", (token, id, request) => apiClient!.StartMissionAsync(token, id, request));
    }

    private async void OnCompleteClicked(object? sender, EventArgs e)
    {
        if (apiClient is null || selectedAssignmentId is null || string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        SetBusy(true);
        var result = await apiClient.CompleteMissionAsync(
            accessToken,
            selectedAssignmentId.Value,
            new ProviderCompleteMissionRequest(60, "Prestation terminee depuis l'application mobile.", null));
        SetBusy(false);

        if (!result.IsSuccess)
        {
            ShowMessage(result.ErrorMessage ?? "Cloture impossible.");
            return;
        }

        ShowMessage("Mission terminee. Le client pourra valider.");
        await LoadMissionAsync();
    }

    private async Task SendLocationActionAsync(
        string successMessage,
        Func<string, Guid, ProviderLocationVerificationRequest, Task<ApiCallResult<ProviderLocationVerificationResponse>>> action)
    {
        if (selectedAssignmentId is null || string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        SetBusy(true);
        var location = await TryGetLocationAsync();
        var result = await action(accessToken, selectedAssignmentId.Value, ToLocationRequest(location));
        SetBusy(false);

        if (!result.IsSuccess)
        {
            ShowMessage(result.ErrorMessage ?? "Action impossible.");
            return;
        }

        ShowMessage(successMessage);
        await LoadMissionAsync();
    }

    private static ProviderAcceptMissionRequest ToAcceptRequest(Location? location)
    {
        return new ProviderAcceptMissionRequest(
            location?.Latitude is null ? null : (decimal)location.Latitude,
            location?.Longitude is null ? null : (decimal)location.Longitude,
            location?.Accuracy is null ? null : (int)Math.Round(location.Accuracy.Value));
    }

    private static ProviderLocationVerificationRequest ToLocationRequest(Location? location)
    {
        return new ProviderLocationVerificationRequest(
            location?.Latitude is null ? null : (decimal)location.Latitude,
            location?.Longitude is null ? null : (decimal)location.Longitude,
            location?.Accuracy is null ? null : (int)Math.Round(location.Accuracy.Value));
    }

    private static async Task<Location?> TryGetLocationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted)
            {
                permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (permission != PermissionStatus.Granted)
            {
                return null;
            }

            return await Geolocation.Default.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(8)),
                    cancellationToken)
                ?? await Geolocation.Default.GetLastKnownLocationAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private void RestartLiveLocationUpdates(ProviderMobileMissionDetailResponse detail)
    {
        StopLiveLocationUpdates();
        if (apiClient is null
            || string.IsNullOrWhiteSpace(accessToken)
            || detail.AssignmentStatus != "Accepted")
        {
            return;
        }

        locationUpdateCancellation = new CancellationTokenSource();
        _ = RunLiveLocationUpdatesAsync(detail.AssignmentId, locationUpdateCancellation.Token);
    }

    private async Task RunLiveLocationUpdatesAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        try
        {
            await SendLiveLocationAsync(assignmentId, cancellationToken);
            using var timer = new PeriodicTimer(LocationUpdateInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await SendLiveLocationAsync(assignmentId, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal when the prestataire leaves the active mission page.
        }
    }

    private async Task SendLiveLocationAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        if (apiClient is null || string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        var location = await TryGetLocationAsync(cancellationToken);
        if (location is null)
        {
            return;
        }

        await apiClient.UpdateMissionLocationAsync(
            accessToken,
            assignmentId,
            ToLocationRequest(location),
            cancellationToken);
    }

    private void StopLiveLocationUpdates()
    {
        var cancellation = locationUpdateCancellation;
        locationUpdateCancellation = null;
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }

    private void SetBusy(bool isBusy)
    {
        AcceptButton.IsEnabled = !isBusy;
        RefuseButton.IsEnabled = !isBusy;
        VerifyArrivalButton.IsEnabled = !isBusy;
        StartButton.IsEnabled = !isBusy;
        CompleteButton.IsEnabled = !isBusy;
    }

    private void ShowMessage(string message)
    {
        MessageLabel.Text = message;
        MessageBanner.IsVisible = true;
    }

    private void HideMessage()
    {
        MessageBanner.IsVisible = false;
    }

    private static string FormatSeconds(int seconds)
    {
        seconds = Math.Max(0, seconds);
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private static string FormatDistance(double? distanceKm)
    {
        return distanceKm is null ? "distance a confirmer" : $"{distanceKm:0.0} km";
    }

    private static string FormatMissionTime(DateTimeOffset? scheduledFor)
    {
        return scheduledFor is null ? "Horaire a confirmer" : $"Rendez-vous : {scheduledFor.Value.LocalDateTime:g}";
    }
}
