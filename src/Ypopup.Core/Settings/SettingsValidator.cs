namespace Ypopup.Core.Settings;

public readonly struct ValidationResult
{
    public bool IsValid { get; }
    public string ErrorMessage { get; }

    private ValidationResult(bool isValid, string errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static ValidationResult Success => new(true, string.Empty);
    public static ValidationResult Fail(string message) => new(false, message);
}

public static class SettingsValidator
{
    public const int MinPort = 1024;
    public const int MaxPort = 65535;

    public static ValidationResult ValidateDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return ValidationResult.Fail("표시 이름을 입력하세요.");
        }

        return ValidationResult.Success;
    }

    public static ValidationResult ValidatePort(string? portText, string portLabel)
    {
        if (!int.TryParse(portText, out var port) || port < MinPort || port > MaxPort)
        {
            return ValidationResult.Fail($"{portLabel} 포트는 {MinPort}~{MaxPort} 사이여야 합니다.");
        }

        return ValidationResult.Success;
    }

    public static ValidationResult ValidatePortsDiffer(int port1, int port2, string label1, string label2)
    {
        if (port1 == port2)
        {
            return ValidationResult.Fail($"{label1} 포트와 {label2} 포트는 다른 번호여야 합니다.");
        }

        return ValidationResult.Success;
    }

    public static ValidationResult ValidateShareFolderPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Fail("공유폴더 경로를 입력하세요.");
        }

        return ValidationResult.Success;
    }

    public static ValidationResult ValidateAwayIdleMinutes(string? minutesText)
    {
        if (!int.TryParse(minutesText, out var minutes) || minutes < 1)
        {
            return ValidationResult.Fail("부재 유휴 시간은 1분 이상이어야 합니다.");
        }

        return ValidationResult.Success;
    }
}
