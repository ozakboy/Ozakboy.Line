# Ozakboy.Line

LINE Login, the Messaging API, and webhooks for .NET — with one line of setup for ASP.NET Core.

English | [繁體中文](README_zh-TW.md)

```
dotnet add package Ozakboy.Line
dotnet add package Ozakboy.Line.AspNetCore
dotnet add package Ozakboy.Line.Mcp
```

| Package | What it is for |
| --- | --- |
| `Ozakboy.Line` | The core: LINE Login, the Messaging API, webhook verification and parsing, plus message templates and keyword auto reply. |
| `Ozakboy.Line.AspNetCore` | The integration: the `AddLineLogin()` authentication scheme and the `MapLineWebhook()` endpoint. |
| `Ozakboy.Line.Mcp` | Optional: the above as MCP tools, so an outside AI can operate the account — by default it can propose, not send. |

Requires .NET 10. The core and integration packages depend only on Microsoft packages and `Ozakboy.*`; the MCP package also takes the first-party `ModelContextProtocol.AspNetCore` (see Compatibility).

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

## Quick replies

A row of buttons under a message. The whole row disappears once the user taps one or types something instead, so it is not a menu — an entry point that stays put belongs in a rich menu.

```csharp
await line.PushAsync(userId, [
    new TextMessage("Which district?")
    {
        QuickReply = new LineQuickReply
        {
            Items =
            {
                new LineQuickReplyItem(new MessageAction("Taipei") { Label = "Taipei" }),
                new LineQuickReplyItem(new PostbackAction("area=tpe", "Taipei it is") { Label = "No trace" }),
                new LineQuickReplyItem(new LocationAction { Label = "Send my location" }),
                new LineQuickReplyItem(new DatetimePickerAction("when", DatetimePickerAction.ModeDate) { Label = "Pick a date" }),
            },
        },
    },
]);
```

There are ten actions: `MessageAction`, `PostbackAction`, `UriAction`, `DatetimePickerAction`, `CameraAction`, `CameraRollAction`, `LocationAction`, `ClipboardAction`, `RichMenuSwitchAction`, and `RawAction` for whatever is not modelled yet. `UriAction.AltUriDesktop` gives the desktop clients a different address — a LIFF app or a `line://` address only a phone understands opens as a blank page there — and `PostbackAction.InputOption` decides what the keyboard and the rich menu do after the tap.

**Thirteen buttons is the cap**, and going over does not mean LINE shows the first thirteen: it rejects the whole message. Every message is checked before sending, so an oversized call fails on your machine, with the actual count in the error.

---

## Message templates

A reusable set of LINE messages that can carry variables. What is stored is **the JSON of a message array** rather than a piece of plain text, so images, Flex layouts and quick replies all fit into a template — and changing the wording never means changing code.

```csharp
services.AddLineTemplates(o => o.UseJsonFile("data/line-templates.json"));   // in memory when not set
```

```csharp
var store = provider.GetRequiredService<ILineTemplateStore>();

await store.UpsertAsync(new LineMessageTemplate
{
    Name = "Sanction notice",
    MessagesJson = """[{"type":"text","text":"{{name}} has a new sanction on record, document {{docNo}}."}]""",
});

var rendered = LineTemplateRenderer.Render(template.MessagesJson, new Dictionary<string, string>
{
    ["name"] = "Sunshine Kindergarten",
    ["docNo"] = "1130001",
});

if (rendered.IsSuccess)
{
    await line.PushRawJsonAsync(userId, rendered.GetValueOrThrow());
}
```

A placeholder is `{{name}}`, where the name starts with a letter or an underscore. Values are **escaped for JSON** before they go in: without that, one quotation mark in a display name is enough to break the whole document, and the symptom is a 400 from LINE that names no field. A missing variable is a `line.validation.missing_template_variables` failure naming which ones, rather than `{{name}}` going out verbatim to a user.

Two stores ship with the package — in memory and one JSON file, the latter with atomic writes and one lock shared by reads and writes. A host with a database implements `ILineTemplateStore` instead; the package touches no ORM.

---

## Keyword auto reply

A rule is **data** rather than code: the whole thing lives in a store and can be edited from an admin page or an MCP tool, so changing a welcome line never means a deployment.

```csharp
services.AddLineMessaging(o => { /* … */ });
services.AddLineAutoReply(
    o => o.Enabled = true,
    s => s.UseJsonFile("data/line-auto-replies.json"));

app.MapLineWebhook("/line/webhook", o => o.AutoReply = true, async (evt, context, ct) =>
{
    // Auto reply has already run; here is what it did:
    var outcome = context.Items[LineWebhookItems.AutoReplyOutcome] as LineAutoReplyOutcome;
    if (outcome?.Kind == LineAutoReplyOutcomeKind.Replied)
    {
        return;   // a rule answered, so there is nothing to add
    }

    // …the host's own handling
});
```

There are six match modes: `Exact`, `StartsWith`, `Contains` and `Regex`, plus two that read no text — `Follow`, the welcome message for an add-friend event, and `Fallback`, the answer to a text message no other rule matched. Expressing both as rules makes the **rule table the single source**: changing the welcome message from an admin page is an upsert, with no second copy hiding in a settings file or an environment variable.

Rules are ordered by `Priority` first, smallest going first, and within a tie by how specific the match is (`Exact` > `StartsWith` > `Contains` > `Regex`). That second level matters: once there are many rules, one loose `Contains` easily swallows a pile of precise `Exact` ones, and nothing in the rule list shows it — every rule looks right on its own.

A reply can use `{{text}}`, the message the user sent, and `{{displayName}}`, their display name, which is looked up only when a rule actually uses it. Regular expressions are matched under a 100-millisecond cap, and a timeout or an invalid expression counts as no match rather than throwing: one badly written rule should not turn the whole webhook into a 500, which would have LINE redeliver the entire batch.

Auto reply always uses a **reply token** rather than a push: a reply costs no quota, and auto reply is the heaviest consumer of quota there is.

---

## Letting an outside AI operate the account: `Ozakboy.Line.Mcp`

The above as a set of MCP tools, so Claude, a custom connector, or any MCP client can read the account's state, draft broadcasts, edit rules and templates, and replace rich menus.

```csharp
builder.Services.AddLineMessaging(o => { /* … */ });
builder.Services.AddLineTemplates();
builder.Services.AddLineAutoReply();

builder.Services.AddLineMcpServer(
    o =>
    {
        o.ApiKey = builder.Configuration["LINE_MCP_API_KEY"];   // unset means the endpoint is off
        o.SendMode = LineMcpSendMode.Review;                    // the default
        o.MaxSendRequestsPerDay = 10;
    },
    s => s.UseJsonFileOutbox("data/line-mcp-outbox.json"));

// Both gates go before UseRouting
app.UseLineMcpWellKnownNotFound();
app.UseLineMcpKeyGate();
app.UseRouting();
// …
app.MapLineMcp();
```

**The rule is that the AI cannot send.** In the default `Review` mode, `line_send_push`, `line_send_multicast`, `line_send_broadcast` and `line_replace_rich_menu` send nothing at all: they write the payload to an outbox as `PendingReview` for a person on the host's side to approve. The approval API, `ILineMcpOutboxService.ApproveAndSendAsync`, is **not a tool** and belongs to the host — a review queue is worth something because whoever proposes cannot approve. `Direct` mode sends straight away and still writes a record: without it there is nowhere afterwards to find out who sent that message, when, or why.

A daily cap sits on top of that — ten items by default, counted against the local date in `TimeZoneId`. It is a runaway guard: an AI retrying the same tool in a loop is a common failure, and without a cap that loop fills the review queue until nobody is willing to read it.

**The key gate.** The public address is `{prefix}/{ApiKey}`, with `/mcp/line` as the default prefix. The key travels in the path rather than a header, because most MCP connectors give the user one field for a URL and nowhere for a custom header. It is compared in fixed time, and **a failure answers 404 rather than 401 or 403**: a 401 or 403 tells the caller that something is here, and this endpoint's existence is not worth announcing. An unset `ApiKey` means the feature is off and the whole subtree is a 404; rotating the key means changing the setting and restarting.

`UseLineMcpWellKnownNotFound()` answers a clean JSON 404 for everything under `/.well-known/`: an MCP connector probes `/.well-known/oauth-protected-resource` before connecting, and when that lands in MVC and comes back as an HTML sign-in page or a 302, the connector decides the site needs OAuth and cannot connect. ⚠ This gate covers the **whole `/.well-known` subtree**, so serving a `security.txt` later means handling it before this line.

A host that already runs its own MCP server just adds the set to it:

```csharp
builder.Services.AddLineMcp(o => o.ApiKey = key);
builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)
    .WithTools<MyOwnTools>()
    .WithLineTools();
```

The tools:

| Group | Tools |
| --- | --- |
| Reading | `line_get_bot_info` · `line_get_quota` · `line_get_profile` |
| Sending (reviewed) | `line_send_push` · `line_send_multicast` · `line_send_broadcast` |
| Outbox | `line_list_outbox` · `line_get_outbox` · `line_cancel_outbox` |
| Rich menus | `line_list_rich_menus` · `line_get_default_rich_menu` · `line_list_rich_menu_aliases` · `line_validate_rich_menu` · `line_replace_rich_menu` (reviewed) |
| Auto reply | `line_list_auto_replies` · `line_upsert_auto_reply` · `line_delete_auto_reply` · `line_test_auto_reply` |
| Templates | `line_list_templates` · `line_get_template` · `line_upsert_template` · `line_delete_template` · `line_render_template` |

The rule and template tools **skip the review queue**, because they send nothing by themselves: a broken rule answers wrongly rather than reaching out to everyone uninvited, and undoing it is one more upsert. To forbid them outright there are `AllowRuleEdits`, `AllowTemplateEdits` and `AllowRichMenuChanges`. Where the host has not called `AddLineTemplates` or `AddLineAutoReply`, those tools are still mounted and answer that the feature is off — which says more than their absence would.

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
| Rich menu | `CreateRichMenuAsync` · `UploadRichMenuImageAsync` · `GetRichMenuListAsync` · `GetRichMenuAsync` · `DeleteRichMenuAsync` · `SetDefaultRichMenuAsync` · `ClearDefaultRichMenuAsync` · `GetDefaultRichMenuIdAsync` · `LinkRichMenuToUserAsync` · `UnlinkRichMenuFromUserAsync` · `GetRichMenuIdOfUserAsync` · `ValidateRichMenuAsync` |
| Replacement and aliases | `ReplaceRichMenuAsync` · `CreateRichMenuAliasAsync` · `UpdateRichMenuAliasAsync` · `DeleteRichMenuAliasAsync` · `GetRichMenuAliasAsync` · `GetRichMenuAliasListAsync` · `LinkRichMenuToUsersAsync` · `UnlinkRichMenuFromUsersAsync` |

Message types: `TextMessage`, `ImageMessage`, `VideoMessage`, `AudioMessage`, `LocationMessage`, `StickerMessage`, `FlexMessage`, and `RawMessage` for anything not modelled yet. Actions: `UriAction`, `MessageAction`, `PostbackAction`, `DatetimePickerAction`, `CameraAction`, `CameraRollAction`, `LocationAction`, `ClipboardAction`, `RichMenuSwitchAction`, `RawAction`. Quick replies: `LineQuickReply` and `LineQuickReplyItem`.

`ReplaceRichMenuAsync` runs create, upload, set-as-default, repoint-the-alias and delete-the-old-menu as one call. Its failure handling is asymmetric on purpose: **a failed image upload deletes the menu just created**, since a menu without an image is worse than no menu, while **a failed deletion of the old menu is only logged and the call still succeeds**, since the new menu is already live and reporting a failure would have the caller redo it and end up with one menu more.

The `*RawJsonAsync` overloads accept either a single message object or an array of them, and wrap either into `{"messages": […]}`. They are the way out when LINE adds something this package has not caught up with.

### Templates and auto reply

| Member | What it does |
| --- | --- |
| `LineTemplateVariables.Extract(json)` | The variable names a template uses, deduplicated and in order. |
| `LineTemplateRenderer.Render(json, values)` | Substitutes and checks the result is still a valid one-to-five-message array. |
| `LineTemplateRenderer.RenderMessages(json, values)` | The same, returning `RawMessage` objects. |
| `LineTemplateRenderer.Validate(json)` | Validation without substitution, for use before storing. |
| `ILineTemplateStore` | `ListAsync` · `GetAsync` · `UpsertAsync` · `DeleteAsync`, with in-memory and JSON-file implementations. |
| `LineAutoReplyMatcher.Match(rules, text)` | Picks the rule that answers a message, across the four text modes. |
| `LineAutoReplyMatcher.FindFirst(rules, mode)` | The lowest-priority `Follow` or `Fallback` rule. |
| `LineAutoReplyMatcher.Validate(rule)` | The pattern, the regular expression, and the reply body, all at once. |
| `ILineAutoReplyStore` | The same shape as `ILineTemplateStore`. |
| `ILineAutoReplyService.HandleAsync(evt)` | Applies the rules to one webhook event: `Skipped`, `NoMatch`, or `Replied`. |

### MCP (`Ozakboy.Line.Mcp`)

| Member | What it does |
| --- | --- |
| `AddLineMcp(configure, stores?)` | The settings, the outbox, the outbox service, and the named HTTP client used to fetch images. |
| `AddLineMcpServer(configure, stores?)` | All of the above plus a stateless MCP server carrying just the LINE tools. |
| `IMcpServerBuilder.WithLineTools()` | Adds the six tool sets to an MCP server the host already has. |
| `UseLineMcpKeyGate(prefix?)` | The key gate. **Must come before `UseRouting`.** |
| `UseLineMcpWellKnownNotFound()` | A clean 404 for everything under `/.well-known/`. |
| `MapLineMcp(prefix?)` | The MCP endpoint, on the same prefix the gate uses. |
| `ILineMcpOutboxService` | `ApproveAndSendAsync` · `RejectAsync` · `CancelAsync`. **For the host only; never a tool.** |
| `ILineMcpOutboxStore` | The outbox store, with in-memory and JSON-file implementations. |

Outbox statuses: `PendingReview` → `Approved` / `Sent` / `Failed` / `Rejected` / `Canceled`. They only move forward, and something already sent never returns to review — an item that could go back to review is one that could be approved twice.

### Webhooks

| Member | What it does |
| --- | --- |
| `LineWebhookSignature.Verify(secret, body, signature)` | Fixed-time comparison; `false` for a missing header, a blank secret, or a signature that is not base64. |
| `LineWebhookParser.Parse(body)` | Events, with anything unmodelled kept in `LineWebhookEvent.Raw`. |
| `HttpRequest.ReadLineWebhookAsync(secret)` | Reads the raw bytes, verifies, parses. |
| `MapLineWebhook(pattern, handler)` | The endpoint. One overload per event, one for the whole payload. |
| `MapLineWebhook(pattern, configure, handler)` | The same, with `LineWebhookEndpointOptions.AutoReply` to run auto reply first. |
| `LineWebhookItems.AutoReplyOutcome` | The `HttpContext.Items` key the auto reply outcome lands under. |

### Error codes

`line.not_configured` · `line.validation.too_many_recipients` · `line.validation.too_many_messages` · `line.validation.invalid_json` · `line.validation.too_many_quick_reply_items` · `line.validation.missing_template_variables` · `line.validation.invalid_auto_reply_rule` · `line.mcp.outbox_not_found` · `line.mcp.outbox_invalid_status` · `line.mcp.image_fetch_failed` · `line.api.error` · `line.api.invalid_response` · `line.id_token.invalid_format` · `line.id_token.unsupported_algorithm` · `line.id_token.invalid_signature` · `line.id_token.invalid_issuer` · `line.id_token.invalid_audience` · `line.id_token.expired` · `line.id_token.nonce_mismatch` · `line.webhook.invalid_signature` · `line.webhook.invalid_payload`

These are a public contract and do not change once published.

---

## Compatibility

- **Target framework:** net10.0.
- **Dependencies:** `Ozakboy.Core.Abstractions`, `Ozakboy.Http`, and `Microsoft.Extensions.*`. `Ozakboy.Line.AspNetCore` adds a framework reference to `Microsoft.AspNetCore.App`. No third party appears in the transitive graph of either package, and no JWT library is pulled in.
- **The MCP package's exception:** `Ozakboy.Line.Mcp` depends on `ModelContextProtocol.AspNetCore`, the official C# SDK for MCP, maintained by the modelcontextprotocol organisation together with Microsoft. This is the **one listed exception** to the dependency policy. MCP is a versioned wire protocol, and implementing it here would mean maintaining an implementation that falls behind every revision — where falling behind shows up as a connector that attaches successfully and then lists no tools. To avoid the dependency, do not install this package; the core and integration packages are untouched by it.
- **Tests:** offline throughout. The suite never calls LINE, and uses placeholder credentials only.

## Licence

MIT. See [LICENSE](LICENSE).
