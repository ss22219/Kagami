using System.Net;
using System.Text;
using Kagami;
using Microsoft.Extensions.FileProviders;
using MiHoYoAuth;
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
}