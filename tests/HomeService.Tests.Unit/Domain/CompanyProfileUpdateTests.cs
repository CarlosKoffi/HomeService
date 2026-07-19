using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class CompanyProfileUpdateTests
{
    [Fact]
    public void Company_UpdateCompanyInformation_TrimsAndStoresProfileDetails()
    {
        var company = new Company("Ancien Nom", "+2250700000000", "old@kaza.ci");

        company.UpdateCompanyInformation(
            "  CI Home Service  ",
            "  SARL  ",
            "  RCCM-CI-ABJ-2026  ",
            "  DFE-2026-001  ",
            "  Abidjan  ",
            "  Cocody Angre  ");

        Assert.Equal("CI Home Service", company.Name);
        Assert.Equal("SARL", company.LegalForm);
        Assert.Equal("RCCM-CI-ABJ-2026", company.RegistrationNumber);
        Assert.Equal("DFE-2026-001", company.TaxIdentificationNumber);
        Assert.Equal("Abidjan", company.City);
        Assert.Equal("Cocody Angre", company.Address);
    }

    [Fact]
    public void CompanyApplication_UpdateOperations_AllowsClearingOptionalValues()
    {
        var application = CreateApplication();
        application.UpdateOperations("Cocody, Marcory", "Menage, Jardinage");

        application.UpdateOperations(" ", null);

        Assert.Null(application.InterventionZones);
        Assert.Null(application.PlannedServices);
    }

    [Fact]
    public void Company_UpdatePayment_CleansBlankPaymentNumbers()
    {
        var company = new Company("CI Home Service", "+2250700000000", "direction@entreprise.ci");

        company.UpdatePayment(" +2250701020304 ", " ");

        Assert.Equal("+2250701020304", company.WavePaymentNumber);
        Assert.Null(company.OrangeMoneyPaymentNumber);
    }

    [Fact]
    public void Company_SetInterimApplications_StoresCompanyChoice()
    {
        var company = new Company("CI Home Service", "+2250700000000", "direction@entreprise.ci");

        company.SetInterimApplications(true);

        Assert.True(company.AcceptsInterimApplications);

        company.SetInterimApplications(false);

        Assert.False(company.AcceptsInterimApplications);
    }

    [Fact]
    public void Company_SuspendAndApprove_UpdatesOperationalStatus()
    {
        var company = new Company("CI Home Service", "+2250700000000", "direction@entreprise.ci");
        company.Approve();

        company.Suspend();

        Assert.Equal(CompanyStatus.Suspended, company.Status);

        company.Approve();

        Assert.Equal(CompanyStatus.Approved, company.Status);
    }

    private static CompanyApplication CreateApplication()
    {
        return new CompanyApplication(
            "CI Home Service",
            null,
            "Abidjan",
            "Cocody",
            "John Pripri",
            "direction@entreprise.ci",
            "+2250701020304",
            "Menage",
            12);
    }
}
