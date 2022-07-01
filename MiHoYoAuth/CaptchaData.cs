namespace MiHoYoAuth
{
    public record CaptchaData(string mmtKey, string secCode, string validate, string challenge);
}