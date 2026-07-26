using HomeService.Domain.Enums;

namespace HomeService.Application.Notifications;

public static class NotificationTemplateCatalog
{
    public const string CommonVariables = "{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}";

    public static readonly IReadOnlyList<NotificationTemplateSeed> Defaults =
    [
        Company("CompanyDocumentApproved", "Piece entreprise validee", "Piece validee", "{NomEntreprise}, une piece de votre dossier a ete validee."),
        Company("CompanyDocumentRejected", "Piece entreprise refusee", "Piece a reprendre", "{NomEntreprise}, une piece de votre dossier demande une correction. {Note}"),
        Company("CompanyDocumentNeedsReplacement", "Complement requis sur dossier entreprise", "Complement requis", "{NomEntreprise}, notre equipe demande un complement sur votre dossier. {Note}"),
        Company("CompanyDocumentReopened", "Piece entreprise reouverte", "Piece reouverte", "{NomEntreprise}, une piece de votre dossier a ete remise en verification."),
        Company("CompanyApplicationRejected", "Dossier entreprise refuse", "Dossier refuse", "{NomEntreprise}, votre demande partenaire n'a pas pu etre validee pour le moment. {Note}"),
        Company("CompanyApplicationReopened", "Dossier entreprise reouvert", "Dossier reouvert", "{NomEntreprise}, votre dossier partenaire est de nouveau en analyse."),
        Company("CompanyApplicationMoreInformationRequested", "Complement requis sur dossier entreprise", "Complement requis", "{NomEntreprise}, un complement est necessaire pour terminer l'analyse. {Note}"),
        Company("CompanyApplicationApproved", "Dossier entreprise valide", "Dossier valide", "{NomEntreprise}, votre entreprise est validee sur Wele."),
        Company("CompanyActivationLinkCreated", "Lien d'activation entreprise", "Activation de votre portail", "{NomEntreprise}, votre lien d'activation est pret: {LienAction}"),
        Company("InterimCandidateReceived", "Nouvelle demande interimaire", "Nouvelle candidature", "{NomEntreprise}, {NomPrestataire} souhaite collaborer avec vous."),
        Company("MissionRequestReceived", "Nouvelle demande client", "Nouvelle demande client", "{NomEntreprise}, une demande {Service} est disponible dans votre zone."),
        Company("MissionQuoteRequired", "Devis a preparer", "Devis attendu", "{NomEntreprise}, analysez la demande {NumeroMission} et proposez votre prix."),
        Company("MissionQuoteAcceptedByCustomer", "Devis accepte par le client", "Devis accepte", "Le client a accepte le devis de la mission {NumeroMission}. Vous pouvez affecter un technicien."),
        Company("MissionAssignmentDeadlineExpired", "Delai d'affectation expire", "Delai depasse", "Le delai d'affectation de la mission {NumeroMission} est depasse."),
        Company("MissionProviderRefused", "Prestataire a refuse", "Mission refusee", "{NomPrestataire} a refuse la mission {NumeroMission}. {Motif}"),
        Company("MissionProviderAccepted", "Prestataire a accepte", "Mission acceptee", "{NomPrestataire} a accepte la mission {NumeroMission}."),
        Company("MissionAdditionalQuoteRequested", "Devis complementaire demande", "Complement demande", "{NomPrestataire} demande un devis complementaire pour {NumeroMission}. {Note}"),
        Company("MissionDisputeOpened", "Litige mission ouvert", "Litige ouvert", "Un litige est ouvert sur la mission {NumeroMission}. {Motif}"),
        Company("MissionDisputeResolved", "Litige mission resolu", "Litige resolu", "Le litige de la mission {NumeroMission} est resolu. {Note}"),
        Company("MissionPaymentReleased", "Paiement transfere", "Paiement transfere", "Le paiement de {Montant} pour {NumeroMission} est pret pour transfert."),
        Provider("InterimCandidateApproved", "Candidature interimaire acceptee", "Candidature acceptee", "{NomPrestataire}, {NomEntreprise} a accepte votre candidature."),
        Provider("InterimCandidateRejected", "Candidature interimaire refusee", "Candidature non retenue", "{NomPrestataire}, votre candidature chez {NomEntreprise} n'a pas ete retenue. {Note}"),
        Provider("MissionAssignedToProvider", "Mission affectee au prestataire", "Nouvelle mission disponible", "Mission {Service} a accepter avant la fin du delai."),
        Provider("MissionProviderAcceptanceReminder", "Rappel acceptation mission", "Reponse attendue", "{NomPrestataire}, vous avez encore {Delai} pour repondre a la mission {NumeroMission}."),
        Provider("MissionClientConfirmed", "Client a confirme", "Mission confirmee", "Le client a confirme la mission {NumeroMission}. Preparez votre intervention."),
        Provider("MissionTechnicianCanStart", "Debut mission autorise", "Vous pouvez demarrer", "Vous pouvez demarrer la mission {NumeroMission} a l'adresse {Adresse}."),
        Provider("MissionAdditionalQuoteApproved", "Devis complementaire accepte", "Complement accepte", "Le client a accepte le devis complementaire pour {NumeroMission}."),
        Provider("MissionAdditionalQuoteRejected", "Devis complementaire refuse", "Complement refuse", "Le client a refuse le devis complementaire pour {NumeroMission}."),
        Provider("ProviderProfileValidated", "Profil prestataire valide", "Profil valide", "{NomPrestataire}, votre profil est valide pour recevoir des missions."),
        Provider("ProviderProfileSuspended", "Profil prestataire suspendu", "Profil suspendu", "{NomPrestataire}, votre acces mission est suspendu. {Motif}"),
        Customer("MissionQuoteSentToCustomer", "Devis mission envoye au client", "Devis disponible", "Votre devis pour {Service} est disponible."),
        Customer("MissionQuoteAccepted", "Paiement client recu", "Paiement recu", "Votre paiement pour la mission {NumeroMission} est confirme."),
        Customer("MissionTechnicianAssigned", "Technicien affecte", "Technicien affecte", "{NomTechnicien} interviendra pour votre mission {NumeroMission}."),
        Customer("MissionTechnicianOnTheWay", "Technicien en route", "Technicien en route", "{NomTechnicien} est en route vers {Adresse}."),
        Customer("MissionTechnicianArrived", "Technicien arrive", "Technicien arrive", "{NomTechnicien} est arrive pour la mission {NumeroMission}."),
        Customer("MissionStarted", "Mission demarree", "Mission demarree", "La mission {NumeroMission} a demarre."),
        Customer("MissionCompleted", "Mission terminee", "Mission terminee", "La mission {NumeroMission} est terminee. Vous pouvez valider et noter l'intervention."),
        Customer("MissionReviewRequested", "Avis client demande", "Votre avis compte", "Notez la mission {NumeroMission}: qualite, ponctualite, politesse et proprete."),
        Customer("MissionCancelled", "Mission annulee", "Mission annulee", "La mission {NumeroMission} a ete annulee. {Motif}"),
        Customer("MissionRefundApproved", "Remboursement valide", "Remboursement valide", "Un remboursement de {Montant} est valide pour la mission {NumeroMission}."),
        Mixed("MissionStatusChanged", "Suivi de mission", "Suivi mission {NumeroMission}", "La mission {NumeroMission} a ete mise a jour.")
    ];

    private static NotificationTemplateSeed Company(string eventKey, string label, string subject, string body)
        => new(eventKey, "Company", [NotificationTemplateChannel.Portal, NotificationTemplateChannel.Email, NotificationTemplateChannel.WhatsApp], label, subject, body, CommonVariables);

    private static NotificationTemplateSeed Provider(string eventKey, string label, string subject, string body)
        => new(eventKey, "Provider", [NotificationTemplateChannel.MobilePush, NotificationTemplateChannel.Email, NotificationTemplateChannel.WhatsApp], label, subject, body, CommonVariables);

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
