using System.Globalization;
using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using HomeService.Mobile.Shared;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;

namespace HomeService.Provider.Mobile.Pages;

[QueryProperty(nameof(AssignmentId), "assignmentId")]
public partial class MissionDetailPage : ContentPage
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);
    private const int MinimumQualityExceptionReasonLength = 20;
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;
    private readonly CatalogMediaResolver? catalogMedia;
    private string? accessToken;
    private Guid? assignmentId;
    private ProviderMobileMissionDetailResponse? detail;
    private ProviderMissionQualityChecklistResponse? qualityChecklist;
    private decimal? destinationLatitude;
    private decimal? destinationLongitude;
    private CancellationTokenSource? refreshCancellation;
    private CancellationTokenSource? locationCancellation;
    private CancellationTokenSource? offerCountdownCancellation;
    private CancellationTokenSource? departureCountdownCancellation;
    private bool loading;
    private bool offerActionInProgress;
    private readonly HashSet<Guid> qualityUpdatesInProgress = [];

    public string AssignmentId
    {
        set
        {
            if (Guid.TryParse(value, out var parsed)) assignmentId = parsed;
        }
    }

    public MissionDetailPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
        sessionService = IPlatformApplication.Current?.Services.GetService<ProviderSessionService>();
        catalogMedia = IPlatformApplication.Current?.Services.GetService<CatalogMediaResolver>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
        StartAutomaticRefresh();
    }

    protected override void OnDisappearing()
    {
        StopBackgroundWork();
        base.OnDisappearing();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadAsync(false);
        RefreshHost.IsRefreshing = false;
    }

    private async Task LoadAsync(bool showSpinner = true, bool waitForActiveLoad = false)
    {
        if (assignmentId is null) return;
        if (loading)
        {
            if (!waitForActiveLoad) return;
            var waitUntil = DateTimeOffset.UtcNow.AddSeconds(5);
            while (loading && DateTimeOffset.UtcNow < waitUntil)
            {
                await Task.Delay(50);
            }

            if (loading) return;
        }

        loading = true;
        if (showSpinner) SetLoading(true);
        MessageBanner.IsVisible = false;

        accessToken ??= sessionService is null ? null : await sessionService.GetAccessTokenAsync();
        if (apiClient is null || string.IsNullOrWhiteSpace(accessToken))
        {
            ShowMessage("Votre session a expiré. Reconnectez-vous pour consulter cette mission.");
            FinishLoading();
            return;
        }

        var result = await apiClient.GetMissionDetailAsync(accessToken, assignmentId.Value);
        if (!result.IsSuccess || result.Response is null)
        {
            ShowMessage(result.ErrorMessage ?? "Le détail de cette mission est indisponible.");
            FinishLoading();
            return;
        }

        detail = result.Response;
        var qualityResult = await apiClient.GetMissionQualityAsync(accessToken, assignmentId.Value);
        qualityChecklist = qualityResult.IsSuccess ? qualityResult.Response : null;
        await RenderAsync(detail);
        FinishLoading();
    }

    private async Task RenderAsync(ProviderMobileMissionDetailResponse mission)
    {
        var isClosed = IsClosed(mission);
        var blockingQuote = mission.AdditionalQuotes.FirstOrDefault(quote => quote.Status is "Requested" or "Submitted");
        DetailHost.IsVisible = true;
        var displayedStatus = isClosed
            ? mission.MissionStatus is "Completed" or "Resolved" || mission.AssignmentStatus == "Completed"
                ? "Completed"
                : "Cancelled"
            : mission.MissionStatus == "OnTheWay" ? "OnTheWay" : mission.AssignmentStatus;
        StatusLabel.Text = StatusText(displayedStatus);
        var statusColor = StatusColor(displayedStatus);
        StatusLabel.TextColor = statusColor;
        StatusPill.BackgroundColor = Color.FromArgb(mission.AssignmentStatus == "Completed" ? "#ECFDF3" : "#EEF4FF");

        ServiceIcon.Source = ProviderIconResolver.ForService(mission.ServiceIconName, mission.ServiceName);
        if (catalogMedia is not null)
        {
            var remote = string.IsNullOrWhiteSpace(mission.PrestationName)
                ? await catalogMedia.ResolveServiceAsync(null, mission.ServiceName)
                : await catalogMedia.ResolvePrestationAsync(null, mission.PrestationName, serviceName: mission.ServiceName);
            if (remote is not null) ServiceIcon.Source = remote;
        }
        MissionTitleLabel.Text = string.IsNullOrWhiteSpace(mission.PrestationName) ? mission.ServiceName : mission.PrestationName;
        ServiceLabel.Text = string.IsNullOrWhiteSpace(mission.PrestationName) ? mission.CompanyName : $"{mission.ServiceName} · {mission.CompanyName}";
        MissionNumberLabel.Text = $"MISSION {mission.MissionNumber}";
        ScheduleLabel.Text = mission.ScheduledFor?.LocalDateTime.ToString("dddd d MMMM · HH:mm") ?? "Horaire à confirmer";
        LocationLabel.Text = $"{mission.LocationLabel} · {FormatDistance(mission.DistanceKm)}";
        MissionMetaDivider.IsVisible = !isClosed;
        ScheduleRow.IsVisible = !isClosed;
        LocationRow.IsVisible = !isClosed;

        CustomerCard.IsVisible = !isClosed;
        CustomerNameLabel.Text = mission.CustomerDisplayName;
        CustomerPhoneLabel.Text = mission.CanCallCustomer && !string.IsNullOrWhiteSpace(mission.CustomerPhoneNumber)
            ? mission.CustomerPhoneNumber
            : "Coordonnées disponibles selon l’état de la mission";
        CallButton.IsVisible = mission.CanCallCustomer && !string.IsNullOrWhiteSpace(mission.CustomerPhoneNumber);
        MessageButton.IsVisible = IsConversationActive(mission.MissionStatus);

        DescriptionCard.IsVisible = !string.IsNullOrWhiteSpace(mission.Description);
        DescriptionLabel.Text = mission.Description ?? string.Empty;
        if (isClosed)
        {
            MapCard.IsVisible = false;
            destinationLatitude = null;
            destinationLongitude = null;
        }
        else
        {
            RenderMap(mission);
        }

        ArrivalCard.IsVisible = !isClosed && (mission.Actions.CanVerifyArrival || mission.AssignmentStatus == "Started");
        ArrivalTitleLabel.Text = mission.Arrival.IsVerified ? "Arrivée vérifiée" : "Arrivée à confirmer";
        ArrivalDetailLabel.Text = mission.Arrival.DistanceMeters is null
            ? $"La vérification s’effectue dans un rayon de {mission.Arrival.ToleranceMeters} m."
            : $"Distance mesurée : {mission.Arrival.DistanceMeters} m · Tolérance : {mission.Arrival.ToleranceMeters} m.";

        OfferCard.IsVisible = mission.Actions.CanAccept || mission.Actions.CanRefuse;
        RestartOfferCountdown(mission);
        AcceptButton.IsVisible = mission.Actions.CanAccept;
        RefuseButton.IsVisible = mission.Actions.CanRefuse;
        SetActionsEnabled(true);

        AdditionalQuotePauseCard.IsVisible = blockingQuote is not null;
        AdditionalQuotePauseLabel.Text = blockingQuote?.Status == "Requested"
            ? "L'entreprise prépare le devis. La mission reste verrouillée."
            : "Le devis a été envoyé. La mission reprendra automatiquement après la validation du client.";

        FieldActionsStack.IsVisible = !isClosed
            && (mission.Actions.CanMarkOnTheWay || mission.Actions.CanVerifyArrival || mission.Actions.CanStart || mission.Actions.CanComplete
                || (mission.MissionStatus == "Started" && blockingQuote is null));
        OnTheWayButton.IsVisible = mission.Actions.CanMarkOnTheWay;
        RestartDepartureCountdown(mission);
        VerifyArrivalButton.IsVisible = mission.Actions.CanVerifyArrival;
        StartButton.IsVisible = mission.Actions.CanStart;
        AdditionalQuoteButton.IsVisible = !isClosed && mission.MissionStatus == "Started" && blockingQuote is null;
        CompleteButton.IsVisible = mission.Actions.CanComplete;
        await RenderQualityChecklistAsync(isClosed);
        RestartLocationUpdates(mission);
    }

    private async Task RenderQualityChecklistAsync(bool isClosed)
    {
        QualityStagesHost.Children.Clear();
        QualityCard.IsVisible = !isClosed && qualityChecklist is { RequiredItemCount: > 0 };
        if (!QualityCard.IsVisible || qualityChecklist is null) return;
        QualityProgressLabel.Text = $"{qualityChecklist.CompletionPercentage} %";
        QualityProgressBar.Progress = qualityChecklist.RequiredItemCount == 0 ? 1 : qualityChecklist.CompletedRequiredItemCount / (double)qualityChecklist.RequiredItemCount;
        QualityRequirementLabel.Text = $"{qualityChecklist.CompletedRequiredItemCount} contrôle(s) sur {qualityChecklist.RequiredItemCount} · minimum {qualityChecklist.MinimumCompletionPercentage} % pour terminer";
        QualityExceptionPanel.IsVisible = !qualityChecklist.CanComplete;
        QualityBlockingLabel.Text = qualityChecklist.CanComplete
            ? qualityChecklist.CompletionPercentage == 100
                ? "Checklist complète. Merci pour la qualité du suivi."
                : $"Vous pouvez terminer la mission. Continuez si possible jusqu'à 100 % : la qualité du suivi compte pour la réputation de l'entreprise."
            : $"Encore {Math.Max(0, qualityChecklist.MinimumCompletionPercentage - qualityChecklist.CompletionPercentage)} point(s) pour atteindre le minimum. Sinon, renseignez un motif exceptionnel detaille ci-dessous.";
        QualityBlockingLabel.TextColor = qualityChecklist.CanComplete
            ? Color.FromArgb("#067647")
            : Color.FromArgb("#B54708");

        var previewTasks = new List<Task>();

        foreach (var stage in qualityChecklist.Stages)
        {
            var stageHeader = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                Margin = new Thickness(0, 5, 0, 0)
            };
            stageHeader.Children.Add(new Label
            {
                Text = stage.Label,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#101828"),
                VerticalTextAlignment = TextAlignment.Center
            });
            var stageProgress = new Label
            {
                Text = $"{stage.CompletedRequiredItemCount}/{stage.RequiredItemCount}",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#667085"),
                VerticalTextAlignment = TextAlignment.Center
            };
            Grid.SetColumn(stageProgress, 1);
            stageHeader.Children.Add(stageProgress);
            QualityStagesHost.Children.Add(stageHeader);

            foreach (var item in stage.Items)
            {
                var itemLayout = new VerticalStackLayout { Spacing = 8 };
                var header = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    ColumnSpacing = 8
                };
                var copy = new VerticalStackLayout { Spacing = 2 };
                copy.Children.Add(new Label
                {
                    Text = item.IsRequired ? item.Label : $"{item.Label} · optionnel",
                    FontSize = 13,
                    FontAttributes = item.IsRequired ? FontAttributes.Bold : FontAttributes.None,
                    TextColor = Color.FromArgb("#101828")
                });
                if (!string.IsNullOrWhiteSpace(item.Guidance))
                {
                    copy.Children.Add(new Label
                    {
                        Text = item.Guidance,
                        FontSize = 11,
                        TextColor = Color.FromArgb("#667085"),
                        LineBreakMode = LineBreakMode.WordWrap
                    });
                }

                header.Children.Add(copy);
                if (item.ResponseType == "Automatic")
                {
                    var state = CreateQualityStateLabel(item.IsCompleted ? "✓ Auto" : "En attente", item.IsCompleted);
                    Grid.SetColumn(state, 1);
                    header.Children.Add(state);
                }
                else if (!item.IsAvailable)
                {
                    var state = CreateQualityStateLabel("Apres demarrage", false);
                    Grid.SetColumn(state, 1);
                    header.Children.Add(state);
                }

                itemLayout.Children.Add(header);

                if (item.ResponseType == "Confirmation" && item.IsAvailable)
                {
                    var confirmationRow = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Auto),
                            new ColumnDefinition(GridLength.Star)
                        },
                        ColumnSpacing = 8
                    };
                    var checkbox = new CheckBox
                    {
                        IsChecked = item.BooleanValue == true,
                        Color = Color.FromArgb("#155EEF"),
                        IsEnabled = !qualityUpdatesInProgress.Contains(item.ItemId)
                    };
                    var confirmationLabel = new Label
                    {
                        Text = item.IsCompleted ? "Fait" : "Touchez pour confirmer",
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = item.IsCompleted ? Color.FromArgb("#067647") : Color.FromArgb("#475467"),
                        VerticalTextAlignment = TextAlignment.Center
                    };
                    checkbox.CheckedChanged += async (_, args) =>
                        await SaveQualityItemAsync(item, args.Value, null, null);
                    confirmationRow.Children.Add(checkbox);
                    Grid.SetColumn(confirmationLabel, 1);
                    confirmationRow.Children.Add(confirmationLabel);
                    itemLayout.Children.Add(confirmationRow);
                }
                else if (item.ResponseType == "YesNo" && item.IsAvailable)
                {
                    var choices = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Star)
                        },
                        ColumnSpacing = 8
                    };
                    var yes = CreateQualityChoiceButton("Oui", item.BooleanValue == true);
                    yes.Clicked += async (_, _) => await SaveQualityItemAsync(item, true, null, null);
                    var no = CreateQualityChoiceButton("Non", item.BooleanValue == false);
                    no.Clicked += async (_, _) => await SaveQualityItemAsync(item, false, null, null);
                    choices.Children.Add(yes);
                    Grid.SetColumn(no, 1);
                    choices.Children.Add(no);
                    itemLayout.Children.Add(choices);
                }
                else if (item.ResponseType == "Photo" && item.IsAvailable)
                {
                    var photoRow = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(new GridLength(84)),
                            new ColumnDefinition(GridLength.Star)
                        },
                        ColumnSpacing = 10
                    };
                    var preview = new Image
                    {
                        HeightRequest = 68,
                        WidthRequest = 84,
                        Aspect = Aspect.AspectFill,
                        IsVisible = !string.IsNullOrWhiteSpace(item.EvidencePhotoUrl),
                        BackgroundColor = Color.FromArgb("#F2F4F7")
                    };
                    photoRow.Children.Add(preview);
                    var photoActions = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
                    photoActions.Children.Add(new Label
                    {
                        Text = item.IsCompleted ? "Photo enregistrée" : "Ajoutez une preuve visible",
                        FontSize = 11,
                        TextColor = item.IsCompleted ? Color.FromArgb("#067647") : Color.FromArgb("#667085")
                    });
                    var photoButton = new Button
                    {
                        Text = item.IsCompleted ? "Remplacer la photo" : "Ajouter une photo",
                        FontSize = 11,
                        HeightRequest = 36,
                        Padding = new Thickness(10, 4),
                        IsEnabled = !qualityUpdatesInProgress.Contains(item.ItemId)
                    };
                    photoButton.Clicked += async (_, _) => await CaptureQualityPhotoAsync(item);
                    photoActions.Children.Add(photoButton);
                    Grid.SetColumn(photoActions, 1);
                    photoRow.Children.Add(photoActions);
                    itemLayout.Children.Add(photoRow);
                    if (!string.IsNullOrWhiteSpace(item.EvidencePhotoUrl))
                    {
                        previewTasks.Add(LoadQualityPhotoPreviewAsync(preview, item.EvidencePhotoUrl));
                    }
                }
                else if ((item.ResponseType is "ShortText" or "Choice" or "Number") && item.IsAvailable)
                {
                    var responseRow = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto)
                        },
                        ColumnSpacing = 8
                    };
                    var entry = new Entry
                    {
                        Text = item.ResponseType == "Number"
                            ? item.NumberValue?.ToString(CultureInfo.CurrentCulture)
                            : item.TextValue,
                        Placeholder = item.ResponseType == "Number" ? "Valeur" : "Votre reponse",
                        Keyboard = item.ResponseType == "Number" ? Keyboard.Numeric : Keyboard.Text,
                        FontSize = 12
                    };
                    var save = new Button
                    {
                        Text = "Enregistrer",
                        FontSize = 11,
                        HeightRequest = 36,
                        Padding = new Thickness(10, 4),
                        IsEnabled = !qualityUpdatesInProgress.Contains(item.ItemId)
                    };
                    save.Clicked += async (_, _) =>
                    {
                        if (item.ResponseType == "Number")
                        {
                            if (!decimal.TryParse(entry.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var number))
                            {
                                ShowMessage("Saisissez une valeur numérique valide.");
                                return;
                            }

                            await SaveQualityItemAsync(item, null, number, null);
                        }
                        else
                        {
                            await SaveQualityItemAsync(item, null, null, entry.Text);
                        }
                    };
                    responseRow.Children.Add(entry);
                    Grid.SetColumn(save, 1);
                    responseRow.Children.Add(save);
                    itemLayout.Children.Add(responseRow);
                }

                QualityStagesHost.Children.Add(new Border
                {
                    BackgroundColor = item.IsCompleted ? Color.FromArgb("#F0FDF4") : Colors.White,
                    Stroke = item.IsCompleted ? Color.FromArgb("#BBF7D0") : Color.FromArgb("#E4EAF3"),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                    Padding = new Thickness(11),
                    Content = itemLayout
                });
            }
        }

        StartButton.IsEnabled = StartButton.IsEnabled && qualityChecklist.CanStart;
        await Task.WhenAll(previewTasks);
    }

    private static Label CreateQualityStateLabel(string text, bool completed) => new()
    {
        Text = text,
        FontSize = 10,
        FontAttributes = FontAttributes.Bold,
        TextColor = completed ? Color.FromArgb("#067647") : Color.FromArgb("#667085"),
        VerticalTextAlignment = TextAlignment.Center
    };

    private static Button CreateQualityChoiceButton(string text, bool selected) => new()
    {
        Text = text,
        FontSize = 11,
        HeightRequest = 36,
        Padding = new Thickness(10, 4),
        BackgroundColor = selected ? Color.FromArgb("#155EEF") : Color.FromArgb("#F2F4F7"),
        TextColor = selected ? Colors.White : Color.FromArgb("#344054")
    };

    private async Task SaveQualityItemAsync(
        ProviderMissionQualityItemResponse item,
        bool? booleanValue,
        decimal? numberValue,
        string? textValue)
    {
        if (!CanAct() || qualityUpdatesInProgress.Contains(item.ItemId)) return;
        if ((item.ResponseType is "ShortText" or "Choice") && string.IsNullOrWhiteSpace(textValue))
        {
            ShowMessage("Saisissez une réponse avant d'enregistrer.");
            return;
        }

        qualityUpdatesInProgress.Add(item.ItemId);
        var result = await apiClient!.UpdateMissionQualityItemAsync(
            accessToken!,
            assignmentId!.Value,
            item.ItemId,
            new UpdateProviderMissionQualityItemRequest(booleanValue, numberValue, textValue, null));
        qualityUpdatesInProgress.Remove(item.ItemId);
        if (!result.IsSuccess || result.Response is null)
        {
            ShowMessage(result.ErrorMessage ?? "Contrôle non enregistré.");
            await RenderQualityChecklistAsync(detail is not null && IsClosed(detail));
            return;
        }

        qualityChecklist = result.Response;
        await RenderQualityChecklistAsync(detail is not null && IsClosed(detail));
        QualityStatusLabel.Text = "Enregistré automatiquement";
        QualityStatusLabel.IsVisible = true;
    }

    private async Task CaptureQualityPhotoAsync(ProviderMissionQualityItemResponse item)
    {
        if (!CanAct() || qualityUpdatesInProgress.Contains(item.ItemId)) return;
        var source = await DisplayActionSheet("Preuve photo", "Annuler", null, "Prendre une photo", "Choisir dans la galerie");
        FileResult? file = null;
        try
        {
            if (source == "Prendre une photo" && MediaPicker.Default.IsCaptureSupported)
            {
                file = await MediaPicker.Default.CapturePhotoAsync();
            }
            else if (source == "Choisir dans la galerie")
            {
                file = await MediaPicker.Default.PickPhotoAsync();
            }
        }
        catch
        {
            ShowMessage("Impossible d'ouvrir la photo.");
        }

        if (file is null) return;
        qualityUpdatesInProgress.Add(item.ItemId);
        var upload = await apiClient!.UploadMissionQualityPhotoAsync(accessToken!, assignmentId!.Value, item.ItemId, file);
        qualityUpdatesInProgress.Remove(item.ItemId);
        if (!upload.IsSuccess)
        {
            ShowMessage(upload.ErrorMessage ?? "La photo n'a pas pu être envoyée.");
            return;
        }

        var refreshed = await apiClient.GetMissionQualityAsync(accessToken!, assignmentId.Value);
        if (refreshed.IsSuccess && refreshed.Response is not null)
        {
            qualityChecklist = refreshed.Response;
            await RenderQualityChecklistAsync(detail is not null && IsClosed(detail));
            QualityStatusLabel.Text = "Photo enregistrée et visible dans la checklist";
            QualityStatusLabel.IsVisible = true;
        }
        else
        {
            ShowMessage("Photo enregistrée. Actualisez l'écran pour afficher l'aperçu.");
        }
    }

    private async Task LoadQualityPhotoPreviewAsync(Image target, string photoUrl)
    {
        if (apiClient is null || string.IsNullOrWhiteSpace(accessToken)) return;
        var result = await apiClient.DownloadAsync(accessToken, photoUrl);
        if (!result.IsSuccess || result.Response is null || result.Response.Length == 0) return;
        var bytes = result.Response;
        target.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
        target.IsVisible = true;
    }

    private void RenderMap(ProviderMobileMissionDetailResponse mission)
    {
        destinationLatitude = mission.Latitude;
        destinationLongitude = mission.Longitude;
        if (!IsValidCoordinate(mission.Latitude, mission.Longitude))
        {
            MapCard.IsVisible = false;
            return;
        }

        var location = new Location((double)mission.Latitude!.Value, (double)mission.Longitude!.Value);
        MissionMap.Pins.Clear();
        MissionMap.Pins.Add(new Pin { Label = "Lieu de l’intervention", Address = mission.LocationLabel, Location = location, Type = PinType.Place });
        MissionMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromKilometers(0.8)));
        MapCard.IsVisible = true;
    }

    private async void OnAcceptClicked(object? sender, EventArgs e)
    {
        if (!CanAct()) return;
        if (detail is null || !detail.Actions.CanAccept || HasOfferExpired(detail.ExpiresAt))
        {
            await ExpireOfferAsync();
            return;
        }

        offerActionInProgress = true;
        SetActionsEnabled(false);
        var location = await TryGetLocationAsync();
        var result = await apiClient!.AcceptMissionAsync(accessToken!, assignmentId!.Value, new ProviderAcceptMissionRequest(
            location is null ? null : (decimal)location.Latitude,
            location is null ? null : (decimal)location.Longitude,
            location?.Accuracy is null ? null : (int)Math.Round(location.Accuracy.Value)));
        offerActionInProgress = false;
        await CompleteActionAsync(result.IsSuccess, result.ErrorMessage, "Mission acceptée.");
    }

    private async void OnRefuseClicked(object? sender, EventArgs e)
    {
        if (!CanAct()) return;
        if (detail is null || !detail.Actions.CanRefuse || HasOfferExpired(detail.ExpiresAt))
        {
            await ExpireOfferAsync();
            return;
        }

        offerActionInProgress = true;
        SetActionsEnabled(false);
        var result = await apiClient!.RefuseMissionAsync(accessToken!, assignmentId!.Value, new ProviderRefuseMissionRequest("Unavailable", "Refus depuis l’application mobile."));
        offerActionInProgress = false;
        await CompleteActionAsync(result.IsSuccess, result.ErrorMessage, "Mission refusée.");
    }

    private async void OnVerifyArrivalClicked(object? sender, EventArgs e)
        => await SendLocationActionAsync("Arrivée vérifiée.", (token, id, request) => apiClient!.VerifyArrivalAsync(token, id, request));

    private async void OnTheWayClicked(object? sender, EventArgs e)
        => await SendLocationActionAsync("Départ confirmé. Le client peut maintenant suivre votre arrivée.", (token, id, request) => apiClient!.MarkMissionOnTheWayAsync(token, id, request));

    private async void OnStartClicked(object? sender, EventArgs e)
        => await SendLocationActionAsync("Mission démarrée.", (token, id, request) => apiClient!.StartMissionAsync(token, id, request));

    private async void OnCompleteClicked(object? sender, EventArgs e)
    {
        if (!CanAct()) return;
        if (detail?.Actions.CanComplete != true)
        {
            ShowMessage("La mission ne peut pas encore être terminée. Vérifiez son statut et les actions en attente.");
            return;
        }

        string? qualityExceptionReason = null;
        if (qualityChecklist is { CanComplete: false })
        {
            qualityExceptionReason = QualityExceptionEditor.Text?.Trim();
            if (string.IsNullOrWhiteSpace(qualityExceptionReason)
                || qualityExceptionReason.Length < MinimumQualityExceptionReasonLength)
            {
                ShowMessage($"Checklist à {qualityChecklist.CompletionPercentage} %. Complétez au moins {qualityChecklist.MinimumCompletionPercentage} %, ou expliquez la situation en {MinimumQualityExceptionReasonLength} caractères minimum.");
                QualityExceptionEditor.Focus();
                return;
            }

            var confirmException = await DisplayAlert(
                "Clôture exceptionnelle",
                $"La checklist est remplie à {qualityChecklist.CompletionPercentage} %. Votre motif sera conservé et pourra être contrôlé. Confirmer la fin de mission ?",
                "Confirmer",
                "Revenir a la checklist");
            if (!confirmException) return;
        }

        SetActionsEnabled(false);
        var result = await apiClient!.CompleteMissionAsync(
            accessToken!,
            assignmentId!.Value,
            new ProviderCompleteMissionRequest(
                60,
                "Prestation terminée depuis l'application mobile.",
                null,
                qualityExceptionReason));
        if (result.IsSuccess)
        {
            ApplyClosedVisualState();
        }

        await CompleteActionAsync(
            result.IsSuccess,
            result.ErrorMessage,
            "Mission terminée. Le client peut maintenant la valider.",
            waitForActiveLoad: true);
    }

    private async void OnAdditionalQuoteClicked(object? sender, EventArgs e)
    {
        if (!CanAct() || detail?.MissionStatus != "Started") return;
        var reason = await DisplayPromptAsync(
            "Besoin complémentaire",
            "Décrivez précisément ce que vous constatez. L’entreprise préparera l’ajustement et l’enverra au client.",
            "Transmettre",
            "Annuler",
            "Pièce, réparation ou travail supplémentaire…",
            maxLength: 600,
            keyboard: Keyboard.Text);
        if (string.IsNullOrWhiteSpace(reason)) return;

        AdditionalQuoteButton.IsEnabled = false;
        var result = await apiClient!.RequestAdditionalQuoteAsync(
            accessToken!,
            assignmentId!.Value,
            new HomeService.Contracts.Missions.RequestMissionAdditionalQuoteRequest(reason.Trim(), null));
        AdditionalQuoteButton.IsEnabled = true;
        await CompleteActionAsync(
            result.IsSuccess,
            result.ErrorMessage,
            "Votre remarque a été transmise. L’entreprise préparera le devis complémentaire.");
    }

    private async Task SendLocationActionAsync(string successMessage, Func<string, Guid, ProviderLocationVerificationRequest, Task<ApiCallResult<ProviderLocationVerificationResponse>>> action)
    {
        if (!CanAct()) return;
        SetActionsEnabled(false);
        var location = await TryGetLocationAsync();
        var request = new ProviderLocationVerificationRequest(
            location is null ? null : (decimal)location.Latitude,
            location is null ? null : (decimal)location.Longitude,
            location?.Accuracy is null ? null : (int)Math.Round(location.Accuracy.Value));
        var result = await action(accessToken!, assignmentId!.Value, request);
        await CompleteActionAsync(result.IsSuccess, result.ErrorMessage, successMessage);
    }

    private async Task CompleteActionAsync(
        bool success,
        string? error,
        string successMessage,
        bool waitForActiveLoad = false)
    {
        ShowMessage(success ? successMessage : error ?? "Action impossible.");
        if (!success)
        {
            SetActionsEnabled(true);
        }

        await LoadAsync(false, waitForActiveLoad);
    }

    private void ApplyClosedVisualState()
    {
        StatusLabel.Text = StatusText("Completed");
        StatusLabel.TextColor = StatusColor("Completed");
        StatusPill.BackgroundColor = Color.FromArgb("#ECFDF3");
        MissionMetaDivider.IsVisible = false;
        ScheduleRow.IsVisible = false;
        LocationRow.IsVisible = false;
        CustomerCard.IsVisible = false;
        MapCard.IsVisible = false;
        ArrivalCard.IsVisible = false;
        OfferCard.IsVisible = false;
        AdditionalQuotePauseCard.IsVisible = false;
        FieldActionsStack.IsVisible = false;
        destinationLatitude = null;
        destinationLongitude = null;
        StopOfferCountdown();
        StopDepartureCountdown();
        locationCancellation?.Cancel();
        locationCancellation?.Dispose();
        locationCancellation = null;
    }

    private static bool IsClosed(ProviderMobileMissionDetailResponse mission)
        => mission.AssignmentStatus is "Completed" or "Cancelled" or "Refused" or "Expired"
            || mission.MissionStatus is "Completed" or "Cancelled" or "Disputed" or "Resolved";

    private async void OnCallClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(detail?.CustomerPhoneNumber)) await Launcher.Default.OpenAsync($"tel:{detail.CustomerPhoneNumber}");
    }

    private async void OnMessageClicked(object? sender, EventArgs e)
    {
        if (assignmentId is not null) await Shell.Current.GoToAsync($"//messages?assignmentId={assignmentId.Value:D}");
    }

    private static bool IsConversationActive(string status)
        => status.Equals("Accepted", StringComparison.OrdinalIgnoreCase)
            || status.Equals("OnTheWay", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Started", StringComparison.OrdinalIgnoreCase);

    private async void OnOpenRouteClicked(object? sender, EventArgs e)
    {
        if (!IsValidCoordinate(destinationLatitude, destinationLongitude)) return;
        var latitude = destinationLatitude!.Value.ToString(CultureInfo.InvariantCulture);
        var longitude = destinationLongitude!.Value.ToString(CultureInfo.InvariantCulture);
        await Launcher.Default.OpenAsync($"https://www.google.com/maps/dir/?api=1&destination={latitude},{longitude}");
    }

    private void StartAutomaticRefresh()
    {
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = new CancellationTokenSource();
        _ = RefreshLoopAsync(refreshCancellation.Token);
    }

    private async Task RefreshLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(RefreshInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken)) await MainThread.InvokeOnMainThreadAsync(() => LoadAsync(false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void RestartLocationUpdates(ProviderMobileMissionDetailResponse mission)
    {
        locationCancellation?.Cancel();
        locationCancellation?.Dispose();
        locationCancellation = null;
        if (mission.AssignmentStatus != "Accepted"
            || (!mission.Actions.CanMarkOnTheWay && !mission.Actions.CanVerifyArrival)
            || apiClient is null
            || string.IsNullOrWhiteSpace(accessToken)) return;
        locationCancellation = new CancellationTokenSource();
        _ = LocationLoopAsync(mission.AssignmentId, locationCancellation.Token);
    }

    private async Task LocationLoopAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var location = await TryGetLocationAsync(cancellationToken);
                if (location is null) continue;
                await apiClient!.UpdateMissionLocationAsync(accessToken!, id, new ProviderLocationVerificationRequest((decimal)location.Latitude, (decimal)location.Longitude, location.Accuracy is null ? null : (int)Math.Round(location.Accuracy.Value)), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void StopBackgroundWork()
    {
        StopOfferCountdown();
        StopDepartureCountdown();
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = null;
        locationCancellation?.Cancel();
        locationCancellation?.Dispose();
        locationCancellation = null;
    }

    private static async Task<Location?> TryGetLocationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted) permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted) return null;
            return await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(8)), cancellationToken)
                ?? await Geolocation.Default.GetLastKnownLocationAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return null; }
    }

    private bool CanAct() => apiClient is not null && assignmentId is not null && !string.IsNullOrWhiteSpace(accessToken);

    private void SetActionsEnabled(bool enabled)
    {
        var offerIsActive = detail is not null && !HasOfferExpired(detail.ExpiresAt);
        AcceptButton.IsEnabled = enabled && offerIsActive && detail!.Actions.CanAccept;
        RefuseButton.IsEnabled = enabled && offerIsActive && detail!.Actions.CanRefuse;
        OnTheWayButton.IsEnabled = enabled && detail?.Actions.CanMarkOnTheWay == true;
        VerifyArrivalButton.IsEnabled = enabled;
        StartButton.IsEnabled = enabled;
        AdditionalQuoteButton.IsEnabled = enabled;
        CompleteButton.IsEnabled = enabled;
    }

    private void RestartOfferCountdown(ProviderMobileMissionDetailResponse mission)
    {
        StopOfferCountdown();
        if (!mission.Actions.CanAccept && !mission.Actions.CanRefuse)
        {
            CountdownLabel.Text = string.Empty;
            return;
        }

        UpdateOfferCountdown(mission.ExpiresAt);
        if (HasOfferExpired(mission.ExpiresAt)) return;

        offerCountdownCancellation = new CancellationTokenSource();
        _ = RunOfferCountdownAsync(mission.AssignmentId, mission.ExpiresAt, offerCountdownCancellation.Token);
    }

    private void RestartDepartureCountdown(ProviderMobileMissionDetailResponse mission)
    {
        StopDepartureCountdown();
        DepartureCountdownLabel.IsVisible = false;
        if (!mission.Actions.CanMarkOnTheWay || !mission.Actions.MarkOnTheWayAutomaticallyAt.HasValue)
        {
            return;
        }

        var deadline = mission.Actions.MarkOnTheWayAutomaticallyAt.Value;
        DepartureCountdownLabel.IsVisible = true;
        UpdateDepartureCountdown(deadline);
        departureCountdownCancellation = new CancellationTokenSource();
        _ = RunDepartureCountdownAsync(mission.AssignmentId, deadline, departureCountdownCancellation.Token);
    }

    private async Task RunDepartureCountdownAsync(Guid id, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var expired = await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (detail?.AssignmentId != id) return false;
                    UpdateDepartureCountdown(deadline);
                    return deadline <= DateTimeOffset.UtcNow;
                });

                if (!expired) continue;
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    DepartureCountdownLabel.Text = "Départ automatique en cours…";
                    OnTheWayButton.IsEnabled = false;
                    await LoadAsync(false);
                });
                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void UpdateDepartureCountdown(DateTimeOffset deadline)
    {
        var seconds = RemainingSeconds(deadline);
        DepartureCountdownLabel.Text = $"Départ automatique dans {seconds / 60:00}:{seconds % 60:00}";
    }

    private void StopDepartureCountdown()
    {
        departureCountdownCancellation?.Cancel();
        departureCountdownCancellation?.Dispose();
        departureCountdownCancellation = null;
    }

    private async Task RunOfferCountdownAsync(Guid id, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var expired = await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (detail?.AssignmentId != id) return false;
                    UpdateOfferCountdown(expiresAt);
                    return HasOfferExpired(expiresAt);
                });

                if (!expired) continue;
                await MainThread.InvokeOnMainThreadAsync(ExpireOfferAsync);
                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void UpdateOfferCountdown(DateTimeOffset expiresAt)
    {
        var seconds = RemainingSeconds(expiresAt);
        CountdownLabel.Text = $"{seconds / 60:00}:{seconds % 60:00}";
        if (seconds == 0) SetActionsEnabled(false);
    }

    private async Task ExpireOfferAsync()
    {
        if (offerActionInProgress) return;
        CountdownLabel.Text = "00:00";
        SetActionsEnabled(false);
        await LoadAsync(false);
    }

    private void StopOfferCountdown()
    {
        offerCountdownCancellation?.Cancel();
        offerCountdownCancellation?.Dispose();
        offerCountdownCancellation = null;
    }

    private static int RemainingSeconds(DateTimeOffset expiresAt)
        => Math.Max(0, (int)Math.Ceiling((expiresAt - DateTimeOffset.UtcNow).TotalSeconds));

    private static bool HasOfferExpired(DateTimeOffset expiresAt) => expiresAt <= DateTimeOffset.UtcNow;

    private void FinishLoading()
    {
        loading = false;
        SetLoading(false);
    }

    private void SetLoading(bool value)
    {
        LoadingIndicator.IsVisible = value;
        LoadingIndicator.IsRunning = value;
    }

    private void ShowMessage(string message)
    {
        MessageLabel.Text = message;
        MessageBanner.IsVisible = true;
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    private static bool IsValidCoordinate(decimal? latitude, decimal? longitude) => latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
    private static string FormatDistance(double? value) => value is null ? "distance à confirmer" : $"{value:0.0} km";
    private static string StatusText(string status) => status switch { "Offered" => "À confirmer", "Accepted" => "Acceptée", "OnTheWay" => "En route", "Started" => "En cours", "Completed" => "Terminée", "Cancelled" => "Annulée", "Refused" => "Refusée", "Expired" => "Expirée", _ => status };
    private static Color StatusColor(string status) => Color.FromArgb(status switch { "Completed" => "#067647", "Cancelled" or "Refused" or "Expired" => "#B42318", "Offered" => "#B54708", _ => "#155EEF" });
}
