using HomeService.Application.Abstractions;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.CompanyPortal;

public sealed class CompanyWalletService(
    IAppDbContext db,
    IPayoutDataProtector payoutDataProtector,
    ICompanyPayoutGateway payoutGateway)
{
    private const int MinimumPayoutAmount = 5_000;

    public async Task CreditMissionAsync(Mission mission, CancellationToken cancellationToken)
    {
        if (mission.CompanyId is null || mission.CompanyPayoutAmount <= 0 || mission.CompanyPayoutReleasedAt is null)
        {
            return;
        }

        var idempotencyKey = $"mission:{mission.Id:N}:company-payout";
        if (await db.CompanyWalletEntries.AnyAsync(entry => entry.IdempotencyKey == idempotencyKey, cancellationToken))
        {
            return;
        }

        var company = await db.Companies.FirstAsync(company => company.Id == mission.CompanyId.Value, cancellationToken);
        var wallet = await GetOrCreateWalletAsync(company.Id, mission.Currency, cancellationToken);
        var eligibleAt = CompanySettlementCalendar.GetEligibilityDate(
            mission.CompanyPayoutReleasedAt.Value,
            company.SettlementFrequency);

        wallet.CreditPending(mission.CompanyPayoutAmount);
        db.CompanyWalletEntries.Add(new CompanyWalletEntry(
            company.Id,
            wallet.Id,
            CompanyWalletEntryType.MissionCreditPending,
            mission.CompanyPayoutAmount,
            idempotencyKey,
            $"Mission {mission.MissionNumber} validee - reversement en attente d'echeance.",
            eligibleAt,
            mission.Id,
            currency: mission.Currency));
    }

    public async Task<int> PromoteDueFundsAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
    {
        var dueEntries = await db.CompanyWalletEntries
            .Where(entry => entry.Type == CompanyWalletEntryType.MissionCreditPending
                && entry.EligibleAt != null
                && entry.EligibleAt <= now)
            .OrderBy(entry => entry.EligibleAt)
            .Take(Math.Clamp(batchSize, 1, 500))
            .ToListAsync(cancellationToken);

        var promoted = 0;
        foreach (var entry in dueEntries)
        {
            var releaseKey = $"release:{entry.Id:N}";
            if (await db.CompanyWalletEntries.AnyAsync(item => item.IdempotencyKey == releaseKey, cancellationToken))
            {
                continue;
            }

            var wallet = await db.CompanyWallets.FirstAsync(item => item.Id == entry.WalletId, cancellationToken);
            wallet.MakeAvailable(entry.Amount);
            db.CompanyWalletEntries.Add(new CompanyWalletEntry(
                entry.CompanyId,
                wallet.Id,
                CompanyWalletEntryType.FundsBecameAvailable,
                entry.Amount,
                releaseKey,
                "Fonds devenus disponibles selon le calendrier de reversement.",
                missionId: entry.MissionId,
                currency: entry.Currency));
            promoted++;
        }

        if (promoted > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return promoted;
    }

    public async Task<CompanyWalletOperationResult> GetAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await db.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return CompanyWalletOperationResult.Fail("Entreprise introuvable.");
        }

        var wallet = await GetOrCreateWalletAsync(companyId, "XOF", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var destinations = await db.CompanyPayoutDestinations.AsNoTracking()
            .Where(item => item.CompanyId == companyId)
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var payouts = await db.CompanyPayoutRequests.AsNoTracking()
            .Where(item => item.CompanyId == companyId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(30)
            .ToListAsync(cancellationToken);
        var entries = await db.CompanyWalletEntries.AsNoTracking()
            .Where(item => item.CompanyId == companyId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return CompanyWalletOperationResult.Ok(new CompanyWalletResponse(
            wallet.PendingBalance,
            wallet.AvailableBalance,
            wallet.ReservedBalance,
            wallet.WithdrawnBalance,
            wallet.Currency,
            company.SettlementFrequency.ToString(),
            CompanySettlementCalendar.GetEligibilityDate(DateTimeOffset.UtcNow, company.SettlementFrequency),
            destinations.Select(ToResponse).ToList(),
            payouts.Select(ToResponse).ToList(),
            entries.Select(ToResponse).ToList()));
    }

    public async Task<CompanyWalletOperationResult> ChangeFrequencyAsync(
        Guid companyId,
        UpdateCompanySettlementFrequencyRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CompanySettlementFrequency>(request.Frequency, true, out var frequency)
            || frequency is not (CompanySettlementFrequency.Fortnightly or CompanySettlementFrequency.Monthly))
        {
            return CompanyWalletOperationResult.Fail("Frequence invalide. Utilisez Fortnightly ou Monthly.");
        }

        var company = await db.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return CompanyWalletOperationResult.Fail("Entreprise introuvable.");
        }

        company.ChangeSettlementFrequency(frequency);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(companyId, cancellationToken);
    }

    public async Task<CompanyWalletOperationResult> AddDestinationAsync(
        Guid companyId,
        CreateCompanyPayoutDestinationRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CompanyPayoutMethod>(request.Method, true, out var method))
        {
            return CompanyWalletOperationResult.Fail("Mode de reversement invalide.");
        }

        if (!await db.Companies.AnyAsync(item => item.Id == companyId, cancellationToken))
        {
            return CompanyWalletOperationResult.Fail("Entreprise introuvable.");
        }

        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            return CompanyWalletOperationResult.Fail("Le numero ou compte beneficiaire est obligatoire.");
        }

        if (request.IsDefault)
        {
            var currentDefaults = await db.CompanyPayoutDestinations
                .Where(item => item.CompanyId == companyId && item.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (var current in currentDefaults)
            {
                current.MarkDefault(false);
            }
        }

        var destination = new CompanyPayoutDestination(
            companyId,
            method,
            request.Label,
            request.BeneficiaryName,
            request.ProviderCode,
            payoutDataProtector.Protect(request.Identifier.Trim()),
            MaskIdentifier(request.Identifier),
            request.IsDefault);
        db.CompanyPayoutDestinations.Add(destination);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(companyId, cancellationToken);
    }

    public async Task<CompanyWalletOperationResult> RequestPayoutAsync(
        Guid companyId,
        CreateCompanyPayoutRequest request,
        CancellationToken cancellationToken)
    {
        var company = await db.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        var wallet = await db.CompanyWallets.FirstOrDefaultAsync(item => item.CompanyId == companyId, cancellationToken);
        var destination = await db.CompanyPayoutDestinations.FirstOrDefaultAsync(
            item => item.Id == request.DestinationId && item.CompanyId == companyId && item.IsActive,
            cancellationToken);

        if (company is null || wallet is null || destination is null)
        {
            return CompanyWalletOperationResult.Fail("Compte de reversement incomplet.");
        }

        if (!destination.IsVerified)
        {
            return CompanyWalletOperationResult.Fail("Le beneficiaire doit etre verifie avant le premier reversement.");
        }

        var amount = request.Amount ?? wallet.AvailableBalance;
        if (amount < MinimumPayoutAmount)
        {
            return CompanyWalletOperationResult.Fail($"Le minimum de reversement est de {MinimumPayoutAmount:N0} XOF.");
        }

        if (amount > wallet.AvailableBalance)
        {
            return CompanyWalletOperationResult.Fail("Le montant depasse le solde disponible.");
        }

        var fee = CompanyPayoutFeeCalculator.Calculate(destination.Method, amount);
        if (fee >= amount)
        {
            return CompanyWalletOperationResult.Fail("Le montant est insuffisant apres deduction des frais.");
        }

        var period = CompanySettlementCalendar.GetClosedPeriod(DateTimeOffset.UtcNow, company.SettlementFrequency);
        var payout = new CompanyPayoutRequest(
            companyId,
            destination.Id,
            destination.Method,
            company.SettlementFrequency,
            amount,
            fee,
            period.Start,
            period.End,
            wallet.Currency);
        wallet.Reserve(amount);
        db.CompanyPayoutRequests.Add(payout);
        db.CompanyWalletEntries.Add(new CompanyWalletEntry(
            companyId,
            wallet.Id,
            CompanyWalletEntryType.PayoutReserved,
            amount,
            $"payout:{payout.Id:N}:reserved",
            $"Reversement {payout.Reference} reserve.",
            payoutRequestId: payout.Id,
            currency: wallet.Currency));
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(companyId, cancellationToken);
    }

    public async Task<bool> VerifyDestinationAsync(Guid destinationId, string? externalContactId, CancellationToken cancellationToken)
    {
        var destination = await db.CompanyPayoutDestinations.FirstOrDefaultAsync(item => item.Id == destinationId, cancellationToken);
        if (destination is null)
        {
            return false;
        }

        destination.MarkVerified(externalContactId);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ApprovePayoutAsync(Guid payoutId, CancellationToken cancellationToken)
    {
        var payout = await db.CompanyPayoutRequests.FirstOrDefaultAsync(item => item.Id == payoutId, cancellationToken);
        if (payout is null)
        {
            return false;
        }

        if (payout.Status == CompanyPayoutStatus.Approved)
        {
            return true;
        }

        if (payout.Status != CompanyPayoutStatus.Submitted)
        {
            return false;
        }

        payout.Approve();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RejectPayoutAsync(Guid payoutId, string? reason, CancellationToken cancellationToken)
    {
        var payout = await db.CompanyPayoutRequests.FirstOrDefaultAsync(item => item.Id == payoutId, cancellationToken);
        if (payout is null)
        {
            return false;
        }

        if (payout.Status == CompanyPayoutStatus.Rejected)
        {
            return true;
        }

        if (payout.Status != CompanyPayoutStatus.Submitted)
        {
            return false;
        }

        var wallet = await db.CompanyWallets.FirstAsync(item => item.CompanyId == payout.CompanyId, cancellationToken);
        payout.Reject(reason ?? "Reversement rejete par l'administration.");
        wallet.ReleaseReservation(payout.GrossAmount);
        db.CompanyWalletEntries.Add(new CompanyWalletEntry(
            payout.CompanyId,
            wallet.Id,
            CompanyWalletEntryType.PayoutFailed,
            payout.GrossAmount,
            $"payout:{payout.Id:N}:rejected",
            $"Reservation liberee apres rejet du reversement {payout.Reference}.",
            payoutRequestId: payout.Id,
            currency: payout.Currency));
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CompleteCashPayoutAsync(
        Guid payoutId,
        string? proofReference,
        CancellationToken cancellationToken)
    {
        var payout = await db.CompanyPayoutRequests.FirstOrDefaultAsync(item => item.Id == payoutId, cancellationToken);
        if (payout is null
            || payout.Method != CompanyPayoutMethod.Cash
            || string.IsNullOrWhiteSpace(proofReference))
        {
            return false;
        }

        if (payout.Status == CompanyPayoutStatus.Paid)
        {
            return true;
        }

        if (payout.Status is not (CompanyPayoutStatus.Submitted or CompanyPayoutStatus.Approved))
        {
            return false;
        }

        if (payout.Status == CompanyPayoutStatus.Submitted)
        {
            payout.Approve();
        }

        var wallet = await db.CompanyWallets.FirstAsync(item => item.CompanyId == payout.CompanyId, cancellationToken);
        payout.MarkPaid(proofReference);
        wallet.CompletePayout(payout.GrossAmount);
        db.CompanyWalletEntries.Add(new CompanyWalletEntry(
            payout.CompanyId,
            wallet.Id,
            CompanyWalletEntryType.PayoutPaid,
            payout.GrossAmount,
            $"payout:{payout.Id:N}:paid",
            $"Reversement cash {payout.Reference} remis avec preuve.",
            payoutRequestId: payout.Id,
            currency: payout.Currency));
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> ProcessApprovedPayoutsAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (!payoutGateway.IsEnabled)
        {
            return 0;
        }

        var payouts = await db.CompanyPayoutRequests
            .Include(item => item.Destination)
            .Where(item => item.Status == CompanyPayoutStatus.Approved && item.Method != CompanyPayoutMethod.Cash)
            .OrderBy(item => item.ApprovedAt)
            .Take(Math.Clamp(batchSize, 1, 50))
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var payout in payouts)
        {
            var destination = payout.Destination!;
            var result = await payoutGateway.CreateAsync(new CompanyPayoutGatewayRequest(
                payout.Reference,
                payout.Method,
                destination.ProviderCode,
                destination.BeneficiaryName,
                payoutDataProtector.Unprotect(destination.ProtectedDetails),
                payout.NetAmount,
                payout.Currency), cancellationToken);

            if (!result.IsAccepted)
            {
                if (result.IsFinal)
                {
                    await FailAndReleaseAsync(payout, result.Message ?? result.Status, cancellationToken);
                    processed++;
                }

                continue;
            }

            payout.MarkProcessing(result.ExternalTransactionId ?? payout.Reference);
            if (result.IsFinal)
            {
                if (result.IsSuccessful)
                {
                    await CompleteAndWithdrawAsync(payout, result.ExternalTransactionId, cancellationToken);
                }
                else
                {
                    await FailAndReleaseAsync(payout, result.Message ?? result.Status, cancellationToken);
                }
            }
            else
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            processed++;
        }

        return processed;
    }

    public async Task<int> ReconcileProcessingPayoutsAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (!payoutGateway.IsEnabled)
        {
            return 0;
        }

        var payouts = await db.CompanyPayoutRequests
            .Where(item => item.Status == CompanyPayoutStatus.Processing && item.ExternalTransactionId != null)
            .OrderBy(item => item.ProcessingAt)
            .Take(Math.Clamp(batchSize, 1, 50))
            .ToListAsync(cancellationToken);
        var reconciled = 0;
        foreach (var payout in payouts)
        {
            var result = await payoutGateway.GetStatusAsync(payout.ExternalTransactionId!, cancellationToken);
            if (!result.IsFinal)
            {
                continue;
            }

            if (result.IsSuccessful)
            {
                await CompleteAndWithdrawAsync(payout, result.ExternalTransactionId, cancellationToken);
            }
            else
            {
                await FailAndReleaseAsync(payout, result.Message ?? result.Status, cancellationToken);
            }

            reconciled++;
        }

        return reconciled;
    }

    public async Task<bool> ApplyExternalStatusAsync(
        string externalTransactionId,
        string status,
        string? message,
        CancellationToken cancellationToken)
    {
        var payout = await db.CompanyPayoutRequests.FirstOrDefaultAsync(
            item => item.ExternalTransactionId == externalTransactionId || item.Reference == externalTransactionId,
            cancellationToken);
        if (payout is null)
        {
            return false;
        }

        var normalized = status.Trim().ToLowerInvariant();
        if (normalized is "success" or "successful" or "paid" or "completed")
        {
            await CompleteAndWithdrawAsync(payout, externalTransactionId, cancellationToken);
        }
        else if (normalized is "failed" or "rejected" or "cancelled" or "canceled" or "error")
        {
            await FailAndReleaseAsync(payout, message ?? status, cancellationToken);
        }
        else
        {
            payout.MarkProcessing(externalTransactionId);
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private async Task CompleteAndWithdrawAsync(
        CompanyPayoutRequest payout,
        string? proofReference,
        CancellationToken cancellationToken)
    {
        var key = $"payout:{payout.Id:N}:paid";
        if (await db.CompanyWalletEntries.AnyAsync(item => item.IdempotencyKey == key, cancellationToken))
        {
            return;
        }

        var wallet = await db.CompanyWallets.FirstAsync(item => item.CompanyId == payout.CompanyId, cancellationToken);
        payout.MarkPaid(proofReference);
        wallet.CompletePayout(payout.GrossAmount);
        db.CompanyWalletEntries.Add(new CompanyWalletEntry(
            payout.CompanyId,
            wallet.Id,
            CompanyWalletEntryType.PayoutPaid,
            payout.GrossAmount,
            key,
            $"Reversement {payout.Reference} confirme par la passerelle.",
            payoutRequestId: payout.Id,
            currency: payout.Currency));
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task FailAndReleaseAsync(
        CompanyPayoutRequest payout,
        string reason,
        CancellationToken cancellationToken)
    {
        var key = $"payout:{payout.Id:N}:failed";
        if (await db.CompanyWalletEntries.AnyAsync(item => item.IdempotencyKey == key, cancellationToken))
        {
            return;
        }

        var wallet = await db.CompanyWallets.FirstAsync(item => item.CompanyId == payout.CompanyId, cancellationToken);
        payout.MarkFailed(reason);
        wallet.ReleaseReservation(payout.GrossAmount);
        db.CompanyWalletEntries.Add(new CompanyWalletEntry(
            payout.CompanyId,
            wallet.Id,
            CompanyWalletEntryType.PayoutFailed,
            payout.GrossAmount,
            key,
            $"Reversement {payout.Reference} echoue : reservation restituee.",
            payoutRequestId: payout.Id,
            currency: payout.Currency));
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<CompanyWallet> GetOrCreateWalletAsync(Guid companyId, string currency, CancellationToken cancellationToken)
    {
        var wallet = await db.CompanyWallets.FirstOrDefaultAsync(item => item.CompanyId == companyId, cancellationToken);
        if (wallet is not null)
        {
            return wallet;
        }

        wallet = new CompanyWallet(companyId, currency);
        db.CompanyWallets.Add(wallet);
        return wallet;
    }

    private static string MaskIdentifier(string identifier)
    {
        var normalized = new string(identifier.Where(char.IsLetterOrDigit).ToArray());
        if (normalized.Length <= 4)
        {
            return new string('*', normalized.Length);
        }

        return $"{new string('*', normalized.Length - 4)}{normalized[^4..]}";
    }

    private static CompanyPayoutDestinationResponse ToResponse(CompanyPayoutDestination item) => new(
        item.Id, item.Method.ToString(), item.Label, item.BeneficiaryName, item.ProviderCode,
        item.MaskedIdentifier, item.IsDefault, item.IsVerified, item.IsActive);

    private static CompanyPayoutResponse ToResponse(CompanyPayoutRequest item) => new(
        item.Id, item.Reference, item.Method.ToString(), item.Status.ToString(), item.GrossAmount,
        item.FeeAmount, item.NetAmount, item.Currency, item.PeriodStart, item.PeriodEnd,
        item.CreatedAt, item.PaidAt, item.FailureReason);

    private static CompanyWalletEntryResponse ToResponse(CompanyWalletEntry item) => new(
        item.Id, item.Type.ToString(), item.Amount, item.Currency, item.Description, item.EligibleAt,
        item.CreatedAt, item.MissionId, item.PayoutRequestId);
}

public sealed record CompanyWalletOperationResult(bool IsSuccess, CompanyWalletResponse? Response, string? Message)
{
    public static CompanyWalletOperationResult Ok(CompanyWalletResponse response) => new(true, response, null);
    public static CompanyWalletOperationResult Fail(string message) => new(false, null, message);
}
