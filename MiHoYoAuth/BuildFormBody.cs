using System.Collections.Generic;

namespace MiHoYoAuth
{
    public static class BuildFormBody
    {
        public static FormBody Add(string key, string value)
        {
            return new FormBody(new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>(key, value) });
        }
    }
}