using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class ProviderDocumentTests
{
    [Fact]
    public void ReplaceFile_UpdatesStoredFileMetadata()
    {
        var document = new ProviderDocument(
            Guid.NewGuid(),
            ProviderDocumentType.IdentityDocument,
            "old.pdf",
            "providers/company/provider/old.pdf",
            "application/pdf");

        document.ReplaceFile(
            "new.png",
            "providers/company/provider/new.png",
            "image/png");

        Assert.Equal("new.png", document.OriginalFileName);
        Assert.Equal("providers/company/provider/new.png", document.StoragePath);
        Assert.Equal("image/png", document.ContentType);
        Assert.NotNull(document.UpdatedAt);
    }
}
