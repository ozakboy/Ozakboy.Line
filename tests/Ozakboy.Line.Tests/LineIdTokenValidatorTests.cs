using Microsoft.Extensions.Time.Testing;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests;

/// <summary>
/// id_token 本地驗證的測試。
/// Tests for local id_token validation.
/// </summary>
[TestClass]
public sealed class LineIdTokenValidatorTests
{
    [TestMethod]
    public void Validate_ValidToken_Succeeds()
    {
        var token = TestIdToken.Create(email: "user@example.com", nonce: "nonce-1");

        var result = LineIdTokenValidator.Validate(token, TestIdToken.ChannelId, TestIdToken.ChannelSecret, "nonce-1");

        Assert.IsTrue(result.IsSuccess, "以正確的 channel id、secret 與 nonce 驗證應該通過。");
        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.AreEqual(LineEndpoints.IdTokenIssuer, payload.Issuer);
        Assert.AreEqual(TestIdToken.ChannelId, payload.Audience);
        Assert.AreEqual("U1234567890abcdef", payload.Subject);
        Assert.AreEqual("user@example.com", payload.Email);
        Assert.AreEqual("nonce-1", payload.Nonce);
        var expectedAmr = new List<string> { "pwd" };
        CollectionAssert.AreEqual(expectedAmr, payload.Amr.ToList());
    }

    [TestMethod]
    public void Validate_TamperedSignature_FailsWithInvalidSignature()
    {
        var token = TestIdToken.WithBrokenSignature(TestIdToken.Create());

        var result = LineIdTokenValidator.Validate(token, TestIdToken.ChannelId, TestIdToken.ChannelSecret);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.IdTokenInvalidSignature, result.Error.Code);
    }

    [TestMethod]
    public void Validate_WrongSecret_FailsWithInvalidSignature()
    {
        var token = TestIdToken.Create(channelSecret: "another-channel-secret");

        var result = LineIdTokenValidator.Validate(token, TestIdToken.ChannelId, TestIdToken.ChannelSecret);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.IdTokenInvalidSignature, result.Error.Code);
    }

    [TestMethod]
    public void Validate_WrongAudience_FailsWithInvalidAudience()
    {
        var token = TestIdToken.Create(audience: "some-other-channel");

        var result = LineIdTokenValidator.Validate(token, TestIdToken.ChannelId, TestIdToken.ChannelSecret);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.IdTokenInvalidAudience, result.Error.Code);
    }

    [TestMethod]
    public void Validate_WrongIssuer_FailsWithInvalidIssuer()
    {
        var token = TestIdToken.Create(issuer: "https://example.com");

        var result = LineIdTokenValidator.Validate(token, TestIdToken.ChannelId, TestIdToken.ChannelSecret);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.IdTokenInvalidIssuer, result.Error.Code);
    }

    [TestMethod]
    public void Validate_Expired_FailsWithExpired()
    {
        var token = TestIdToken.Create(expiresAt: DateTimeOffset.UtcNow.AddHours(-2));

        var result = LineIdTokenValidator.Validate(token, TestIdToken.ChannelId, TestIdToken.ChannelSecret);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.IdTokenExpired, result.Error.Code);
    }

    [TestMethod]
    public void Validate_JustExpiredWithinClockSkew_Succeeds()
    {
        // 剛過期一分鐘,在預設五分鐘的容許誤差內:兩台機器的時鐘差幾十秒是常態,
        // 不容許誤差會讓登入偶發失敗,而且完全無法重現。
        var token = TestIdToken.Create(expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = LineIdTokenValidator.Validate(token, TestIdToken.ChannelId, TestIdToken.ChannelSecret);

        Assert.IsTrue(result.IsSuccess, "在允許的時鐘誤差內剛過期的 token 仍應通過。");
    }

    [TestMethod]
    public void Validate_ExpiredAgainstFakeClock_FailsWithExpired()
    {
        var expiry = DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var token = TestIdToken.Create(expiresAt: expiry);
        var clock = new FakeTimeProvider(expiry.AddMinutes(10));

        var result = LineIdTokenValidator.Validate(
            token,
            TestIdToken.ChannelId,
            TestIdToken.ChannelSecret,
            timeProvider: clock);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.IdTokenExpired, result.Error.Code);
    }

    [TestMethod]
    public void Validate_NonceMismatch_FailsWithNonceMismatch()
    {
        var token = TestIdToken.Create(nonce: "nonce-from-line");

        var result = LineIdTokenValidator.Validate(token, TestIdToken.ChannelId, TestIdToken.ChannelSecret, "nonce-we-sent");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.IdTokenNonceMismatch, result.Error.Code);
    }

    [TestMethod]
    public void Validate_UnsupportedAlgorithm_FailsWithUnsupportedAlgorithm()
    {
        var token = TestIdToken.Create(algorithm: "ES256");

        var result = LineIdTokenValidator.Validate(token, TestIdToken.ChannelId, TestIdToken.ChannelSecret);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.IdTokenUnsupportedAlgorithm, result.Error.Code);
        StringAssert.Contains(result.Error.Message, "VerifyIdTokenAsync", "訊息應指出改用遠端驗證這條路。");
    }

    [TestMethod]
    public void Validate_NotThreeParts_FailsWithInvalidFormat()
    {
        var result = LineIdTokenValidator.Validate("not.a-jwt", TestIdToken.ChannelId, TestIdToken.ChannelSecret);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.IdTokenInvalidFormat, result.Error.Code);
    }

    [TestMethod]
    public void Validate_NotBase64Url_FailsWithInvalidFormat()
    {
        var result = LineIdTokenValidator.Validate("!!!.???.###", TestIdToken.ChannelId, TestIdToken.ChannelSecret);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.IdTokenInvalidFormat, result.Error.Code);
    }

    [TestMethod]
    public void Validate_BlankToken_Throws()
    {
        // 空白的 id_token 是呼叫端的程式缺陷,不是預期之內的失敗,因此擲出而不是回 Result。
        Assert.ThrowsExactly<ArgumentException>(() =>
            LineIdTokenValidator.Validate("   ", TestIdToken.ChannelId, TestIdToken.ChannelSecret));
    }
}
