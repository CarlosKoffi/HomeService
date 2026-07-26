using HomeService.Domain.Enums;

namespace HomeService.Application.Notifications;

public static class NotificationTemplateCatalog
{
    public const string CommonVariables = "{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}";

    public static readonly IReadOnlyList<NotificationTemplateSeed> Defaults =
    [
        Company("CompanyDocumentRejected", "Piece entreprise refusee", "Piece a reprendre", "{NomEntreprise}, une piece de votre dossier demande une correction. {Note}"),
        Company("CompanyDocumentNeedsReplacement", "Complement requis sur dossier entreprise", "Complement requis", "{NomEntreprise}, notre equipe demande un complement sur votre dossier. {Note}"),
        Company("CompanyDocumentReopened", "Piece entreprise reouverte", "Piece reouverte", "{NomEntreprise}, une piece de votre dossier a ete remise en verification."),
        Company("CompanyApplicationRejected", "Dossier entreprise refuse", "Dossier refuse", "{NomEntreprise}, votre demande partenaire n'a pas pu etre validee pour le moment. {Note}"),
        Company("CompanyApplicationReopened", "Dossier entreprise reouvert", "Dossier reouvert", "{NomEntreprise}, votre dossier partenaire est de nouveau en analyse."),
        Company("CompanyApplicationMoreInformationRequested", "Complement requis sur dossier entreprise", "Complement requis", "{NomEntreprise}, un complement est necessaire pour terminer l'analyse. {Note}"),
        Company("CompanyApplicationApproved", "Dossier entreprise valide", "Dossier valide", "{NomEntreprise}, votre entreprise est validee sur Wele."),
        Company("CompanyActivationLinkCreated", "Lien d'activation entreprise", "Activation de votre portail", "{NomEntreprise}, votre lien d'activation est pret: {LienAction}"),
        Company("InterimCandidateReceived", "Nouvelle demande interimaire", "Nouvelle candidature", "{NomEntreprise}, {NomPrestataire} souhaite collaborer avec vous."),
        Provider("InterimCandidateApproved", "Candidature interimaire acceptee", "Candidature acceptee", "{NomPrestataire}, {NomEntreprise} a accepte votre candidature."),
        Provider("MissionAssignedToProvider", "Mission affectee au prestataire", "Nouvelle mission disponible", "Mission {Service} a accepter avant la fin du delai."),
        Customer("MissionQuoteSentToCustomer", "Devis mission envoye au client", "Devis disponible", "Votre devis pour {Service} est disponible."),
        Mixed("MissionStatusChanged", "Suivi de mission", "Suivi mission {NumeroMission}", "La mission {NumeroMission} a ete mise a jour.")
    ];

    private static NotificationTemplateSeed Company(string eventKey, string label, string subject, string body)
        => new(eventKey, "Company", [NotificationTemplateChannel.Portal, NotificationTemplateChannel.Email, NotificationTemplateChannel.WhatsApp], label, subject, body, CommonVariables);

    private static NotificationTemplateSeed Provider(string eventKey, string label, string subject, string body)
        => new(eventKey, "Provider", [NotificationTemplateChannel.MobilePush, NotificationTemplateChannel.WhatsApp], label, subject, body, CommonVariables);

    private static NotificationTemplateSeed Customer(string eventKey, string label, string subject, string body)
        => new(eventKey, "Customer", [NotificationTemplateChannel.MobilePush, NotificationTemplateChannel.Email, NotificationTemplateChannel.WhatsApp], label, subject, body, CommonVariables);

    private static NotificationTemplateSeed Mixed(string eventKey, string label, string subject, string body)
        => new(eventKey, "Mixed", [NotificationTemplateChannel.Portal, NotificationTemplateChannel.MobilePush, NotificationTemplateChannel.Email, NotificationTemplateChannel.WhatsApp], label, subject, body, CommonVariables);
}

public sealed record NotificationTemplateSeed(
    string EventKey,
    string Audience,
    IReadOnlyList<NotificationTemplateChannel> Channels,
    string Label,
    string SubjectTemplate,
    string BodyTemplate,
    string AvailableVariables);
