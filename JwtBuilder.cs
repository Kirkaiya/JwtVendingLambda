using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace JwtVendingLambda
{
    public class JwtBuilder : IDisposable
    {
        private RSA _rsa;
        private readonly byte[] _privateKey;
        private readonly byte[] _publicKey;
        private readonly string _aud;
        private readonly string _kid;
        private readonly string _iss;
        private bool disposedValue;

        public JwtBuilder(byte[] privateKey, byte[] publicKey, string aud, string kid, string iss)
        {
            _privateKey = privateKey;
            _publicKey = publicKey;
            _aud = aud;
            _kid = kid;
            _iss = iss;

            _rsa = RSA.Create();
        }

        public string GetToken(JwtRequest request, JwtType type)
        {
            _rsa.ImportRSAPrivateKey(_privateKey, out _);

            var jwtHandler = new JwtSecurityTokenHandler();
            var claims = new List<Claim>();

            switch (type) {
                case JwtType.IdToken:
                    claims.Add(new Claim("email", request.email ?? ""));
                    claims.Add(new Claim("phone_number", request.phoneNumber ?? ""));
                    break;

                case JwtType.AccessToken:
                    claims.Add(new Claim("client_id", _aud));
                    claims.Add(new Claim("scope", request.scope));
                    break;
            }

            claims.Add(new Claim("sub", request.subject));
            claims.Add(new Claim("username", request.username));
            foreach (var rclaim in request.claims)
                claims.Add(new Claim(rclaim.Key, rclaim.Value));

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _iss,
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(12),
                SigningCredentials = new SigningCredentials(new RsaSecurityKey(_rsa) { KeyId = _kid }, SecurityAlgorithms.RsaSha256)
            };

            if (type == JwtType.IdToken) descriptor.Audience = _aud;

            var jwtToken = jwtHandler.CreateToken(descriptor);
            var b64token = jwtHandler.WriteToken(jwtToken);

            return b64token;
        }

        public Jwks GetJwks()
        {
            _rsa.ImportRSAPublicKey(_publicKey, out _);

            RsaSecurityKey key = new RsaSecurityKey(_rsa);

            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
            jwk.Alg = "RSA256";
            jwk.Kid = _kid;
            jwk.Use = "sig";
            var jwks = new Jwks
            {
                keys = new[] {
                    new { alg = jwk.Alg, e = jwk.E, kid = jwk.Kid, kty = jwk.Kty, n = jwk.N, use = jwk.Use }
                }
            };

            return jwks;

            //var jwksJson = JsonSerializer.Serialize(Jwks);

            //return jwksJson;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                    _rsa.Dispose();

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
