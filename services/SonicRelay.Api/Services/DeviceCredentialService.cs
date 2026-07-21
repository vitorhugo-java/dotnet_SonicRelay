using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SonicRelay.Domain.Devices;

namespace SonicRelay.Api.Services;

public sealed class DeviceCredentialService(IOptions<DeviceIdentityOptions> options, TimeProvider time)
{
    public (string PlaintextSecret, string SecretHash) GenerateCredential()
    {
        var plaintext = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return (plaintext, HashSecret(plaintext));
    }

    public string HashSecret(string plaintextSecret)
    {
        var key = RequireKey(options.Value.CredentialHmacKey, nameof(DeviceIdentityOptions.CredentialHmacKey));
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(plaintextSecret));
        return Convert.ToHexString(hash);
    }

    public bool VerifySecret(string plaintextSecret, string secretHash)
    {
        var computed = Convert.FromHexString(HashSecret(plaintextSecret));
        var expected = Convert.FromHexString(secretHash);
        return CryptographicOperations.FixedTimeEquals(computed, expected);
    }

    public static IReadOnlyList<string> ScopesFor(string deviceType) => deviceType switch
    {
        DeviceTypes.WindowsPublisher => ["device:read", "device:manage", "pairing:create", "pairing:revoke"],
        DeviceTypes.FlutterViewer => ["device:read", "device:manage", "pairing:complete", "pairing:revoke"],
        _ => []
    };

    internal static string RequireKey(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"DeviceIdentity:{name} must be configured.")
            : value;
}
