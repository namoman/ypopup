namespace Ypopup.App;

public static class AppInfo
{
    public const string ProductName = Core.Models.AppConstants.ProductName;
    public const string Version = "1.1";
    public const string Author = "namoman";
    public const string Website = "https://namoman.com";
    public const string Email = "namolove@gmail.com";
    public const string ContactSummary = "문의·제안: namoman.com · namolove@gmail.com";

    public static string VersionDisplay => $"버전 {Version}";

    public static string AboutText =>
        $"2000년대 X-Popup(빨간전화기) 오마주 한 Y-popup(파란전화기)입니다. ({VersionDisplay})\n\n" +
        "Win11에 대응하여 최신 통신기술을 적용하여 호환성을 높였습니다.\n\n" +
        "같은 네트워크(같은 IP 대역)에 있는 PC끼리 쪽지와 파일을 주고받을 수 있습니다.\n\n" +
        "버전 번호는 프로그램 릴리스 표시이며, 포트 설정(UDP/TCP)이 같으면 " +
        "쪽지·파일·공유폴더는 버전이 달라도 함께 사용할 수 있습니다.\n\n" +
        "문의 및 제안: namolove@gmail.com / namoman.com";
}
