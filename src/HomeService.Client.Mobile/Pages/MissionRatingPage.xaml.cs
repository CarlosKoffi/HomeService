using HomeService.Client.Mobile.Services;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class MissionRatingPage : ContentPage
{
    private readonly MissionReviewDraftStore draftStore = MobileServiceLocator.GetRequiredService<MissionReviewDraftStore>();
    private Guid currentMissionId;

    public MissionRatingPage()
    {
        InitializeComponent();
    }

    public string? MissionId { get; set; }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!Guid.TryParse(MissionId, out currentMissionId))
        {
            ShowError("Mission introuvable.");
            return;
        }

        var draft = draftStore.Begin(currentMissionId);
        QualityRating.Value = draft.QualityRating;
        PunctualityRating.Value = draft.PunctualityRating;
        PresentationRating.Value = draft.PresentationRating;
        PolitenessRating.Value = draft.PolitenessRating;
        CleanlinessRating.Value = draft.CleanlinessRating;
        CommentEditor.Text = draft.Comment;
    }

    private async void OnContinueClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        if (currentMissionId == Guid.Empty)
        {
            ShowError("Mission introuvable.");
            return;
        }

        if (QualityRating.Value == 0
            || PunctualityRating.Value == 0
            || PresentationRating.Value == 0
            || PolitenessRating.Value == 0
            || CleanlinessRating.Value == 0)
        {
            ShowError("Ajoutez une note de 1 à 5 étoiles pour chaque critère.");
            return;
        }

        var draft = draftStore.Begin(currentMissionId);
        draft.QualityRating = QualityRating.Value;
        draft.PunctualityRating = PunctualityRating.Value;
        draft.PresentationRating = PresentationRating.Value;
        draft.PolitenessRating = PolitenessRating.Value;
        draft.CleanlinessRating = CleanlinessRating.Value;
        draft.Comment = string.IsNullOrWhiteSpace(CommentEditor.Text) ? null : CommentEditor.Text.Trim();

        await Shell.Current.GoToAsync($"{nameof(MissionReviewPhotosPage)}?missionId={currentMissionId:D}");
    }

    private void OnCommentChanged(object sender, TextChangedEventArgs e)
    {
        CommentCountLabel.Text = $"{e.NewTextValue?.Length ?? 0}/500";
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
