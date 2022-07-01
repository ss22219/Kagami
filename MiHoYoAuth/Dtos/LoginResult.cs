using Newtonsoft.Json;

namespace MiHoYoAuth.Dtos
{
    public class LoginResult
    {
        [JsonProperty("account_id")]
        public string Uid { get; set; }
        [JsonProperty("weblogin_token")]
        public string Ticket { get; set; }
    }
}