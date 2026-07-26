using HomeService.Application.Notifications;

namespace HomeService.Tests.Unit.Application;

public sealed class NotificationTemplateRendererTests
{
    [Fact]
    public void Render_ReplacesKnownVariablesAndKeepsUnknownVariablesVisible()
    {
        var variables = NotificationTemplateRenderer.Variables(
            ("NomEntreprise", "CI Home Service"),
            ("Service", "Menage"));

        var rendered = NotificationTemplateRenderer.Render(
            "{NomEntreprise} propose une mission {Service} numero {NumeroMission}.",
            "fallback",
            variables);

        Assert.Equal("CI Home Service propose une mission Menage numero {NumeroMission}.", rendered);
    }

    [Fact]
    public void Render_UsesFallbackWhenTemplateIsEmpty()
    {
        var variables = NotificationTemplateRenderer.Variables(("Service", "Jardinage"));

        var rendered = NotificationTemplateRenderer.Render(null, "Mission {Service}", variables);

        Assert.Equal("Mission Jardinage", rendered);
    }
}
