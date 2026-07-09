namespace Ypopup.Core.Models;

public sealed record TransferProgress(
    long BytesTransferred,
    long TotalBytes,
    bool IsSending,
    string? FileName = null)
{
    public double Fraction => TotalBytes > 0 ? (double)BytesTransferred / TotalBytes : 0;

    public int Percent => TotalBytes > 0 ? (int)Math.Round(Fraction * 100) : 0;

    public bool IsComplete => TotalBytes > 0 && BytesTransferred >= TotalBytes;
}