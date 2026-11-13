using System.Net;

namespace RadarPlural.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class HealthTests(ApiFixture fx)
{
    [Fact]
    public async Task LivenessAnswersOk()
    {
        var response = await fx.Api.Browser().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
