using HomeService.Application.CompanyPortal;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyComplianceDocumentUploadResultTests
{
    [Fact]
    public void Ok_CarriesDocumentCountAndTypes()
    {
        var applicationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var result = CompanyComplianceDocumentUploadResult.Ok(applicationId, 2, ["BusinessRegistration", "OwnerIdentity"]);

        Assert.Equal(CompanyComplianceDocumentStatus.Ok, result.Status);
        Assert.Equal(applicationId, result.ApplicationId);
        Assert.Equal(2, result.DocumentCount);
        Assert.Equal(["BusinessRegistration", "OwnerIdentity"], result.DocumentTypes);
    }

    [Fact]
    public void NoValidDocument_ReturnsBusinessMessage()
    {
        var result = CompanyComplianceDocumentUploadResult.NoValidDocument();

        Assert.Equal(CompanyComplianceDocumentStatus.NoValidDocument, result.Status);
        Assert.Equal("Aucun document valide n'a ete transmis.", result.Message);
    }
}
