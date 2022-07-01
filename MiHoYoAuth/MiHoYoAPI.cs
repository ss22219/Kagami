using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MiHoYoAuth
{
    public static class MiHoYoAPI
    {
        private const string SOURCE = "webstatic.mihoyo.com";
        private const string BBSAPI = "https://bbs-api.mihoyo.com";
        private const string SDKAPI = "https://api-sdk.mihoyo.com";
        private const string HK4API = "https://hk4e-api.mihoyo.com";
        private const string WEBAPI = "https://webapi.account.mihoyo.com/Api";
        private const string RECAPI = "https://api-takumi-record.mihoyo.com/game_record/app";
        private const string TAKUMI_AUTH_API = "https://api-takumi.mihoyo.com/auth/api";
        private const string TAKUMI_BINDING_API = "https://api-takumi.mihoyo.com/binding/api";
        private static readonly Lazy<HttpClient?> DefaultHttpClient = new();

        public static Task<JToken> CreateMMT() =>
            GetJson(
                url: $"{WEBAPI}/create_mmt",
                postBody: BuildFormBody.Add("mmt_type", "1").Add("scene_type", "1").AddTimestamp("now").ToHttpContent()
            ).GetJsonData().CheckStatus().GetAsJsonObject("mmt_data");


        public static Task<JToken> LoginByMobileCaptcha(
            string mobile,
            string code,
            HttpClient? httpClient = null
        ) =>
            GetJson(
                url: $"{WEBAPI}/login_by_mobilecaptcha",
                postBody: BuildFormBody.Add("mobile", mobile)
                    .Add("mobile_captcha", code)
                    .Add("source", SOURCE)
                    .AddTimestamp()
                    .ToHttpContent()
            ).GetJsonData().CheckStatus();


        public static Task<JToken> CreateMobileCaptcha(
            string phoneNumber,
            CaptchaData cData,
            string type = "login",
            HttpClient? httpClient = null
        ) =>
            GetJson(
                url: $"{WEBAPI}/create_mobile_captcha",
                postBody: BuildFormBody.Add("action_type", type).Add("mobile", phoneNumber).AddTimestamp()
                    .AddCaptchaData(cData).ToHttpContent()
            ).GetJsonData().CheckStatus();

        public static Task<JToken> LoginByPassword(
            string account,
            string encryptedPassword,
            CaptchaData cData,
            HttpClient? httpClient = null) =>
            GetJson(
                url: $"{WEBAPI}/login_by_password",
                postBody: BuildFormBody.Add("source", SOURCE).Add("account", account)
                    .Add("password", encryptedPassword).Add("is_crypto", "true").AddTimestamp().AddCaptchaData(cData)
                    .ToHttpContent(),
                httpClient: httpClient
            ).GetJsonData().CheckStatus();


        public static Task<JToken> ScanQrCode(string codeUrl, string deviceId, HttpClient? httpClient = null)
        {
            var urlParams = ParseQrCodeUrl(codeUrl);
            return GetJson(
                url: $"{SDKAPI}/{urlParams["biz_key"]}/combo/panda/qrcode/scan",
                postBody: JsonBodyOf(new
                {
                    app_id = urlParams["app_id"],
                    ticket = urlParams["ticket"],
                    device = deviceId
                }),
                httpClient: httpClient
            ).CheckRetCode();
        }

        public static Task<Dictionary<string, string>> GetMultiTokenByLoginTicket(string uid, string ticket) =>
            GetJson(
                    $"{TAKUMI_AUTH_API}/getMultiTokenByLoginTicket?login_ticket={ticket}&token_types=3&uid={uid}")
                .CheckRetCode()
                .GetAsJsonArray("list")
                .ContinueWith(t =>
                    t.Result.ToDictionary(o => o["name"]!.ToString(), o => o["token"]!.ToString()));

        public static string EncryptedPassword(string password)
        {
            using var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(PublicKey, out var _);
            return Convert.ToBase64String(rsa.Encrypt(Encoding.UTF8.GetBytes(password), RSAEncryptionPadding.Pkcs1));
        }
        
        public static CaptchaData ParseCaptchaData(string mmtKey, GeeTestResult result)
        {
            return new CaptchaData(
                mmtKey,
                result.geetest_seccode,
                result.geetest_validate,
                result.geetest_challenge
            );
        }

        private static Task<JToken> GetJson(string url, HttpContent postBody,
            HttpClient? httpClient = null)
        {
            httpClient = httpClient ?? DefaultHttpClient.Value;
            return ToJsonObject(httpClient!.PostAsync(url, postBody));
        }

        private static Task<JToken> GetJson(string url,
            HttpClient? httpClient = null)
        {
            httpClient = httpClient ?? DefaultHttpClient.Value;
            return ToJsonObject(httpClient!.GetAsync(url));
        }

        private static Task<JToken> CheckStatus(this Task<JToken> jTask) =>
            Check(jTask, "status", "msg", 1);

        private static Task<JArray> GetAsJsonArray(this Task<JToken> jTask, string memberName) =>
            jTask.ContinueWith(t => (t.Result[memberName] as JArray)!);

        private static Task<JToken> CheckRetCode(this Task<JToken> jTask) =>
            jTask.Check("retcode", "message", 0);

        private static Task<JToken> Check(this Task<JToken> task, string codeProperty, string msgProperty, int code)
        {
            return task.ContinueWith(t =>
            {
                if (t.Result[codeProperty]!.Value<int>() != code)
                    throw new Exception(t.Result[msgProperty]!.Value<string>());
                return t.Result;
            });
        }

        private static Task<JToken> GetJsonData(this Task<JToken> jTask) =>
            jTask.GetAsJsonObject("data")!;

        private static Task<JToken> ToJsonObject(this Task<HttpResponseMessage> responseMessage) =>
            responseMessage.ContinueWith(t =>
                t.Result.Content.ReadAsStringAsync().ContinueWith(c =>
                    JsonConvert.DeserializeObject<JToken>(c.Result)!)).Unwrap();

        private static Task<JToken> GetAsJsonObject(this Task<JToken> jTask, string memberName) =>
            jTask.ContinueWith(t =>
                t.Result[memberName]!);

        private static Dictionary<string, string> ParseQrCodeUrl(string codeUrl)
        {
            var uri = new Uri(codeUrl);
            var query = QueryHelpers.ParseQuery(uri.Query);
            return query.ToDictionary(kv => kv.Key, kv => kv.Value[0]);
        }

        private static HttpContent JsonBodyOf(object o)
        {
            return new StringContent(JsonConvert.SerializeObject(o), Encoding.UTF8, "application/json");
        }

        private static readonly byte[] PublicKey =
            Convert.FromBase64String(
                "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDDvekdPMHN3AYhm/vktJT+YJr7cI5DcsNKqdsx5DZX0gDuWFuIjzdwButrIYPNmRJ1G8ybDIF7oDW2eEpm5sMbL9zs\n9ExXCdvqrn51qELbqj0XxtMTIpaCHFSI50PfPpTFV9Xt/hmyVwokoOXFlAEgCn+Q\nCgGs52bFoYMtyi+xEQIDAQAB\n");

    }
}