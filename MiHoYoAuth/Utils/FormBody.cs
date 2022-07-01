using System;
using System.Collections.Generic;
using System.Net.Http;
using MiHoYoAuth.Dtos;

namespace MiHoYoAuth.Utils
{
    public class FormBody
    {
        private readonly List<KeyValuePair<string, string>> _parameters = new();

        public FormBody(List<KeyValuePair<string, string>> keyValuePairs)
        {
            _parameters.AddRange(keyValuePairs);
        }

        public FormBody Add(string key, string value)
        {
            _parameters.Add(new KeyValuePair<string, string>(key, value));
            return this;
        }

        public FormBody AddTimestamp(string t = "t")
        {
            Add(t, DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
            return this;
        }

        public FormBody AddCaptchaData(CaptchaData data)
        {
            Add("mmt_key", data.mmtKey);
            Add("geetest_seccode", data.secCode);
            Add("geetest_validate", data.validate);
            Add("geetest_challenge", data.challenge);
            return this;
        }
        
        public HttpContent ToHttpContent()
        {
            return new FormUrlEncodedContent(_parameters);
        }
    }
}