using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace ConsoleApp1
{
    [DataContract]
    public class Tokens
    {
        [JsonConstructor]
        public Tokens(
            string access_token,
            int expires_at,
            string refresh_token)
        {
            AccessToken = access_token;
            ExpiresAt = expires_at;
            RefreshToken = refresh_token;
        }

        [DataMember(Name = "access_token")]
        public string AccessToken { get; set; }

        [DataMember(Name = "expires_at")]
        public int ExpiresAt { get; set; }

        [DataMember(Name = "refresh_token")]
        public string RefreshToken { get; set; }

        public DateTime GetExpiresAt()
        {
            return FromUnixTime(ExpiresAt);
        }

        public static DateTime FromUnixTime(long unixTime)
        {
            return epoch.AddSeconds(unixTime);
        }

        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}