using System;
using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.Lambda.Core;
using System.Collections.Generic;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace JwtVendingLambda
{
    public class Function
    {

        private static JwtBuilder builder;

        public Function()
        {
            if (builder != null) return;

            //read aud, kid and iss from env vars
            var aud = Environment.GetEnvironmentVariable("Audience");
            var iss = Environment.GetEnvironmentVariable("Issuer");
            var kid = Environment.GetEnvironmentVariable("KeyId");

            var client = new AmazonSecretsManagerClient();
            GetSecretValueResponse response;

            try
            {
                response = client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = "RsaKeypair" }).Result;
            }
            catch (Exception ex)
            {
                LambdaLogger.Log("Error trying to fetch RSA keys from SM: " + ex.Message);
                throw;
            }

            var json = JsonDocument.Parse(response.SecretString);
            var privKey = Convert.FromBase64String(json.RootElement.GetProperty("privateKey").GetString());
            var pubKey = Convert.FromBase64String(json.RootElement.GetProperty("publicKey").GetString());

            builder = new JwtBuilder(privKey, pubKey, aud, kid, iss);
        }

        /// <summary>
        /// Function that takes in a JwtRequest that specifies either requestType = 0 (to generate tokens) or 1 (to generate the JWKS json)
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns>JwtResponse</returns>
        public JwtResponse FunctionHandler(JwtRequest request, ILambdaContext context)
        {
            if (request.requestType == RequestType.Jwks)
                return new JwtResponse { Jwks = builder.GetJwks() };

            if (request.claims == null)
                request.claims = Array.Empty<KeyValuePair<string, string>>();

            return new JwtResponse
            {
                IdToken = builder.GetToken(request, JwtType.IdToken),
                AccessToken = builder.GetToken(request, JwtType.AccessToken)
            };
        }
    }
}
