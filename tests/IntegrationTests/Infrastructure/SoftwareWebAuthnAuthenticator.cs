using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// Minimal platform authenticator used to exercise Fido2NetLib cryptographically. It emits
/// standards-shaped none attestation and ES256 assertions; application tests therefore do
/// not replace or bypass the production verifier.
/// </summary>
internal sealed class SoftwareWebAuthnAuthenticator : IDisposable
{
    private const string Origin = "http://localhost:5035";
    private const string RelyingPartyId = "localhost";

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly byte[] _credentialId = RandomNumberGenerator.GetBytes(32);
    private uint _signCount;

    public string CredentialId => WebEncoders.Base64UrlEncode(_credentialId);

    public JsonElement CreateAttestation(JsonElement options)
    {
        var clientData = ClientData(
            "webauthn.create",
            options.GetProperty("challenge").GetString()!);
        var authenticatorData = RegistrationAuthenticatorData();
        var attestationObject = Cbor.Map(
            Cbor.Text("fmt"), Cbor.Text("none"),
            Cbor.Text("attStmt"), Cbor.EmptyMap,
            Cbor.Text("authData"), Cbor.Bytes(authenticatorData));

        return JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["id"] = CredentialId,
            ["rawId"] = CredentialId,
            ["type"] = "public-key",
            ["response"] = new Dictionary<string, object?>
            {
                ["attestationObject"] = WebEncoders.Base64UrlEncode(attestationObject),
                ["clientDataJSON"] = WebEncoders.Base64UrlEncode(clientData),
                ["transports"] = new[] { "internal" },
            },
            ["clientExtensionResults"] = new Dictionary<string, object?>(),
        });
    }

    public JsonElement CreateAssertion(JsonElement options, Guid userId, bool advanceCounter = true)
    {
        if (advanceCounter)
        {
            _signCount++;
        }

        var clientData = ClientData(
            "webauthn.get",
            options.GetProperty("challenge").GetString()!);
        var authenticatorData = AssertionAuthenticatorData();
        var signedData = new byte[authenticatorData.Length + SHA256.HashSizeInBytes];
        authenticatorData.CopyTo(signedData, 0);
        SHA256.HashData(clientData).CopyTo(signedData, authenticatorData.Length);
        var signature = _key.SignData(
            signedData,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);

        return JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["id"] = CredentialId,
            ["rawId"] = CredentialId,
            ["type"] = "public-key",
            ["response"] = new Dictionary<string, object?>
            {
                ["authenticatorData"] = WebEncoders.Base64UrlEncode(authenticatorData),
                ["clientDataJSON"] = WebEncoders.Base64UrlEncode(clientData),
                ["signature"] = WebEncoders.Base64UrlEncode(signature),
                ["userHandle"] = WebEncoders.Base64UrlEncode(userId.ToByteArray()),
            },
            ["clientExtensionResults"] = new Dictionary<string, object?>(),
        });
    }

    public void Dispose() => _key.Dispose();

    private byte[] RegistrationAuthenticatorData()
    {
        var parameters = _key.ExportParameters(false);
        var coseKey = Cbor.Map(
            Cbor.Integer(1), Cbor.Integer(2),
            Cbor.Integer(3), Cbor.Integer(-7),
            Cbor.Integer(-1), Cbor.Integer(1),
            Cbor.Integer(-2), Cbor.Bytes(parameters.Q.X!),
            Cbor.Integer(-3), Cbor.Bytes(parameters.Q.Y!));
        var data = new byte[32 + 1 + 4 + 16 + 2 + _credentialId.Length + coseKey.Length];
        var offset = WriteAuthenticatorHeader(data, flags: 0x45, signCount: 0);
        // AAGUID is deliberately all zeroes for this virtual authenticator.
        offset += 16;
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset, 2), checked((ushort)_credentialId.Length));
        offset += 2;
        _credentialId.CopyTo(data, offset);
        offset += _credentialId.Length;
        coseKey.CopyTo(data, offset);
        return data;
    }

    private byte[] AssertionAuthenticatorData()
    {
        var data = new byte[32 + 1 + 4];
        _ = WriteAuthenticatorHeader(data, flags: 0x05, _signCount);
        return data;
    }

    private static int WriteAuthenticatorHeader(byte[] destination, byte flags, uint signCount)
    {
        SHA256.HashData(Encoding.UTF8.GetBytes(RelyingPartyId)).CopyTo(destination, 0);
        destination[32] = flags;
        BinaryPrimitives.WriteUInt32BigEndian(destination.AsSpan(33, 4), signCount);
        return 37;
    }

    private static byte[] ClientData(string type, string challenge) =>
        JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object?>
        {
            ["type"] = type,
            ["challenge"] = challenge,
            ["origin"] = Origin,
            ["crossOrigin"] = false,
        });

    private static class Cbor
    {
        public static readonly byte[] EmptyMap = [0xA0];

        public static byte[] Map(params byte[][] values)
        {
            if (values.Length % 2 != 0 || values.Length / 2 > 23)
            {
                throw new ArgumentOutOfRangeException(nameof(values));
            }

            return Concat([(byte)(0xA0 | values.Length / 2)], values);
        }

        public static byte[] Integer(int value) =>
            value >= 0
                ? EncodeHead(0, checked((ulong)value))
                : EncodeHead(1, checked((ulong)(-1 - value)));

        public static byte[] Text(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            return Concat(EncodeHead(3, checked((ulong)bytes.Length)), [bytes]);
        }

        public static byte[] Bytes(byte[] value) =>
            Concat(EncodeHead(2, checked((ulong)value.Length)), [value]);

        private static byte[] EncodeHead(byte majorType, ulong value) => value switch
        {
            < 24 => [(byte)(((ulong)majorType << 5) | value)],
            <= byte.MaxValue => [(byte)((majorType << 5) | 24), (byte)value],
            <= ushort.MaxValue =>
            [
                (byte)((majorType << 5) | 25),
                (byte)(value >> 8),
                (byte)value,
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

        private static byte[] Concat(byte[] prefix, IReadOnlyList<byte[]> values)
        {
            var result = new byte[prefix.Length + values.Sum(value => value.Length)];
            prefix.CopyTo(result, 0);
            var offset = prefix.Length;
            foreach (var value in values)
            {
                value.CopyTo(result, offset);
                offset += value.Length;
            }

            return result;
        }
    }
}
