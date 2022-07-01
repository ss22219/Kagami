using Kagami.Function;
using Konata.Core;
using Konata.Core.Common;
using Konata.Core.Events.Model;
using Konata.Core.Interfaces;
using Konata.Core.Interfaces.Api;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Konata.Core.Message.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiHoYoAuth;
using MiHoYoAuth.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;
using Color = System.Drawing.Color;
using JsonSerializer = System.Text.Json.JsonSerializer;
using LogLevel = Konata.Core.Events.LogLevel;

namespace Kagami;

public static class Program
{
    private static Bot _bot;
    private static ILoggerFactory _loggerFactory;
    private static HttpClient _httpClient;
    private static MiAccount _miAccount;

    public static async Task Main()
    {
        _loggerFactory = LoggerFactory.Create(_ =>
            _.Services.AddLogging(option => option.AddConsole()));
        _miAccount = await GetMiAccount();
        _bot = BotFather.Create(GetConfig(),
            GetDevice(), GetKeyStore());
        _httpClient = new HttpClient();

        _bot.OnLog += (_, e) =>
        {
            if (e.Level < LogLevel.Information) return;
            var level = e.Level switch
            {
                LogLevel.Verbose => Microsoft.Extensions.Logging.LogLevel.Debug,
                LogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
                LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
                LogLevel.Exception => Microsoft.Extensions.Logging.LogLevel.Error,
                LogLevel.Fatal => Microsoft.Extensions.Logging.LogLevel.Critical,
                _ => Microsoft.Extensions.Logging.LogLevel.Trace
            };
            _loggerFactory.CreateLogger(e.Tag).Log(level, e.EventMessage);
        };

        // Handle the captcha
        _bot.OnCaptcha += (s, e) =>
        {
            switch (e.Type)
            {
                case CaptchaEvent.CaptchaType.Sms:
                    Console.WriteLine(e.Phone);
                    s.SubmitSmsCode(Console.ReadLine());
                    break;

                case CaptchaEvent.CaptchaType.Slider:
                    Console.WriteLine(e.SliderUrl);
                    s.SubmitSliderTicket(Console.ReadLine());
                    break;

                default:
                case CaptchaEvent.CaptchaType.Unknown:
                    break;
            }
        };

        // Handle poke messages
        _bot.OnGroupPoke += Poke.OnGroupPoke;

        // Handle messages from group
        _bot.OnGroupMessage += Command.OnGroupMessage;
        _bot.OnFriendMessage += async (sender, args) =>
        {
            try
            {
                var friends = await sender.GetFriendList();
                _loggerFactory.CreateLogger("OnFriendMessage")
                    .LogInformation(
                        $"{string.Join(",", args.Chain.Select(c => c.Type.ToString()))} from {friends.FirstOrDefault(f => f.Uin == args.FriendUin)?.Name}: {args.Chain.GetChain<TextChain>()?.Content}");

                var imageChain = args.Chain.GetChain<ImageChain>();
                if (imageChain == null) return;
                var bytes = await _httpClient.GetByteArrayAsync(imageChain.ImageUrl);
                using var image = Image.Load<Rgba32>(bytes);
                var barcodeReader = new ZXing.ImageSharp.BarcodeReader<Rgba32>();
                var result = barcodeReader.Decode(image);
                if (result.BarcodeFormat == BarcodeFormat.QR_CODE &&
                    result.Text.StartsWith("https://user.mihoyo.com/qr_code_in_game.html"))
                {
                    if(_miAccount == null) return;
                    await MiHoYoAPI.ScanQrCode(result.Text, _miAccount.DeviceId);
                    var gameToken = await MiHoYoAPI.GetGameToken(_miAccount.Uid, _miAccount.SToken);
                    await MiHoYoAPI.ConfirmQrCode(result.Text, _miAccount.Uid, gameToken, _miAccount.DeviceId);
                    await _bot.SendFriendMessage(args.FriendUin, "扫码成功");
                }
            }
            catch (Exception e)
            {
                if(e.Message == "ExpiredCode")
                    await _bot.SendFriendMessage(args.FriendUin, "二维码过期了");
                else if(e.Message == "InvalidStat")
                    await _bot.SendFriendMessage(args.FriendUin, "二维码过期了");
                else
                {
                    _loggerFactory.CreateLogger("OnFriendMessage").LogError(e.ToString());
                    await _bot.SendFriendMessage(args.FriendUin, e.Message);
                }

            }
        };

        // Login the bot
        var result = await _bot.Login();
        {
            // Update the keystore
            if (result) UpdateKeystore(_bot.KeyStore);
        }
        // cli
        while (true)
        {
            switch (Console.ReadLine())
            {
                case "/stop":
                    await _bot.Logout();
                    _bot.Dispose();
                    return;
                case "/switch":
                    _miAccount = null;
                    File.Delete("mihoyo_account.json");
                    await GetMiAccount();
                    return;
            }
        }
    }

    private static async Task<MiAccount> GetMiAccount()
    {
        // Read the device from config
        if (File.Exists("mihoyo_account.json"))
        {
            return JsonSerializer.Deserialize
                <MiAccount>(File.ReadAllText("mihoyo_account.json"));
        }

        var miAccount = new MiAccount { DeviceId = Guid.NewGuid().ToString() };
        Console.Write("Mihoyo Account: ");
        var account = Console.ReadLine();

        Console.Write("Password: ");
        var password = Console.ReadLine();

        Console.WriteLine("Open http://localhost:5123 to verify");
        var captchaData = await GT3.GetGeetest();
        var loginResult =
            await MiHoYoAPI.LoginByPassword(account, MiHoYoAPI.EncryptedPassword(password), captchaData);
        miAccount.Ticket = loginResult.Ticket;
        miAccount.Uid = loginResult.Uid;

        var multiTokenResult = await MiHoYoAPI.GetMultiTokenByLoginTicket(miAccount.Uid, miAccount.Ticket);
        miAccount.LToken = multiTokenResult.LToken;
        miAccount.SToken = multiTokenResult.SToken;
        File.WriteAllText("mihoyo_account.json",
            JsonSerializer.Serialize(miAccount, new JsonSerializerOptions { WriteIndented = true }));
        return miAccount;
    }


    /// <summary>
    /// Get bot config
    /// </summary>
    /// <returns></returns>
    private static BotConfig GetConfig()
    {
        return new BotConfig
        {
            EnableAudio = false,
            TryReconnect = true,
            HighwayChunkSize = 8192,
        };
    }

    /// <summary>
    /// Load or create device 
    /// </summary>
    /// <returns></returns>
    private static BotDevice GetDevice()
    {
        // Read the device from config
        if (File.Exists("device.json"))
        {
            return JsonSerializer.Deserialize
                <BotDevice>(File.ReadAllText("device.json"));
        }

        // Create new one
        var device = BotDevice.Default();
        {
            var deviceJson = JsonSerializer.Serialize(device,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("device.json", deviceJson);
        }

        return device;
    }

    /// <summary>
    /// Load or create keystore
    /// </summary>
    /// <returns></returns>
    private static BotKeyStore GetKeyStore()
    {
        // Read the device from config
        if (File.Exists("keystore.json"))
        {
            return JsonSerializer.Deserialize
                <BotKeyStore>(File.ReadAllText("keystore.json"));
        }

        Console.WriteLine("For first running, please " +
                          "type your account and password.");

        Console.Write("QQ Account: ");
        var account = Console.ReadLine();

        Console.Write("Password: ");
        var password = Console.ReadLine();

        // Create new one
        Console.WriteLine("Bot created.");
        return UpdateKeystore(new BotKeyStore(account, password));
    }

    /// <summary>
    /// Update keystore
    /// </summary>
    /// <param name="keystore"></param>
    /// <returns></returns>
    private static BotKeyStore UpdateKeystore(BotKeyStore keystore)
    {
        var deviceJson = JsonSerializer.Serialize(keystore,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText("keystore.json", deviceJson);
        return keystore;
    }

    private static readonly string[] AsciiChars = { "#", "#", "@", "%", "=", "+", "*", ":", "-", ".", " " };

    private static string ConvertToAscii(Image<Rgba32> image)
    {
        Boolean toggle = false;
        StringBuilder sb = new StringBuilder();
        for (int h = 0; h < image.Height; h++)
        {
            for (int w = 0; w < image.Width; w++)
            {
                var pixelColor = image[w, h];
                //Average out the RGB components to find the Gray Color
                int red = (pixelColor.R + pixelColor.G + pixelColor.B) / 3;
                int green = (pixelColor.R + pixelColor.G + pixelColor.B) / 3;
                int blue = (pixelColor.R + pixelColor.G + pixelColor.B) / 3;
                Color grayColor = Color.FromArgb(red, green, blue);
                //Use the toggle flag to minimize height-wise stretch
                if (!toggle)
                {
                    int index = (grayColor.R * 10) / 255;
                    sb.Append(AsciiChars[index]);
                }
            }

            if (!toggle)
            {
                sb.Append("\r\n");
                toggle = true;
            }
            else
            {
                toggle = false;
            }
        }

        return sb.ToString();
    }
}