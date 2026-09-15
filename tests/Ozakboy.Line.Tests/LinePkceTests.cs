namespace Ozakboy.Line.Tests;

/// <summary>
/// PKCE 產生器的測試。
/// Tests for the PKCE generator.
/// </summary>
[TestClass]
public sealed class LinePkceTests
{
    [TestMethod]
    public void ComputeCodeChallenge_Rfc7636AppendixB_MatchesKnownVector()
    {
        // RFC 7636 附錄 B 的已知向量。挑已知向量而不是「自己算一次再比自己」,
        // 是因為後者只證明程式前後一致,證明不了它算的是規格要求的那個東西。
        const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        const string ExpectedChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        Assert.AreEqual(ExpectedChallenge, LinePkce.ComputeCodeChallenge(Verifier));
    }

    [TestMethod]
    public void ComputeCodeChallenge_HasNoBase64Padding()
    {
        var challenge = LinePkce.ComputeCodeChallenge(LinePkce.CreateCodeVerifier());

        Assert.IsFalse(challenge.Contains('=', StringComparison.Ordinal), "base64url 不帶補位字元。");
        Assert.IsFalse(challenge.Contains('+', StringComparison.Ordinal), "base64url 以 - 取代 +。");
        Assert.IsFalse(challenge.Contains('/', StringComparison.Ordinal), "base64url 以 _ 取代 /。");
    }

    [TestMethod]
    public void CreateCodeVerifier_UsesOnlyUnreservedCharacters()
    {
        var verifier = LinePkce.CreateCodeVerifier(128);

        Assert.AreEqual(128, verifier.Length);
        foreach (var character in verifier)
        {
            Assert.IsTrue(
                char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_' or '~',
                $"字元 '{character}' 不在 RFC 7636 允許的 unreserved 字元集內。");
        }
    }

    [TestMethod]
    public void CreateCodeVerifier_TwoCallsDiffer()
    {
        // verifier 可預測時 PKCE 就完全失效,因此亂數來源必須是密碼學等級的。
        Assert.AreNotEqual(LinePkce.CreateCodeVerifier(), LinePkce.CreateCodeVerifier());
    }

    [TestMethod]
    public void CreateCodeVerifier_LengthOutOfRange_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => LinePkce.CreateCodeVerifier(42));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => LinePkce.CreateCodeVerifier(129));
    }
}
