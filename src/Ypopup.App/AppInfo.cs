namespace Ypopup.App;

public static class AppInfo
{
    public const string ProductName = Core.Models.AppConstants.ProductName;
    public const string Author = "namoman";
    public const string Website = "https://namoman.com";
    public const string Email = "namolove@gmail.com";
    public const string ContactSummary = "문의·제안: namoman.com · namolove@gmail.com";

    public static string AboutText =>
        $"{ProductName}\n\n" +
        $"제작: {Author}\n" +
        $"웹사이트: {Website}\n" +
        $"이메일: {Email}\n\n" +
        "X-Popup(빨간전화기) 오마주 LAN 메신저";
}
