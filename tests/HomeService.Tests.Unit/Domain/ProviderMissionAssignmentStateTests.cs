using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class ProviderMissionAssignmentStateTests
{
    private static readonly Guid MissionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ProviderId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CompanyId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public void Accept_WhenAssignmentIsExpired_MarksExpiredAndThrows()
    {
        var assignment = new ProviderMissionAssignment(MissionId, ProviderId, CompanyId, DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.Throws<InvalidOperationException>(() => assignment.Accept());
        Assert.Equal(ProviderMissionAssignmentStatus.Expired, assignment.Status);
    }

    [Fact]
    public void Refuse_WhenAssignmentAlreadyAccepted_Throws()
    {
        var assignment = CreateOfferedAssignment();
        assignment.Accept();

        Assert.Throws<InvalidOperationException>(() => assignment.Refuse(ProviderMissionRefusalReason.TooFar, "Trop loin"));
        Assert.Equal(ProviderMissionAssignmentStatus.Accepted, assignment.Status);
    }

    [Fact]
    public void Start_WhenArrivalIsNotVerified_Throws()
    {
        var assignment = CreateOfferedAssignment();
        assignment.Accept();

        Assert.Throws<InvalidOperationException>(assignment.Start);
        Assert.Equal(ProviderMissionAssignmentStatus.Accepted, assignment.Status);
    }

    [Fact]
    public void Complete_WhenAssignmentIsNotStarted_Throws()
    {
        var assignment = CreateOfferedAssignment();
        assignment.Accept();

        Assert.Throws<InvalidOperationException>(() => assignment.Complete("Fin", null));
        Assert.Equal(ProviderMissionAssignmentStatus.Accepted, assignment.Status);
    }

    [Fact]
    public void Complete_WhenAssignmentIsStarted_MarksCompleted()
    {
        var assignment = CreateOfferedAssignment();
        assignment.Accept();
        assignment.VerifyArrival(5.348850m, -4.003150m, 25, 5.348850m, -4.003150m, 250);
        assignment.Start();

        assignment.Complete("Client satisfait", "storage/photo.jpg");

        Assert.Equal(ProviderMissionAssignmentStatus.Completed, assignment.Status);
        Assert.Equal("Client satisfait", assignment.CompletionNote);
        Assert.Equal("storage/photo.jpg", assignment.CompletionPhotoPath);
        Assert.NotNull(assignment.CompletedAt);
    }

    private static ProviderMissionAssignment CreateOfferedAssignment()
    {
        return new ProviderMissionAssignment(MissionId, ProviderId, CompanyId, DateTimeOffset.UtcNow.AddMinutes(3));
    }
}
