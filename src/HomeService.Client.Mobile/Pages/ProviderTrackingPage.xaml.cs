using System.Globalization;
using System.Text.Json;

namespace HomeService.Client.Mobile.Pages;

public partial class ProviderTrackingPage : ContentPage, IQueryAttributable
{
    private decimal providerLatitude;
    private decimal providerLongitude;
    private decimal destinationLatitude;
    private decimal destinationLongitude;

    public ProviderTrackingPage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var providerName = Read(query, "providerName", "Votre prestataire");
        providerLatitude = ReadDecimal(query, "providerLat");
        providerLongitude = ReadDecimal(query, "providerLon");
        destinationLatitude = ReadDecimal(query, "destinationLat");
        destinationLongitude = ReadDecimal(query, "destinationLon");
        var eta = Read(query, "eta", string.Empty);
        var distance = Read(query, "distance", string.Empty);

        ProviderNameLabel.Text = providerName;
        ArrivalLabel.Text = string.IsNullOrWhiteSpace(eta) ? "Arrivée en cours de calcul" : $"Arrivée estimée dans {eta} min";
        DistanceLabel.Text = string.IsNullOrWhiteSpace(distance) ? "En route vers votre adresse" : $"À environ {distance} km de votre adresse";
        MapWebView.Source = new HtmlWebViewSource { Html = BuildMapHtml(providerName) };
    }

    private string BuildMapHtml(string providerName)
    {
        var name = JsonSerializer.Serialize(providerName);
        var pLat = providerLatitude.ToString(CultureInfo.InvariantCulture);
        var pLon = providerLongitude.ToString(CultureInfo.InvariantCulture);
        var dLat = destinationLatitude.ToString(CultureInfo.InvariantCulture);
        var dLon = destinationLongitude.ToString(CultureInfo.InvariantCulture);
        return $$"""
        <!doctype html><html><head><meta name="viewport" content="width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no">
        <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"><style>html,body,#map{height:100%;margin:0} .leaflet-control-attribution{font-size:9px}</style></head>
        <body><div id="map"></div><script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script><script>
        const p=[{{pLat}},{{pLon}}], d=[{{dLat}},{{dLon}}]; const map=L.map('map',{zoomControl:false});
        L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png',{maxZoom:19,attribution:'© OpenStreetMap'}).addTo(map);
        L.circleMarker(p,{radius:9,color:'#1765F2',weight:4,fillColor:'#fff',fillOpacity:1}).addTo(map).bindPopup({{name}});
        L.marker(d).addTo(map).bindPopup('Votre adresse'); L.polyline([p,d],{color:'#1765F2',weight:5}).addTo(map);
        map.fitBounds(L.latLngBounds([p,d]),{padding:[42,42]});
        </script></body></html>
        """;
    }

    private async void OnOpenMapsClicked(object sender, EventArgs e)
    {
        var origin = $"{providerLatitude.ToString(CultureInfo.InvariantCulture)},{providerLongitude.ToString(CultureInfo.InvariantCulture)}";
        var destination = $"{destinationLatitude.ToString(CultureInfo.InvariantCulture)},{destinationLongitude.ToString(CultureInfo.InvariantCulture)}";
        await Launcher.Default.OpenAsync($"https://www.google.com/maps/dir/?api=1&origin={origin}&destination={destination}");
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private static string Read(IDictionary<string, object> query, string key, string fallback) =>
        query.TryGetValue(key, out var value) ? Uri.UnescapeDataString(value?.ToString() ?? fallback) : fallback;

    private static decimal ReadDecimal(IDictionary<string, object> query, string key) =>
        decimal.TryParse(Read(query, key, "0"), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
