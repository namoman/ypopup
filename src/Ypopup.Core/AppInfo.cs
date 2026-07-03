namespace Ypopup.Core;

public static class AppInfo
{
    public const string ProductName = Models.AppConstants.ProductName;
    public const string Version = "1.2";
    public const string Author = "namoman";
    public const string Website = "https://namoman.github.io/ypopup/";
    public const string Email = "namolove@gmail.com";
    public const string ContactSummary = "문의·제안: namoman.github.io/ypopup · namolove@gmail.com";

    public static string VersionDisplay => $"버전 {Version}";

    public static string AboutText =>
        $"2000년대 X-Popup(빨간전화기) 오마주 한 Y-popup(파란전화기)입니다. ({VersionDisplay})\n\n" +
        "Win11·macOS·Linux에서 LAN 쪽지·파일·공유폴더를 사용할 수 있습니다.\n\n" +
        "같은 네트워크(같은 IP 대역)에 있는 PC끼리 쪽지와 파일을 주고받을 수 있습니다.\n\n" +
        "버전 번호는 프로그램 릴리스 표시이며, 포트 설정(UDP/TCP)이 같으면 " +
        "쪽지·파일·공유폴더는 버전이 달라도 함께 사용할 수 있습니다.\n\n" +
        "문의 및 제안: namolove@gmail.com / namoman.github.io/ypopup";
}
