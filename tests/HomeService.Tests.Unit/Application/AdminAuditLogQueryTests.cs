using HomeService.Application.Admin;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminAuditLogQueryTests
{
    [Theory]
    [InlineData(null, 50)]
    [InlineData(-10, 1)]
    [InlineData(0, 1)]
    [InlineData(25, 25)]
    [InlineData(500, 200)]
    public void NormalizePageSize_ClampsAdminPageSize(int? take, int expected)
    {
        Assert.Equal(expected, AdminAuditLogQuery.NormalizePageSize(take));
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(20, 20)]
    public void NormalizeOffset_NeverReturnsNegativeOffset(int? skip, int expected)
    {
        Assert.Equal(expected, AdminAuditLogQuery.NormalizeOffset(skip));
    }
}
