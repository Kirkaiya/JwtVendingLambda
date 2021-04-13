using System;
using System.Text.Json;

namespace JwtVendingLambda
{
    [Serializable]
    public class JwtResponse
    {
        public string IdToken { get; set; }

        public string AccessToken { get; set; }

        public Jwks Jwks { get; set; }
    }
}
