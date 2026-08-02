using System.Globalization;
using System.Text;
using HomeService.Application.Abstractions;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientMissionInvoiceService(IAppDbContext db)
{
    public async Task<ClientMissionInvoiceResult> GenerateAsync(Guid customerId, Guid missionId, CancellationToken cancellationToken)
    {
        var mission = await db.Missions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == missionId && x.CustomerId == customerId, cancellationToken);
        if (mission is null) return ClientMissionInvoiceResult.NotFound("Mission introuvable.");
        if (mission.PaymentStatus is not (PaymentStatus.Authorized or PaymentStatus.Paid))
            return ClientMissionInvoiceResult.Invalid("La facture sera disponible apres le paiement.");

        var customer = await db.Customers.AsNoTracking().FirstAsync(x => x.Id == customerId, cancellationToken);
        var address = await db.CustomerAddresses.AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.IsDefault)
            .Select(x => x.AddressLine)
            .FirstOrDefaultAsync(cancellationToken) ?? mission.ServiceAddress ?? "Adresse non renseignee";
        var service = await db.Services.AsNoTracking().Where(x => x.Id == mission.ServiceId).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken) ?? "Service";
        var prestation = mission.ServicePrestationId.HasValue
            ? await db.ServicePrestations.AsNoTracking().Where(x => x.Id == mission.ServicePrestationId.Value).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken)
            : null;
        var option = mission.ServiceOptionId.HasValue
            ? await db.ServiceOptions.AsNoTracking().Where(x => x.Id == mission.ServiceOptionId.Value).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken)
            : null;
        var amount = mission.FinalTotalAmount ?? mission.CompanyQuotedAmount ?? mission.EstimatedTotalAmount ?? 0;
        var invoiceDate = mission.CustomerConfirmedAt ?? DateTimeOffset.UtcNow;
        var bytes = BasicPdfInvoice.Create(new InvoiceData(
            mission.MissionNumber, invoiceDate, customer.FirstName + " " + customer.LastName, address,
            service, prestation, option, mission.Description, amount, mission.Currency));
        return ClientMissionInvoiceResult.Ok(bytes, $"facture-wele-{mission.MissionNumber}.pdf");
    }

    private sealed record InvoiceData(string Number, DateTimeOffset Date, string CustomerName, string Address,
        string Service, string? Prestation, string? Option, string? Description, int Amount, string Currency);

    private static class BasicPdfInvoice
    {
        public static byte[] Create(InvoiceData data)
        {
            var content = new StringBuilder();
            void Text(int x, int y, int size, string value, bool bold = false) =>
                content.AppendLine($"BT /{(bold ? "F2" : "F1")} {size} Tf {x} {y} Td ({Escape(value)}) Tj ET");
            content.AppendLine("0.12 0.38 0.92 rg 42 760 30 30 re f");
            Text(51, 770, 15, "w", true); Text(82, 767, 24, "wele", true);
            Text(42, 720, 22, "FACTURE", true);
            Text(390, 724, 10, $"N° {data.Number}");
            Text(390, 707, 10, data.Date.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));
            Text(42, 665, 10, "FACTURE A", true);
            Text(42, 646, 13, data.CustomerName, true);
            Text(42, 628, 10, data.Address);
            content.AppendLine("0.88 G 42 590 m 553 590 l S");
            Text(42, 565, 10, "DETAIL DE LA PRESTATION", true);
            Text(42, 538, 11, "Service"); Text(240, 538, 11, data.Service, true);
            var y = 514;
            if (!string.IsNullOrWhiteSpace(data.Prestation)) { Text(42, y, 11, "Prestation"); Text(240, y, 11, data.Prestation!, true); y -= 24; }
            if (!string.IsNullOrWhiteSpace(data.Option)) { Text(42, y, 11, "Option"); Text(240, y, 11, data.Option!, true); y -= 24; }
            if (!string.IsNullOrWhiteSpace(data.Description)) { Text(42, y, 11, "Description"); Text(240, y, 10, Trim(data.Description!, 52)); y -= 30; }
            content.AppendLine($"0.12 0.38 0.92 rg 330 {y - 22} 223 48 re f");
            Text(348, y - 5, 12, "TOTAL PAYE", true);
            Text(455, y - 5, 14, $"{data.Amount:N0} {data.Currency}", true);
            Text(42, 90, 9, "Merci pour votre confiance.");
            Text(42, 72, 8, "Wele - Plateforme de services a domicile");
            return BuildPdf(content.ToString());
        }

        private static byte[] BuildPdf(string stream)
        {
            var objects = new[]
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> /Contents 4 0 R >>",
                $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>"
            };
            using var output = new MemoryStream();
            using var writer = new StreamWriter(output, Encoding.ASCII, 1024, true) { NewLine = "\n" };
            writer.Write("%PDF-1.4\n"); writer.Flush();
            var offsets = new List<long> { 0 };
            for (var i = 0; i < objects.Length; i++) { offsets.Add(output.Position); writer.Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); writer.Flush(); }
            var xref = output.Position;
            writer.Write($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
            foreach (var offset in offsets.Skip(1)) writer.Write($"{offset:0000000000} 00000 n \n");
            writer.Write($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF"); writer.Flush();
            return output.ToArray();
        }

        private static string Escape(string value) => Trim(value, 85).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        private static string Trim(string value, int max) => value.Length <= max ? value : value[..(max - 3)] + "...";
    }
}

public sealed record ClientMissionInvoiceResult(bool IsSuccess, bool IsNotFound, byte[]? Content, string? FileName, string Message)
{
    public static ClientMissionInvoiceResult Ok(byte[] content, string fileName) => new(true, false, content, fileName, string.Empty);
    public static ClientMissionInvoiceResult NotFound(string message) => new(false, true, null, null, message);
    public static ClientMissionInvoiceResult Invalid(string message) => new(false, false, null, null, message);
}
