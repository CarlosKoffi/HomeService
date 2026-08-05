using System.Globalization;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;

namespace HomeService.Client.Mobile.Pages;

public partial class ProviderTrackingPage : ContentPage, IQueryAttributable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);
    private readonly ClientMobileApiClient apiClient;
    private readonly ClientSessionStore sessionStore;
    private CancellationTokenSource? refreshCancellation;
    private Guid missionId;
    private string providerName = "Votre prestataire";
    private decimal providerLatitude;
    private decimal providerLongitude;
    private decimal destinationLatitude;
    private decimal destinationLongitude;
    private Pin? providerPin;
    private Pin? destinationPin;
    private Polyline? routeLine;

    public ProviderTrackingPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _ = Guid.TryParse(Read(query, "missionId", string.Empty), out missionId);
        providerName = Read(query, "providerName", "Votre prestataire");
        providerLatitude = ReadDecimal(query, "providerLat");
        providerLongitude = ReadDecimal(query, "providerLon");
        destinationLatitude = ReadDecimal(query, "destinationLat");
        destinationLongitude = ReadDecimal(query, "destinationLon");
        var eta = ReadInt(query, "eta");
        var distance = ReadDecimalOrNull(query, "distance");

        UpdateTrackingDisplay(providerName, providerLatitude, providerLongitude, destinationLatitude, destinationLongitude, eta, distance);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        StopRefresh();

        if (sessionStore.IsPreviewMode() || missionId == Guid.Empty)
        {
            return;
        }

        refreshCancellation = new CancellationTokenSource();
        _ = RunRefreshAsync(refreshCancellation.Token);
    }

    protected override void OnDisappearing()
    {
        StopRefresh();
        base.OnDisappearing();
    }

    private async Task RunRefreshAsync(CancellationToken cancellationToken)
    {
        await RefreshMissionAsync(cancellationToken);
        using var timer = new PeriodicTimer(RefreshInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshMissionAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal when the tracking page is closed.
        }
    }

    private async Task RefreshMissionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await apiClient.GetMissionAsync(missionId, cancellationToken);
            if (!result.IsSuccess || result.Response is null)
            {
                return;
            }

            var mission = result.Response;
            var provider = mission.AssignedProvider;
            if (provider is not null
                && provider.CanTrackLocation
                && provider.CurrentLatitude.HasValue
                && provider.CurrentLongitude.HasValue
                && provider.DestinationLatitude.HasValue
                && provider.DestinationLongitude.HasValue)
            {
                await MainThread.InvokeOnMainThreadAsync(() => UpdateTrackingDisplay(
                    provider.FullName,
                    provider.CurrentLatitude.Value,
                    provider.CurrentLongitude.Value,
                    provider.DestinationLatitude.Value,
                    provider.DestinationLongitude.Value,
                    provider.EstimatedArrivalMinutes,
                    provider.DistanceKm));
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() => ShowTrackingUnavailable(mission));
            if (mission.Status is "Started" or "Completed" or "Cancelled")
            {
                StopRefresh();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                MapStatusLabel.Text = "Connexion momentanément indisponible · nouvelle tentative automatique";
                MapStatusLabel.TextColor = Color.FromArgb("#6B7280");
            });
        }
    }

    private void UpdateTrackingDisplay(
        string name,
        decimal currentLatitude,
        decimal currentLongitude,
        decimal targetLatitude,
        decimal targetLongitude,
        int? estimatedArrivalMinutes,
        decimal? distanceKm)
    {
        providerName = name;
        providerLatitude = currentLatitude;
        providerLongitude = currentLongitude;
        destinationLatitude = targetLatitude;
        destinationLongitude = targetLongitude;

        ProviderNameLabel.Text = providerName;
        ArrivalLabel.Text = estimatedArrivalMinutes.HasValue
            ? $"Arrivée estimée dans {estimatedArrivalMinutes} min"
            : "Arrivée en cours de calcul";
        DistanceLabel.Text = distanceKm.HasValue
            ? $"À environ {distanceKm:0.0} km de votre adresse"
            : "En route vers votre adresse";
        MapStatusLabel.Text = "Position actualisée automatiquement";
        MapStatusLabel.TextColor = Color.FromArgb("#1D4ED8");

        RenderMap();
    }

    private void RenderMap()
    {
        if (!IsValidCoordinate(providerLatitude, providerLongitude)
            || !IsValidCoordinate(destinationLatitude, destinationLongitude))
        {
            return;
        }

        var providerLocation = new Location((double)providerLatitude, (double)providerLongitude);
        var destinationLocation = new Location((double)destinationLatitude, (double)destinationLongitude);

        if (providerPin is null)
        {
            providerPin = new Pin { Type = PinType.Generic };
            TrackingMap.Pins.Add(providerPin);
        }

        if (destinationPin is null)
        {
            destinationPin = new Pin
            {
                Label = "Votre adresse",
                Address = "Lieu de l’intervention",
                Type = PinType.Place
            };
            TrackingMap.Pins.Add(destinationPin);
        }

        providerPin.Label = providerName;
        providerPin.Address = "Prestataire en route";
        providerPin.Location = providerLocation;
        destinationPin.Location = destinationLocation;

        if (routeLine is null)
        {
            routeLine = new Polyline
            {
                StrokeColor = Color.FromArgb("#1765F2"),
                StrokeWidth = 6
            };
            TrackingMap.MapElements.Add(routeLine);
        }

        routeLine.Geopath.Clear();
        routeLine.Geopath.Add(providerLocation);
        routeLine.Geopath.Add(destinationLocation);

        MoveMapToRoute(providerLocation, destinationLocation);
    }

    private void MoveMapToRoute(Location providerLocation, Location destinationLocation)
    {
        var center = new Location(
            (providerLocation.Latitude + destinationLocation.Latitude) / 2,
            (providerLocation.Longitude + destinationLocation.Longitude) / 2);
        var distanceKm = Location.CalculateDistance(providerLocation, destinationLocation, DistanceUnits.Kilometers);
        var radiusKm = Math.Clamp(distanceKm * 0.75, 0.45, 35);
        TrackingMap.MoveToRegion(MapSpan.FromCenterAndRadius(
            center,
            Microsoft.Maui.Maps.Distance.FromKilometers(radiusKm)));
    }

    private void ShowTrackingUnavailable(ClientMissionStatusResponse mission)
    {
        if (providerPin is not null)
        {
            TrackingMap.Pins.Remove(providerPin);
            providerPin = null;
        }

        if (routeLine is not null)
        {
            TrackingMap.MapElements.Remove(routeLine);
            routeLine = null;
        }

        switch (mission.Status)
        {
            case "Started":
                ArrivalLabel.Text = "Le prestataire est arrivé";
                DistanceLabel.Text = "L’intervention est en cours";
                MapStatusLabel.Text = "Le partage de position est terminé";
                break;
            case "Completed":
                ArrivalLabel.Text = "Intervention terminée";
                DistanceLabel.Text = "Vous pouvez maintenant valider et noter la prestation";
                MapStatusLabel.Text = "Suivi du trajet terminé";
                break;
            case "Cancelled":
                ArrivalLabel.Text = "Mission annulée";
                DistanceLabel.Text = "Le suivi du trajet est arrêté";
                MapStatusLabel.Text = "Partage de position désactivé";
                break;
            default:
                ArrivalLabel.Text = "Position en cours de mise à jour";
                DistanceLabel.Text = "Le prestataire doit activer sa localisation";
                MapStatusLabel.Text = "Nouvelle tentative automatique";
                break;
        }

        MapStatusLabel.TextColor = Color.FromArgb("#6B7280");
    }

    private void StopRefresh()
    {
        var cancellation = refreshCancellation;
        refreshCancellation = null;
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }

    private async void OnOpenMapsClicked(object sender, EventArgs e)
    {
        if (!IsValidCoordinate(providerLatitude, providerLongitude)
            || !IsValidCoordinate(destinationLatitude, destinationLongitude))
        {
            await DisplayAlert("Position indisponible", "Le trajet sera disponible dès que le prestataire partagera sa position.", "OK");
            return;
        }

        var origin = $"{providerLatitude.ToString(CultureInfo.InvariantCulture)},{providerLongitude.ToString(CultureInfo.InvariantCulture)}";
        var destination = $"{destinationLatitude.ToString(CultureInfo.InvariantCulture)},{destinationLongitude.ToString(CultureInfo.InvariantCulture)}";
        await Launcher.Default.OpenAsync($"https://www.google.com/maps/dir/?api=1&origin={origin}&destination={destination}");
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private static bool IsValidCoordinate(decimal latitude, decimal longitude) =>
        latitude is >= -90 and <= 90
        && longitude is >= -180 and <= 180
        && (latitude != 0 || longitude != 0);

    private static string Read(IDictionary<string, object> query, string key, string fallback) =>
        query.TryGetValue(key, out var value) ? Uri.UnescapeDataString(value?.ToString() ?? fallback) : fallback;

    private static int? ReadInt(IDictionary<string, object> query, string key) =>
        int.TryParse(Read(query, key, string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static decimal ReadDecimal(IDictionary<string, object> query, string key) =>
        decimal.TryParse(Read(query, key, "0"), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static decimal? ReadDecimalOrNull(IDictionary<string, object> query, string key) =>
        decimal.TryParse(Read(query, key, string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
}
