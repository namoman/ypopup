using Ypopup.Core.Messaging;
using Xunit;

namespace Ypopup.Core.Tests.Messaging;

public class ReadReceiptTests
{
    [Fact]
    public void CountOthers_ExcludesSender()
    {
        var count = ReadReceipt.CountOthers(["sender", "a", "b"], "sender");

        Assert.Equal(2, count);
    }

    [Fact]
    public void ReadMessageIds_SkipsBlankLines()
    {
        var ids = ReadReceipt.ReadMessageIds("m1\n\nm2\n");

        Assert.Equal(["m1", "m2"], ids);
    }
}