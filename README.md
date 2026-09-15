# Ozakboy.Line

LINE Login, the Messaging API, and webhooks for .NET — with one line of setup for ASP.NET Core.

English | [繁體中文](README_zh-TW.md)

```
dotnet add package Ozakboy.Line
dotnet add package Ozakboy.Line.AspNetCore
```

Requires .NET 10. Depends only on Microsoft packages and `Ozakboy.*`.

---

## Why this exists

Putting LINE into an ASP.NET Core site means three separate jobs: signing people in with LINE Login, pushing messages from an official account, and receiving webhooks. They live in different parts of LINE's documentation, they use **two different channels** with two different sets of credentials, and each has a handful of details that fail quietly when you get them wrong — a redirect URI that does not match to the character, an `id_token` nobody verifies, a webhook body that was parsed before its signature was checked.

This package does all three, and the ASP.NET Core integration turns the first one into `AddLineLogin()`.

It was extracted from a kindergarten review platform running in production at [youmingdeng.ozakboy.life](https://youmingdeng.ozakboy.life): LINE sign-in, add-friend prompting, push notifications and rich menus all run there.

---

## How it behaves

**Expected failures come back as `Result<T>`, not exceptions.** A user declining consent, an expired authorization code, a spent push quota, someone who blocked the official account — these are a messaging system's normal days. They arrive as a `Result` carrying a stable `line.*` code and a category, so forgetting to handle one is a compile-time shape rather than a runtime surprise. Genuine defects — a null argument, a blank token — still throw.

**LINE's own error body is folded into the error.** LINE answers a bad request with `{"message": "...", "details": [{"message": "...", "property": "..."}]}`, and `message` is frequently no more than `The request body has 1 error(s)`. The `details` array is the part that names the offending field, so it is parsed out into the error data under `lineMessage` and `lineDetails`, while the HTTP category is left alone: a 404 stays `NotFound` and a 429 stays `RateLimited`.

**HTTP goes through [Ozakboy.Http](https://github.com/ozakboy/Ozakboy.Http).** Retries, timeouts and log masking belong to that pipeline. Channel secrets and access tokens are registered with its masker at startup, so they do not appear in logs or in error messages.

**A push is retried only when you give it a retry key.** A push is a POST, and resending one sends a second message — the user's phone buzzes twice, and the "failure" was quite possibly a lost response to a message that did go out. Supply `RetryKey` and the request carries `X-Line-Retry-Key`, which LINE deduplicates on; only then does the pipeline retry it. Without a key, a push goes out exactly once.

**`id_token` is verified locally, with no JWT library.** LINE signs id_tokens with HS256 keyed by the channel secret — verifier and signer hold the same secret, so there is no public key to fetch and no JWKS cache. `HMACSHA256` and `Base64Url` from the BCL cover it, and the signature is compared with `CryptographicOperations.FixedTimeEquals`. Signature, issuer, audience, expiry and nonce are each checked, and each failure has its own error code.

**Webhook signatures are verified over the raw bytes.** The body is read as it arrived, never parsed and re-serialised first — field order, whitespace and escaping all change under a round trip, and the signature then never matches again. The comparison is fixed-time.

---

## Quick start

### 1. Sign in with LINE (ASP.NET Core)

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie()
    .AddLineLogin(options =>
    {
        options.ClientId = builder.Configuration["Line:Login:ChannelId"]!;
        options.ClientSecret = builder.Configuration["Line:Login:ChannelSecret"]!;
        options.BotPrompt = LineBotPrompt.Aggressive;   // invite the user to add the official account
    });
```

```csharp
app.MapGet("/me", (ClaimsPrincipal user) => new
{
    UserId = user.FindFirstValue(LineClaimTypes.UserId),
    Name = user.FindFirstValue(ClaimTypes.Name),
    Picture = user.FindFirstValue(LineClaimTypes.Picture),
    IsFriend = user.FindFirstValue(LineClaimTypes.IsFriend),   // absent when it could not be read
}).RequireAuthorization();
```

PKCE is on by default. A nonce is generated per sign-in, carried through the OAuth state, and checked against the `id_token`; a token that does not verify fails the whole sign-in rather than signing someone in anyway. Register the callback path — `/signin-line` by default — in the LINE Developers console, character for character.

### 2. The client on its own

When you are not using the authentication handler — a LIFF app, a background job, a non-web host — the core client does the same flow by hand.

```csharp
builder.Services.AddLineLogin(options =>
{
    options.ChannelId = builder.Configuration["Line:Login:ChannelId"]!;
    options.ChannelSecret = builder.Configuration["Line:Login:ChannelSecret"]!;
});
```

```csharp
var verifier = LinePkce.CreateCodeVerifier();           // keep this until the callback
var url = line.BuildAuthorizationUrl(new LineAuthorizationRequest
{
    RedirectUri = "https://example.com/callback",
    State = state,
    Nonce = nonce,
    CodeChallenge = LinePkce.ComputeCodeChallenge(verifier),
});

// …on the callback:
var login = await line.CompleteLoginAsync(code, redirectUri, verifier, nonce);
if (login.TryGetValue(out var result))
{
    var userId = result.Profile.UserId;
    var isFriend = result.IsFriend;    // null when the Login channel has no linked official account
}
```

`CompleteLoginAsync` runs the exchange, the profile lookup, the friendship lookup and the id_token validation. Of the four only the friendship lookup is allowed to fail — recorded as a `null` `IsFriend`, which is a different fact from `false`.

### 3. Push, reply, and webhooks

```csharp
builder.Services.AddLineMessaging(options =>
{
    options.ChannelAccessToken = builder.Configuration["Line:Messaging:ChannelAccessToken"]!;
    options.ChannelSecret = builder.Configuration["Line:Messaging:ChannelSecret"]!;
});
```

```csharp
await line.PushTextAsync(userId, "您追蹤的園所有新的裁罰紀錄");

await line.PushAsync(userId, [
    new TextMessage("新紀錄") { QuoteToken = quoteToken },
    new StickerMessage("446", "1988"),
], new LinePushOptions { RetryKey = jobId });   // same key for one logical push, so a retry cannot duplicate it
```

```csharp
app.MapLineWebhook("/line/webhook", async (evt, context, ct) =>
{
    if (evt.Type == LineWebhookEventTypes.Message && evt.Message?.Text is { } text)
    {
        var line = context.RequestServices.GetRequiredService<ILineMessagingClient>();
        await line.ReplyTextAsync(evt.ReplyToken!, $"收到:{text}", ct);
    }
});
```

The endpoint verifies the signature before parsing — a bad one is a 401, an unparsable body a 400 — and answers 200 for everything else. One event's handler throwing is logged and does not stop the rest of the batch: a non-2xx makes LINE redeliver the **whole** batch, so letting one broken handler return a 500 means the events that did succeed get redone over and over while the broken one stays broken.

---

## Things about LINE worth knowing before you start

**A Login channel and a Messaging channel are two different channels.** Each has its own id and secret, and they are separate entries in the LINE Developers console. Putting one's credentials where the other belongs earns a 400 and nothing that points at the mix-up. `AddLineLogin` and `AddLineMessaging` therefore take separate options and register separate HTTP clients.

**`bot_prompt` and the friendship API both need a linked official account.** Without one on the Login channel, LINE ignores `bot_prompt` entirely — sign-in proceeds, the add-friend step simply never appears — and the friendship endpoint answers 4xx. Neither raises an error you can see, which is the usual explanation for "it is configured but nothing happens". This package treats an unreadable friendship status as unknown rather than as a failure.

**Replies are free; pushes are not.** A reply token costs nothing against the monthly quota, while every push does. The token is single-use and short-lived, so replying wherever the flow allows it is what decides whether the quota lasts the month. A redelivered event's token has usually expired already.

**A multicast takes at most 500 recipients**, and one request at most 5 messages. Both are checked before sending, so an oversized call fails on your machine rather than costing a slice of quota at LINE's.

**A webhook event can arrive twice.** LINE redelivers when it did not get a 200 in time, with the same `WebhookEventId` and `IsRedelivery` set. Any handler with side effects — recording, sending, deducting — needs to deduplicate on that id.

**Rich menus are created and painted in two steps.** `CreateRichMenuAsync` returns an id and `UploadRichMenuImageAsync` puts the image on it; a menu created without an image can still be linked, and shows up blank. The tappable areas are not drawn on the image either — where the buttons look like they are and where they actually are is your job to keep in agreement.

---

## API at a glance

### `ILineLoginClient`

| Member | What it does |
| --- | --- |
| `BuildAuthorizationUrl(request)` | The URL to send the browser to. PKCE, nonce, `bot_prompt`, `ui_locales`, forced consent, auto-login switches. |
| `ExchangeCodeAsync(code, redirectUri, codeVerifier?)` | Authorization code → tokens. |
| `RefreshAsync(refreshToken)` | New tokens, including a new refresh token. |
| `RevokeAsync(accessToken)` | Revokes an access token. |
| `VerifyAccessTokenAsync(accessToken)` | Scopes, owning channel, remaining seconds. |
| `VerifyIdTokenAsync(idToken, nonce?, userId?)` | Has LINE verify remotely. Prefer `LineIdTokenValidator` locally. |
| `GetProfileAsync(accessToken)` | User id, display name, avatar, status message. |
| `GetUserInfoAsync(accessToken)` | The OpenID Connect userinfo. |
| `GetFriendshipStatusAsync(accessToken)` | Whether the account has been added. |
| `CompleteLoginAsync(code, redirectUri, codeVerifier?, nonce?)` | All of the above, as one call. |

`LineIdTokenValidator.Validate(...)` and `LinePkce.CreateCodeVerifier()` / `ComputeCodeChallenge()` are static and need no client.

### `ILineMessagingClient`

| Group | Members |
| --- | --- |
| Sending | `PushAsync` · `PushTextAsync` · `PushRawJsonAsync` · `MulticastAsync` · `BroadcastAsync` · `BroadcastTextAsync` · `BroadcastRawJsonAsync` · `ReplyAsync` · `ReplyTextAsync` · `ReplyRawJsonAsync` |
| Account | `GetQuotaAsync` · `GetQuotaConsumptionAsync` · `GetBotInfoAsync` · `GetProfileAsync` · `GetMessageContentAsync` |
| Rich menu | `CreateRichMenuAsync` · `UploadRichMenuImageAsync` · `GetRichMenuListAsync` · `GetRichMenuAsync` · `DeleteRichMenuAsync` · `SetDefaultRichMenuAsync` · `ClearDefaultRichMenuAsync` · `GetDefaultRichMenuIdAsync` · `LinkRichMenuToUserAsync` · `UnlinkRichMenuFromUserAsync` · `GetRichMenuIdOfUserAsync` |

Message types: `TextMessage`, `ImageMessage`, `VideoMessage`, `AudioMessage`, `LocationMessage`, `StickerMessage`, `FlexMessage`, and `RawMessage` for anything not modelled yet. Actions: `UriAction`, `MessageAction`, `PostbackAction`, `RawAction`.

The `*RawJsonAsync` overloads accept either a single message object or an array of them, and wrap either into `{"messages": […]}`. They are the way out when LINE adds something this package has not caught up with.

### Webhooks

| Member | What it does |
| --- | --- |
| `LineWebhookSignature.Verify(secret, body, signature)` | Fixed-time comparison; `false` for a missing header, a blank secret, or a signature that is not base64. |
| `LineWebhookParser.Parse(body)` | Events, with anything unmodelled kept in `LineWebhookEvent.Raw`. |
| `HttpRequest.ReadLineWebhookAsync(secret)` | Reads the raw bytes, verifies, parses. |
| `MapLineWebhook(pattern, handler)` | The endpoint. One overload per event, one for the whole payload. |

### Error codes

`line.not_configured` · `line.validation.too_many_recipients` · `line.validation.too_many_messages` · `line.validation.invalid_json` · `line.api.error` · `line.api.invalid_response` · `line.id_token.invalid_format` · `line.id_token.unsupported_algorithm` · `line.id_token.invalid_signature` · `line.id_token.invalid_issuer` · `line.id_token.invalid_audience` · `line.id_token.expired` · `line.id_token.nonce_mismatch` · `line.webhook.invalid_signature` · `line.webhook.invalid_payload`

These are a public contract and do not change once published.

---

## Compatibility

- **Target framework:** net10.0.
- **Dependencies:** `Ozakboy.Core.Abstractions`, `Ozakboy.Http`, and `Microsoft.Extensions.*`. `Ozakboy.Line.AspNetCore` adds a framework reference to `Microsoft.AspNetCore.App`. No third party appears in the transitive graph, and no JWT library is pulled in.
- **Tests:** offline throughout. The suite never calls LINE, and uses placeholder credentials only.

## Licence

MIT. See [LICENSE](LICENSE).
