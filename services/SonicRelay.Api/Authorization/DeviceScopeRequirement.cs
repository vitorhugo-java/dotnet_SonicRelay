using Microsoft.AspNetCore.Authorization;

namespace SonicRelay.Api.Authorization;

public sealed class DeviceScopeRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}
