namespace App.Db;

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Yandex.Cloud.Generated;
using Yandex.Cloud.Iam.V1;
using Ydb.Sdk.Auth;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging.Abstractions;
using Ydb.Sdk.Yc;
using ICredentialsProvider = Yandex.Cloud.Credentials.ICredentialsProvider;

internal sealed class ServiceAccountKeyProvider : IamProviderBase
{
    private static readonly TimeSpan JwtTtl = TimeSpan.FromHours(1);
    private readonly ILogger logger;
    private readonly string serviceAccountId;
    private readonly RsaSecurityKey privateKey;

    public ServiceAccountKeyProvider(string saKeyEncoded, ILoggerFactory? loggerFactory = null)
        : base(loggerFactory)
    {
        loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        logger = loggerFactory.CreateLogger<ServiceAccountProvider>();

        var saKey = Base64Decode(saKeyEncoded);
        var saInfo = JsonSerializer.Deserialize<SaJsonInfo>(saKey);
        if (saInfo == null)
        {
            throw new InvalidCredentialsException("Failed to parse service account file.");
        }

        saInfo.EnsureValid();

        logger.LogDebug("Successfully parsed service account file.");

        serviceAccountId = saInfo.service_account_id;

        using (var reader = new StringReader(saInfo.private_key))
        {
            var parameters = new PemReader(reader).ReadObject() as RsaPrivateCrtKeyParameters;
            if (parameters == null)
            {
                throw new InvalidCredentialsException("Failed to parse service account key.");
            }

            RSAParameters rsaParams = DotNetUtilities.ToRSAParameters(parameters);
            privateKey = new RsaSecurityKey(rsaParams) { KeyId = saInfo.id };
        }

        logger.LogInformation("Successfully parsed service account key.");
    }

    protected override async Task<IamTokenData> FetchToken()
    {
        logger.LogInformation("Fetching IAM token by service account key.");

        var services = new Services(new Yandex.Cloud.Sdk(new EmptyYcCredentialsProvider()));

        var request = new CreateIamTokenRequest
        {
            Jwt = MakeJwt()
        };

        var response = await services.Iam.IamTokenService.CreateAsync(request);

        var iamToken = new IamTokenData(
            token: response.IamToken,
            expiresAt: response.ExpiresAt.ToDateTime()
        );

        return iamToken;
    }

    private string MakeJwt()
    {
        var handler = new JsonWebTokenHandler();
        var now = DateTime.UtcNow;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = serviceAccountId,
            Audience = "https://iam.api.cloud.yandex.net/iam/v1/tokens",
            IssuedAt = now,
            Expires = now.Add(JwtTtl),
            SigningCredentials = new SigningCredentials(privateKey, SecurityAlgorithms.RsaSsaPssSha256),
        };

        return handler.CreateToken(descriptor);
    }

    private class SaJsonInfo
    {
        public string id { get; set; } = "";
        public string service_account_id { get; set; } = "";
        public string private_key { get; set; } = "";

        public void EnsureValid()
        {
            if (string.IsNullOrEmpty(id) ||
                string.IsNullOrEmpty(service_account_id) ||
                string.IsNullOrEmpty(private_key))
            {
                throw new InvalidCredentialsException("Invalid service account file.");
            }
        }
    }
    
    private sealed class EmptyYcCredentialsProvider : ICredentialsProvider
    {
        public string GetToken() => "";
    }
    
    public static string Base64Decode(string base64EncodedData) 
    {
        var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
        return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
    }
}