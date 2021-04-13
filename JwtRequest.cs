using System.Collections.Generic;

namespace JwtVendingLambda
{
    public enum RequestType
    {
        JwtTokens,
        Jwks
    }

    public class JwtRequest
    {
        public RequestType requestType { get; set; }

        public string subject { get; set; }

        public string username { get; set; }

        public string email { get; set; }

        public string phoneNumber { get; set; }

        public string scope { get; set; }

        public IEnumerable<KeyValuePair<string, string>> claims { get; set; }
    }
}
