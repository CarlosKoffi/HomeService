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
        Company("MissionRequestAnalyzing", "Demande en analyse entreprise", "Demande en analyse", "{NomEntreprise}, analysez la demande {NumeroMission} avant de proposer un prix au client."),
        Company("MissionQuoteRequired", "Devis a preparer", "Devis attendu", "{NomEntreprise}, analysez la demande {NumeroMission} et proposez votre prix."),
        Company("MissionQuoteSentByCompany", "Devis envoye au client", "Devis envoye", "Votre devis de {Montant} pour la mission {NumeroMission} a ete transmis au client."),
        Company("MissionQuoteAcceptedByCustomer", "Devis accepte par le client", "Devis accepte", "Le client a accepte le devis de la mission {NumeroMission}. Vous pouvez affecter un technicien."),
        Company("MissionQuoteRejectedByCustomer", "Devis refuse par le client", "Devis refuse", "Le client a refuse le devis de la mission {NumeroMission}. {Motif}"),
        Company("MissionAssignmentDeadlineExpired", "Delai d'affectation expire", "Delai depasse", "Le delai d'affectation de la mission {NumeroMission} est depasse."),
        Company("MissionAssignedToCompanyProvider", "Prestataire affecte", "Prestataire affecte", "{NomPrestataire} est affecte a la mission {NumeroMission}."),
        Company("MissionProviderRefused", "Prestataire a refuse", "Mission refusee", "{NomPrestataire} a refuse la mission {NumeroMission}. {Motif}"),
        Company("MissionProviderAccepted", "Prestataire a accepte", "Mission acceptee", "{NomPrestataire} a accepte la mission {NumeroMission}."),
        Company("MissionProviderNoResponse", "Prestataire sans reponse", "Aucune reponse prestataire", "{NomPrestataire} n'a pas repondu dans le delai pour la mission {NumeroMission}."),
        Company("MissionTechnicianArrivedCompany", "Technicien arrive", "Technicien arrive", "{NomPrestataire} est arrive sur la mission {NumeroMission}."),
        Company("MissionStartedCompany", "Mission demarree", "Mission demarree", "{NomPrestataire} a demarre la mission {NumeroMission}."),
        Company("MissionCompletedCompany", "Mission terminee", "Mission terminee", "La mission {NumeroMission} est terminee et attend validation client."),
        Company("MissionAdditionalQuoteRequested", "Devis complementaire demande", "Complement demande", "{NomPrestataire} demande un devis complementaire pour {NumeroMission}. {Note}"),
        Company("MissionAdditionalQuoteSent", "Devis complementaire envoye", "Complement envoye", "Le devis complementaire de {Montant} pour {NumeroMission} a ete transmis au client."),
        Company("MissionAdditionalQuotePaid", "Devis complementaire paye", "Complement paye", "Le client a paye le devis complementaire de {Montant} pour {NumeroMission}."),
        Company("MissionCancelledByCustomer", "Mission annulee par client", "Mission annulee", "Le client a annule la mission {NumeroMission}. {Motif}"),
        Company("MissionCancelledByProvider", "Mission annulee par prestataire", "Mission annulee", "{NomPrestataire} a annule la mission {NumeroMission}. {Motif}"),
        Company("MissionCancelledByCompany", "Mission annulee par entreprise", "Mission annulee", "{NomEntreprise} a annule la mission {NumeroMission}. {Motif}"),
        Company("MissionCustomerAbsent", "Client absent", "Client absent", "Le client est absent pour la mission {NumeroMission}. {Note}"),
        Company("MissionDisputeOpened", "Litige mission ouvert", "Litige ouvert", "Un litige est ouvert sur la mission {NumeroMission}. {Motif}"),
        Company("MissionDisputeResolved", "Litige mission resolu", "Litige resolu", "Le litige de la mission {NumeroMission} est resolu. {Note}"),
        Company("MissionRefundApprovedCompany", "Remboursement valide entreprise", "Remboursement valide", "Un remboursement de {Montant} a ete valide sur la mission {NumeroMission}."),
        Company("MissionPaymentReleased", "Paiement transfere", "Paiement transfere", "Le paiement de {Montant} pour {NumeroMission} est pret pour transfert."),
        Company("MissionPayoutSent", "Paiement entreprise envoye", "Paiement envoye", "Le paiement entreprise de {Montant} pour {NumeroMission} a ete envoye."),
        Provider("InterimCandidateApproved", "Candidature interimaire acceptee", "Candidature acceptee", "{NomPrestataire}, {NomEntreprise} a accepte votre candidature."),
        Provider("InterimCandidateRejected", "Candidature interimaire refusee", "Candidature non retenue", "{NomPrestataire}, votre candidature chez {NomEntreprise} n'a pas ete retenue. {Note}"),
        Provider("MissionAssignedToProvider", "Mission affectee au prestataire", "Nouvelle mission disponible", "Mission {Service} a accepter avant la fin du delai."),
        Provider("MissionProviderAcceptanceReminder", "Rappel acceptation mission", "Reponse attendue", "{NomPrestataire}, vous avez encore {Delai} pour repondre a la mission {NumeroMission}."),
        Provider("MissionProviderRefusalRecorded", "Refus prestataire enregistre", "Refus enregistre", "Votre refus de la mission {NumeroMission} est enregistre. Elle ne vous sera plus proposee."),
        Provider("MissionClientConfirmed", "Client a confirme", "Mission confirmee", "Le client a confirme la mission {NumeroMission}. Preparez votre intervention."),
        Provider("MissionProviderOnTheWay", "Prestataire en route", "Trajet demarre", "Vous etes indique en route pour la mission {NumeroMission}."),
        Provider("MissionProviderArrived", "Prestataire arrive", "Arrivee confirmee", "Votre arrivee est enregistree pour la mission {NumeroMission}."),
        Provider("MissionTechnicianCanStart", "Debut mission autorise", "Vous pouvez demarrer", "Vous pouvez demarrer la mission {NumeroMission} a l'adresse {Adresse}."),
        Provider("MissionProviderStarted", "Prestation demarree", "Mission demarree", "Le debut de la mission {NumeroMission} est enregistre."),
        Provider("MissionProviderCompleted", "Prestation terminee", "Mission terminee", "La fin de la mission {NumeroMission} est enregistree."),
        Provider("MissionAdditionalQuoteNeeded", "Besoin devis complementaire", "Demande transmise", "Votre demande de devis complementaire pour {NumeroMission} a ete transmise a l'entreprise."),
        Provider("MissionAdditionalQuoteApproved", "Devis complementaire accepte", "Complement accepte", "Le client a accepte le devis complementaire pour {NumeroMission}."),
        Provider("MissionAdditionalQuoteRejected", "Devis complementaire refuse", "Complement refuse", "Le client a refuse le devis complementaire pour {NumeroMission}."),
        Provider("MissionCancelledProvider", "Mission annulee prestataire", "Mission annulee", "La mission {NumeroMission} est annulee. {Motif}"),
        Provider("MissionDisputeOpenedProvider", "Litige ouvert prestataire", "Litige ouvert", "Un litige est ouvert sur la mission {NumeroMission}. {Motif}"),
        Provider("ProviderProfileValidated", "Profil prestataire valide", "Profil valide", "{NomPrestataire}, votre profil est valide pour recevoir des missions."),
        Provider("ProviderProfileSuspended", "Profil prestataire suspendu", "Profil suspendu", "{NomPrestataire}, votre acces mission est suspendu. {Motif}"),
        Customer("MissionRequestCreated", "Demande client creee", "Demande recue", "Votre demande {Service} est recue. Nous cherchons une entreprise disponible."),
        Customer("MissionCompaniesContacted", "Entreprises contactez client", "Recherche en cours", "Des entreprises verifiees analysent votre demande {NumeroMission}."),
        Customer("MissionCompanyAnalyzing", "Entreprise analyse demande", "Analyse en cours", "{NomEntreprise} analyse votre demande {NumeroMission}."),
        Customer("MissionQuoteSentToCustomer", "Devis mission envoye au client", "Devis disponible", "Votre devis pour {Service} est disponible."),
        Customer("MissionQuoteExpired", "Devis expire client", "Devis expire", "Le devis de la mission {NumeroMission} a expire."),
        Customer("MissionPaymentRequired", "Paiement mission requis", "Votre paiement est attendu", "{NomTechnicien} a accepte la mission {NumeroMission}. Validez le prix de {Montant} et payez pour lancer l'intervention."),
        Customer("MissionQuoteAccepted", "Paiement client recu", "Paiement recu", "Votre paiement pour la mission {NumeroMission} est confirme."),
        Customer("MissionCommissionPaid", "Commission payee client", "Paiement initial confirme", "Votre paiement initial pour {NumeroMission} est confirme."),
        Customer("MissionStartPaymentReceived", "Paiement demarrage recu", "Paiement demarrage confirme", "Le paiement de demarrage pour {NumeroMission} est confirme."),
        Customer("MissionFinalPaymentReceived", "Paiement final recu", "Paiement final confirme", "Le paiement final pour {NumeroMission} est confirme."),
        Customer("MissionTechnicianAssigned", "Technicien affecte", "Technicien affecte", "{NomTechnicien} interviendra pour votre mission {NumeroMission}."),
        Customer("MissionTechnicianProposed", "Technicien propose", "Un technicien a ete trouve", "Nous avons trouve un technicien pour votre mission {NumeroMission}. Nous attendons sa confirmation."),
        Customer("MissionTechnicianOnTheWay", "Technicien en route", "Technicien en route", "{NomTechnicien} est en route vers {Adresse}."),
        Customer("MissionTechnicianArrived", "Technicien arrive", "Technicien arrive", "{NomTechnicien} est arrive pour la mission {NumeroMission}."),
        Customer("MissionStarted", "Mission demarree", "Mission demarree", "La mission {NumeroMission} a demarre."),
        Customer("MissionAdditionalQuoteAvailable", "Devis complementaire disponible", "Complement disponible", "Un devis complementaire de {Montant} est disponible pour {NumeroMission}."),
        Customer("MissionAdditionalQuotePaidByCustomer", "Devis complementaire paye client", "Complement paye", "Votre paiement complementaire de {Montant} est confirme pour {NumeroMission}."),
        Customer("MissionCompleted", "Mission terminee", "Mission terminee", "La mission {NumeroMission} est terminee. Vous pouvez valider et noter l'intervention."),
        Customer("MissionValidatedByCustomer", "Mission validee client", "Mission validee", "Merci, la mission {NumeroMission} est validee."),
        Customer("MissionReviewRequested", "Avis client demande", "Votre avis compte", "Notez la mission {NumeroMission}: qualite, ponctualite, politesse et proprete."),
        Customer("MissionReviewReceived", "Avis client recu", "Merci pour votre avis", "Votre avis sur la mission {NumeroMission} est enregistre."),
        Customer("MissionCancelled", "Mission annulee", "Mission annulee", "La mission {NumeroMission} a ete annulee. {Motif}"),
        Customer("MissionCancellationFeeApplied", "Frais annulation client", "Frais d'annulation", "Des frais de {Montant} sont appliques a la mission {NumeroMission}."),
        Customer("MissionDisputeOpenedCustomer", "Litige ouvert client", "Litige ouvert", "Votre litige sur la mission {NumeroMission} est enregistre."),
        Customer("MissionDisputeResolvedCustomer", "Litige resolu client", "Litige resolu", "Le litige de la mission {NumeroMission} est resolu. {Note}"),
        Customer("MissionRefundApproved", "Remboursement valide", "Remboursement valide", "Un remboursement de {Montant} est valide pour la mission {NumeroMission}."),
        Customer("MissionRefundSent", "Remboursement envoye", "Remboursement envoye", "Le remboursement de {Montant} pour {NumeroMission} a ete envoye."),
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
