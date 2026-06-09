using GSYNC.Core.Utilities;
using Xunit;

namespace GSYNC.Core.Tests;

public sealed class SystemVariableProviderTests
{
    [Fact]
    public void GetVariables_ReturnsBuiltInKeys()
    {
        var provider = new SystemVariableProvider();

        var variables = provider.GetVariables();

        Assert.Contains("HOME", variables.Keys);
        Assert.Contains("APPDATA", variables.Keys);
        Assert.Contains("LOCALAPPDATA", variables.Keys);
        Assert.Contains("DOCUMENTS", variables.Keys);
    }
}
