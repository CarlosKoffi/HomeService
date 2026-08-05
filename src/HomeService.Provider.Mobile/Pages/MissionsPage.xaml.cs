using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using System.Globalization;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;

namespace HomeService.Provider.Mobile.Pages;

public partial class MissionsPage : ContentPage
{
    private static readonly TimeSpan LocationUpdateInterval = TimeSpan.FromSeconds(10);
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;
    private string? accessToken;
    private Guid? selectedAssignmentId;
    private ProviderMobileMissionOfferResponse? liveOffer;
    private ProviderMobileMissionDetailResponse? missionDetail;
    private CancellationTokenSource? locationUpdateCancellation;
    private decimal? destinationLatitude;
    private decimal? destinationLongitude;
    private Pin? destinationPin;

    public MissionsPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
        sessionService = IPlatformApplication.Current?.Services.GetService<ProviderSessionService>();
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

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadMissionAsync();
        RefreshHost.IsRefreshing = false;
    }

    private async Task LoadMissionAsync()
    {
        if (apiClient is null || sessionService is null)
        {
            ShowMessage("Configuration mobile incomplete. Client API introuvable.");
            RenderEmptyState();
            return;
        }

        accessToken = await sessionService.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            ShowMessage("Connectez-vous pour consulter vos missions.");
            RenderEmptyState();
            return;
        }

        SetBusy(true);
        var homeResult = await apiClient.GetHomeResultAsync(accessToken);
        var listResult = await apiClient.GetMissionsAsync(accessToken);
        SetBusy(false);

        if (!homeResult.IsSuccess || homeResult.Response is null)
        {
            ShowMessage(homeResult.ErrorMessage ?? "Impossible de charger vos missions.");
            RenderEmptyState();
            return;
        }

        HideMessage();
        RenderMissionList(listResult.Response?.Items ?? []);
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
        CurrentMissionCard.IsVisible = true;
        ArrivalCard.IsVisible = true;
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

        RenderDestinationMap(detail.Latitude, detail.Longitude, detail.LocationLabel);

        RestartLiveLocationUpdates(detail);
    }

    private void RenderLiveOffer(ProviderMobileMissionOfferResponse offer)
    {
        CurrentMissionCard.IsVisible = true;
        ArrivalCard.IsVisible = true;
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
        MissionMapCard.IsVisible = false;
        ArrivalStatusLabel.Text = "Arrivee : non verifiee";
        ArrivalDetailLabel.Text = "Acceptez d'abord la mission, puis verifiez votre arrivee sur place.";
    }

    private void RenderEmptyState()
    {
        StopLiveLocationUpdates();
        selectedAssignmentId = null;
        liveOffer = null;
        missionDetail = null;
        CurrentMissionCard.IsVisible = false;
        MissionMapCard.IsVisible = false;
        ArrivalCard.IsVisible = false;
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

    private void RenderDestinationMap(decimal? latitude, decimal? longitude, string label)
    {
        destinationLatitude = latitude;
        destinationLongitude = longitude;
        if (!IsValidCoordinate(latitude, longitude))
        {
            MissionMapCard.IsVisible = false;
            return;
        }

        var location = new Location((double)latitude!.Value, (double)longitude!.Value);
        destinationPin ??= new Pin { Type = PinType.Place };
        destinationPin.Label = "Lieu de l'intervention";
        destinationPin.Address = label;
        destinationPin.Location = location;
        if (!MissionMap.Pins.Contains(destinationPin))
        {
            MissionMap.Pins.Add(destinationPin);
        }

        MissionMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromKilometers(0.8)));
        MissionMapCard.IsVisible = true;
    }

    private async void OnOpenRouteClicked(object? sender, EventArgs e)
    {
        if (!IsValidCoordinate(destinationLatitude, destinationLongitude))
        {
            return;
        }

        var latitude = destinationLatitude!.Value.ToString(CultureInfo.InvariantCulture);
        var longitude = destinationLongitude!.Value.ToString(CultureInfo.InvariantCulture);
        await Launcher.Default.OpenAsync($"https://www.google.com/maps/dir/?api=1&destination={latitude},{longitude}");
    }

    private static bool IsValidCoordinate(decimal? latitude, decimal? longitude)
        => latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    private void RenderMissionList(IReadOnlyList<ProviderMobileMissionSummaryResponse> missions)
    {
        MissionListStack.Children.Clear();
        if (missions.Count == 0)
        {
            MissionListStack.Add(new Label
            {
                Text = "Aucune mission pour le moment.",
                FontFamily = "PlusJakartaSans",
                FontSize = 14,
                TextColor = Color.FromArgb("#667085")
            });
            return;
        }

        foreach (var mission in missions.OrderBy(item => item.ScheduledFor ?? DateTimeOffset.MaxValue))
        {
            var border = new Border
            {
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#DCE8FF"),
                StrokeShape = new RoundRectangle { CornerRadius = 17 },
                Padding = 14,
                Content = new Grid
                {
                    ColumnDefinitions = Columns(GridLength.Auto, GridLength.Star, GridLength.Auto),
                    ColumnSpacing = 12
                }
            };

            var grid = (Grid)border.Content;
            grid.Add(new Border
            {
                BackgroundColor = Color.FromArgb("#EEF4FF"),
                Stroke = Color.FromArgb("#EEF4FF"),
                StrokeShape = new RoundRectangle { CornerRadius = 13 },
                WidthRequest = 48,
                HeightRequest = 48,
                Content = new Label { Text = "▣", FontSize = 22, TextColor = Color.FromArgb("#155EEF"), HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }
            }, 0);
            grid.Add(new VerticalStackLayout
            {
                Spacing = 3,
                Children =
                {
                    new Label { Text = mission.ServiceName, FontFamily = "PlusJakartaSans", FontAttributes = FontAttributes.Bold, FontSize = 15 },
                    new Label { Text = $"{FormatMissionTimeShort(mission.ScheduledFor)} · {mission.LocationLabel}", FontFamily = "PlusJakartaSans", FontSize = 12, TextColor = Color.FromArgb("#667085"), LineBreakMode = LineBreakMode.TailTruncation },
                    new Label { Text = StatusLabel(mission.Status), FontFamily = "PlusJakartaSans", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = StatusColor(mission.Status) }
                }
            }, 1);
            grid.Add(new Label { Text = "›", FontSize = 27, VerticalTextAlignment = TextAlignment.Center }, 2);
            var tap = new TapGestureRecognizer { CommandParameter = mission.AssignmentId };
            tap.Tapped += OnMissionCardTapped;
            border.GestureRecognizers.Add(tap);
            MissionListStack.Add(border);
        }
    }

    private async void OnMissionCardTapped(object? sender, TappedEventArgs e)
    {
        if (apiClient is null || string.IsNullOrWhiteSpace(accessToken) || e.Parameter is not Guid assignmentId)
        {
            return;
        }

        selectedAssignmentId = assignmentId;
        var result = await apiClient.GetMissionDetailAsync(accessToken, assignmentId);
        if (result.Response is not null)
        {
            missionDetail = result.Response;
            RenderMissionDetail(result.Response);
        }
        else
        {
            ShowMessage(result.ErrorMessage ?? "Détail de mission indisponible.");
        }
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

    private static string FormatMissionTimeShort(DateTimeOffset? value) => value?.LocalDateTime.ToString("ddd d MMM · HH:mm") ?? "Horaire à confirmer";
    private static string StatusLabel(string status) => status switch
    {
        "Offered" => "À confirmer",
        "Accepted" => "Acceptée",
        "Started" => "En cours",
        "Completed" => "Terminée",
        "Cancelled" => "Annulée",
        "Refused" => "Refusée",
        "Expired" => "Expirée",
        _ => status
    };
    private static Color StatusColor(string status) => status switch
    {
        "Completed" => Color.FromArgb("#16B364"),
        "Cancelled" or "Refused" or "Expired" => Color.FromArgb("#DC2626"),
        "Offered" => Color.FromArgb("#B54708"),
        _ => Color.FromArgb("#155EEF")
    };
    private static ColumnDefinitionCollection Columns(params GridLength[] widths)
    {
        var columns = new ColumnDefinitionCollection();
        foreach (var width in widths) columns.Add(new ColumnDefinition(width));
        return columns;
    }
}
