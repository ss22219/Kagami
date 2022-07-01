using MiHoYoAuth;
using MiHoYoAuth.Dtos;
using MiHoYoAuth.Utils;
using Newtonsoft.Json;

namespace TestProject1;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        Console.WriteLine(MiHoYoAPI.CreateMMT().Result.ToString(formatting: Formatting.Indented));
        Assert.Pass();
    }

    [Test]
    public async Task Test2()
    {
        Console.WriteLine("open http://localhost:5123 to verify");
        var captchaData = await GT3.GetGeetest();
        Assert.Pass();
    }


    [Test]
    public async Task Test3()
    {
        Console.WriteLine("open http://localhost:5123 to verify");
        var captchaData = await GT3.GetGeetest();
        var result =
            await MiHoYoAPI.LoginByPassword("", MiHoYoAPI.EncryptedPassword(""), captchaData);
        Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
        Assert.Pass();
    }

    [Test]
    public async Task Test4()
    {
        var accountId = "";
        var webLoginToken = "";
        var result = await MiHoYoAPI.GetMultiTokenByLoginTicket(accountId, webLoginToken);
        Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
        Assert.Pass();
    }


    [Test]
    public async Task Test5()
    {
        var accountId = "";
        var tokens = new MultiTokenResult
        {
            SToken = "",
            LToken = ""
        };
        var deviceId = MiHoYoAPI.GetDeviceId("just test machine name");
        var url =
            "https://user.mihoyo.com/qr_code_in_game.html?app_id=4&app_name=%E5%8E%9F%E7%A5%9E&bbs=true&biz_key=hk4e_cn&expire=1656497198&ticket=62b58cae01bd4b2d3db2eb27";
        await MiHoYoAPI.ScanQrCode(url, deviceId);
        var gameToken = await MiHoYoAPI.GetGameToken(accountId, tokens.SToken);
        var result = await MiHoYoAPI.ConfirmQrCode(url, accountId, gameToken, deviceId);
        Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
        Assert.Pass();
    }
}