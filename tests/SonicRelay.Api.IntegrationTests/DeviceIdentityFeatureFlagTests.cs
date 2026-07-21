using System.Net;
using System.Net.Http.Json;
using SonicRelay.Api.Contracts;
using Xunit;

namespace SonicRelay.Api.IntegrationTests;

public sealed class DeviceIdentityFeatureFlagTests
{
    [Fact]
    public async Task Disabling_DeviceIdentity_Removes_New_Endpoints_Without_Affecting_Identity_Auth()
    {
        await using var factory = new SonicRelayApiFactory(new Dictionary<string, string?>
        {
            ["DeviceIdentity:Enabled"] = "false"
        });
        var client = factory.CreateClient();

        var bootstrapResponse = await client.PostAsJsonAsync("/api/devices/bootstrap",
            new BootstrapDeviceRequest("Device", "windows_publisher", "windows"));
        Assert.Equal(HttpStatusCode.NotFound, bootstrapResponse.StatusCode);

        var meResponse = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }
}
