using System.Buffers;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Actions;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// Flex 版面的本地驗證:走訪整棵強型別節點樹,回報第一個違規(附版面路徑)。
/// Local validation of a Flex layout: walks the whole typed node tree and reports the first violation, with the layout path.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RawFlexContainer"/> 與 <see cref="RawFlexComponent"/> 是逃生口,內容<b>不驗</b>(只計入整體大小);
/// 規則寧可寬鬆也不誤擋 —— 本地多擋一則合法訊息,呼叫端除了改走 Raw 之外沒有別的辦法。
/// <see cref="RawFlexContainer"/> and <see cref="RawFlexComponent"/> are the escape hatch and their content is <b>not</b>
/// checked (it only counts towards the overall size). The rules lean lenient rather than wrongly blocking: a legitimate
/// message stopped locally leaves the caller no way out other than going Raw.
/// </para>
/// <para>
/// 路徑以版面容器為根:輪播的第三個氣泡的內文第一個元件的位址是 <c>contents[2].body.contents[0].url</c>。
/// Paths are rooted at the layout container: the address of the first body component of a carousel's third bubble is
/// <c>contents[2].body.contents[0].url</c>.
/// </para>
/// </remarks>
internal static partial class FlexValidator
{
    private static readonly string[] SpacingKeywords = ["none", "xs", "sm", "md", "lg", "xl", "xxl"];

    private static readonly string[] SizeKeywords = ["xxs", "xs", "sm", "md", "lg", "xl", "xxl", "3xl", "4xl", "5xl"];

    private static readonly string[] ImageSizeKeywords = ["xxs", "xs", "sm", "md", "lg", "xl", "xxl", "3xl", "4xl", "5xl", "full"];

    private static readonly string[] BubbleSizes = ["nano", "micro", "deca", "hecto", "kilo", "mega", "giga"];

    private static readonly string[] BorderWidthKeywords = ["none", "light", "normal", "medium", "semi-bold", "bold"];

    private static readonly string[] Layouts = ["horizontal", "vertical", "baseline"];

    private static readonly string[] JustifyContents = ["flex-start", "center", "flex-end", "space-between", "space-around", "space-evenly"];

    private static readonly string[] AlignItemsValues = ["flex-start", "center", "flex-end"];

    private static readonly string[] Aligns = ["start", "end", "center"];

    private static readonly string[] Gravities = ["top", "bottom", "center"];

    private static readonly string[] Weights = ["regular", "bold"];

    private static readonly string[] TextStyles = ["normal", "italic"];

    private static readonly string[] Decorations = ["none", "underline", "line-through"];

    private static readonly string[] AspectModes = ["cover", "fit"];

    private static readonly string[] ButtonHeights = ["sm", "md"];

    private static readonly string[] ButtonStyles = ["link", "primary", "secondary"];

    private static readonly string[] AdjustModes = ["shrink-to-fit"];

    private static readonly string[] Positions = ["relative", "absolute"];

    private static readonly string[] Directions = ["ltr", "rtl"];

    private static readonly string[] NoKeywords = [];

    [GeneratedRegex(@"^\d+(\.\d+)?px$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex PixelPattern();

    [GeneratedRegex(@"^\d+(\.\d+)?%$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex PercentPattern();

    [GeneratedRegex(@"^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex ColorPattern();

    [GeneratedRegex(@"^\d+(\.\d+)?:\d+(\.\d+)?$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex AspectRatioPattern();

    [GeneratedRegex(@"^\d+(\.\d+)?deg$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex AnglePattern();

    /// <summary>
    /// 驗證整個容器(含大小上限);Raw 容器只檢查大小。
    /// Validates a whole container, size limit included; a Raw container is checked for size only.
    /// </summary>
    /// <param name="container">容器。The container.</param>
    /// <returns>通過時為 <see langword="null"/>。<see langword="null"/> when it passes.</returns>
    internal static Error? ValidateContainer(FlexContainer container)
    {
        var structural = container switch
        {
            FlexBubble bubble => Bubble(bubble, string.Empty),
            FlexCarousel carousel => Carousel(carousel),
            _ => null,
        };

        if (structural is not null)
        {
            return structural;
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, SizeMeasureOptions))
        {
            container.WriteTo(writer);
        }

        return CheckSize(container.Type, buffer.WrittenCount);
    }

    /// <summary>
    /// 驗證原樣 JSON 版面的大小(結構不驗,那是原樣路徑的逃生口契約)。
    /// Validates the size of a verbatim JSON layout; its structure is not checked, that being the verbatim path's escape-hatch contract.
    /// </summary>
    /// <param name="contents">版面 JSON。The layout JSON.</param>
    /// <returns>通過時為 <see langword="null"/>。<see langword="null"/> when it passes.</returns>
    internal static Error? ValidateVerbatim(JsonElement contents)
    {
        if (contents.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        var type = contents.ValueKind == JsonValueKind.Object
            && contents.TryGetProperty("type", out var typeElement)
            && typeElement.ValueKind == JsonValueKind.String
                ? typeElement.GetString() ?? string.Empty
                : string.Empty;

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, SizeMeasureOptions))
        {
            contents.WriteTo(writer);
        }

        return CheckSize(type, buffer.WrittenCount);
    }

    // 量測大小時不跳脫非 ASCII:實際送出會把中文寫成 \uXXXX(較大),但 LINE 以什麼為準沒有明文,
    // 取較小的 UTF-8 原始長度,寧可放行也不誤擋。
    // Non-ASCII is not escaped while measuring: the wire form writes CJK as \uXXXX (larger), but which one LINE counts is
    // not documented, so the smaller raw UTF-8 length is used — better to let one through than to block wrongly.
    private static JsonWriterOptions SizeMeasureOptions { get; } = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static Error? CheckSize(string containerType, int bytes)
    {
        var limit = containerType == "carousel" ? LineMessagingLimits.MaxFlexCarouselBytes : LineMessagingLimits.MaxFlexBubbleBytes;
        return bytes > limit
            ? Fail(
                string.Empty,
                string.Create(CultureInfo.InvariantCulture, $"版面 JSON 大小 {bytes} 位元組超過上限 {limit}。"),
                string.Create(CultureInfo.InvariantCulture, $"the layout JSON is {bytes} bytes, over the limit of {limit}."))
            : null;
    }

    private static Error? Carousel(FlexCarousel carousel)
    {
        var count = carousel.Contents.Count;
        if (count is < LineMessagingLimits.MinFlexCarouselBubbles or > LineMessagingLimits.MaxFlexCarouselBubbles)
        {
            return Fail(
                "contents",
                string.Create(CultureInfo.InvariantCulture, $"輪播的氣泡數必須是 {LineMessagingLimits.MinFlexCarouselBubbles}~{LineMessagingLimits.MaxFlexCarouselBubbles},實際是 {count}。"),
                string.Create(CultureInfo.InvariantCulture, $"a carousel must hold {LineMessagingLimits.MinFlexCarouselBubbles} to {LineMessagingLimits.MaxFlexCarouselBubbles} bubbles, but holds {count}."));
        }

        for (var index = 0; index < count; index++)
        {
            if (carousel.Contents[index] is not { } bubble)
            {
                return Fail(Index("contents", index), "氣泡不可為 null。", "a bubble must not be null.");
            }

            if (Bubble(bubble, Index("contents", index)) is { } error)
            {
                return error;
            }
        }

        return null;
    }

    private static Error? Bubble(FlexBubble bubble, string path)
    {
        if (bubble.Header is null && bubble.Hero is null && bubble.Body is null && bubble.Footer is null)
        {
            return Fail(path, "氣泡至少要有 header、hero、body、footer 其中之一。", "a bubble needs at least one of header, hero, body and footer.");
        }

        return Keyword(path, "size", bubble.Size, BubbleSizes)
            ?? Keyword(path, "direction", bubble.Direction, Directions)
            ?? (bubble.Header is { } header ? Box(header, Join(path, "header")) : null)
            ?? (bubble.Hero is { } hero ? Hero(hero, Join(path, "hero")) : null)
            ?? (bubble.Body is { } body ? Box(body, Join(path, "body")) : null)
            ?? (bubble.Footer is { } footer ? Box(footer, Join(path, "footer")) : null)
            ?? (bubble.Styles is { } styles ? Styles(styles, Join(path, "styles")) : null)
            ?? ActionOf(bubble.Action, Join(path, "action"));
    }

    private static Error? Styles(FlexBubbleStyles styles, string path) =>
        BlockStyle(styles.Header, Join(path, "header"))
        ?? BlockStyle(styles.Hero, Join(path, "hero"))
        ?? BlockStyle(styles.Body, Join(path, "body"))
        ?? BlockStyle(styles.Footer, Join(path, "footer"));

    private static Error? BlockStyle(FlexBlockStyle? style, string path) =>
        style is null
            ? null
            : Color(path, "backgroundColor", style.BackgroundColor) ?? Color(path, "separatorColor", style.SeparatorColor);

    private static Error? Hero(FlexComponent hero, string path) =>
        hero switch
        {
            FlexVideo video => Video(video, path),
            FlexImage or FlexBox or RawFlexComponent => Component(hero, path),
            _ => Fail(path, $"hero 只能是 image、box 或 video,實際是 {hero.Type}。", $"the hero can only be an image, a box or a video, but is {hero.Type}."),
        };

    private static Error? Component(FlexComponent component, string path) =>
        component switch
        {
            FlexBox box => Box(box, path),
            FlexText text => Text(text, path),
            FlexImage image => Image(image, path),
            FlexIcon icon => Icon(icon, path),
            FlexButton button => Button(button, path),
            FlexSeparator separator => Margin(path, "margin", separator.Margin) ?? Color(path, "color", separator.Color),
            FlexFiller filler => NonNegative(path, "flex", filler.Flex),
            FlexVideo => Fail(path, "video 只能當 bubble 的 hero。", "a video can only be the hero of a bubble."),
            _ => null,
        };

    private static Error? Box(FlexBox box, string path)
    {
        if (string.IsNullOrEmpty(box.Layout))
        {
            return Fail(Join(path, "layout"), "layout 必填。", "layout is required.");
        }

        if (Keyword(path, "layout", box.Layout, Layouts) is { } layoutError)
        {
            return layoutError;
        }

        var baseline = box.Layout == "baseline";
        for (var index = 0; index < box.Contents.Count; index++)
        {
            var child = box.Contents[index];
            var childPath = Index(Join(path, "contents"), index);
            if (child is null)
            {
                return Fail(childPath, "元件不可為 null。", "a component must not be null.");
            }

            if (baseline && child is not (FlexText or FlexIcon or FlexFiller or RawFlexComponent))
            {
                return Fail(
                    childPath,
                    $"baseline 方塊只能放 text、icon、filler,實際是 {child.Type}。",
                    $"a baseline box can only hold text, icon and filler, but this is {child.Type}.");
            }

            if (Component(child, childPath) is { } childError)
            {
                return childError;
            }
        }

        return NonNegative(path, "flex", box.Flex)
            ?? Spacing(path, "spacing", box.Spacing)
            ?? Margin(path, "margin", box.Margin)
            ?? Keyword(path, "position", box.Position, Positions)
            ?? Offsets(path, box.OffsetTop, box.OffsetBottom, box.OffsetStart, box.OffsetEnd)
            ?? Color(path, "backgroundColor", box.BackgroundColor)
            ?? Color(path, "borderColor", box.BorderColor)
            ?? Dimension(path, "borderWidth", box.BorderWidth, BorderWidthKeywords, percent: false)
            ?? Dimension(path, "cornerRadius", box.CornerRadius, SpacingKeywords, percent: false)
            ?? Dimension(path, "width", box.Width, NoKeywords, percent: true)
            ?? Dimension(path, "maxWidth", box.MaxWidth, NoKeywords, percent: true)
            ?? Dimension(path, "height", box.Height, NoKeywords, percent: true)
            ?? Dimension(path, "maxHeight", box.MaxHeight, NoKeywords, percent: true)
            ?? Dimension(path, "paddingAll", box.PaddingAll, SpacingKeywords, percent: true)
            ?? Dimension(path, "paddingTop", box.PaddingTop, SpacingKeywords, percent: true)
            ?? Dimension(path, "paddingBottom", box.PaddingBottom, SpacingKeywords, percent: true)
            ?? Dimension(path, "paddingStart", box.PaddingStart, SpacingKeywords, percent: true)
            ?? Dimension(path, "paddingEnd", box.PaddingEnd, SpacingKeywords, percent: true)
            ?? Keyword(path, "justifyContent", box.JustifyContent, JustifyContents)
            ?? Keyword(path, "alignItems", box.AlignItems, AlignItemsValues)
            ?? (box.Background is { } gradient ? Gradient(gradient, Join(path, "background")) : null)
            ?? ActionOf(box.Action, Join(path, "action"));
    }

    private static Error? Gradient(FlexLinearGradient gradient, string path)
    {
        if (!AnglePattern().IsMatch(gradient.Angle ?? string.Empty))
        {
            return Fail(
                Join(path, "angle"),
                $"漸層角度格式必須是數字加 deg(例如 90deg),實際是:{LineMessageValidation.Quote(gradient.Angle ?? string.Empty)}。",
                $"a gradient angle must be a number followed by deg (such as 90deg), but is: {LineMessageValidation.Quote(gradient.Angle ?? string.Empty)}.");
        }

        if (gradient.CenterPosition is { } center && !PercentPattern().IsMatch(center))
        {
            return Fail(
                Join(path, "centerPosition"),
                $"centerPosition 必須是百分比(例如 50%),實際是:{LineMessageValidation.Quote(center)}。",
                $"centerPosition must be a percentage (such as 50%), but is: {LineMessageValidation.Quote(center)}.");
        }

        return RequiredColor(path, "startColor", gradient.StartColor)
            ?? RequiredColor(path, "endColor", gradient.EndColor)
            ?? Color(path, "centerColor", gradient.CenterColor);
    }

    private static Error? Text(FlexText text, string path)
    {
        var hasText = !string.IsNullOrEmpty(text.Text);
        var hasContents = text.Contents.Count > 0;
        if (hasText == hasContents)
        {
            return Fail(
                path,
                hasText ? "text 與 contents(span)只能擇一,不能同時設定。" : "text 與 contents(span)必須設定其中之一。",
                hasText ? "text and contents (spans) are exclusive, and both are set." : "one of text and contents (spans) must be set.");
        }

        for (var index = 0; index < text.Contents.Count; index++)
        {
            var spanPath = Index(Join(path, "contents"), index);
            if (text.Contents[index] is not { } span)
            {
                return Fail(spanPath, "span 不可為 null。", "a span must not be null.");
            }

            if (Span(span, spanPath) is { } spanError)
            {
                return spanError;
            }
        }

        return NonNegative(path, "flex", text.Flex)
            ?? Margin(path, "margin", text.Margin)
            ?? Keyword(path, "position", text.Position, Positions)
            ?? Offsets(path, text.OffsetTop, text.OffsetBottom, text.OffsetStart, text.OffsetEnd)
            ?? Dimension(path, "size", text.Size, SizeKeywords, percent: false)
            ?? Keyword(path, "align", text.Align, Aligns)
            ?? Keyword(path, "gravity", text.Gravity, Gravities)
            ?? Dimension(path, "lineSpacing", text.LineSpacing, SpacingKeywords, percent: true)
            ?? NonNegative(path, "maxLines", text.MaxLines)
            ?? Keyword(path, "weight", text.Weight, Weights)
            ?? Color(path, "color", text.Color)
            ?? Keyword(path, "style", text.Style, TextStyles)
            ?? Keyword(path, "decoration", text.Decoration, Decorations)
            ?? Keyword(path, "adjustMode", text.AdjustMode, AdjustModes)
            ?? ActionOf(text.Action, Join(path, "action"));
    }

    private static Error? Span(FlexSpan span, string path)
    {
        if (string.IsNullOrEmpty(span.Text))
        {
            return Fail(Join(path, "text"), "span 的 text 必填。", "the text of a span is required.");
        }

        return Color(path, "color", span.Color)
            ?? Dimension(path, "size", span.Size, SizeKeywords, percent: false)
            ?? Keyword(path, "weight", span.Weight, Weights)
            ?? Keyword(path, "style", span.Style, TextStyles)
            ?? Keyword(path, "decoration", span.Decoration, Decorations);
    }

    private static Error? Image(FlexImage image, string path) =>
        Url(path, "url", image.Url)
        ?? NonNegative(path, "flex", image.Flex)
        ?? Margin(path, "margin", image.Margin)
        ?? Keyword(path, "position", image.Position, Positions)
        ?? Offsets(path, image.OffsetTop, image.OffsetBottom, image.OffsetStart, image.OffsetEnd)
        ?? Keyword(path, "align", image.Align, Aligns)
        ?? Keyword(path, "gravity", image.Gravity, Gravities)
        ?? Dimension(path, "size", image.Size, ImageSizeKeywords, percent: true)
        ?? AspectRatio(path, image.AspectRatio)
        ?? Keyword(path, "aspectMode", image.AspectMode, AspectModes)
        ?? Color(path, "backgroundColor", image.BackgroundColor)
        ?? ActionOf(image.Action, Join(path, "action"));

    private static Error? Icon(FlexIcon icon, string path) =>
        Url(path, "url", icon.Url)
        ?? Margin(path, "margin", icon.Margin)
        ?? Keyword(path, "position", icon.Position, Positions)
        ?? Offsets(path, icon.OffsetTop, icon.OffsetBottom, icon.OffsetStart, icon.OffsetEnd)
        ?? Dimension(path, "size", icon.Size, SizeKeywords, percent: false)
        ?? AspectRatio(path, icon.AspectRatio);

    private static Error? Button(FlexButton button, string path)
    {
        if (button.Action is null)
        {
            return Fail(Join(path, "action"), "button 的 action 必填。", "the action of a button is required.");
        }

        return ActionOf(button.Action, Join(path, "action"))
            ?? NonNegative(path, "flex", button.Flex)
            ?? Margin(path, "margin", button.Margin)
            ?? Keyword(path, "position", button.Position, Positions)
            ?? Offsets(path, button.OffsetTop, button.OffsetBottom, button.OffsetStart, button.OffsetEnd)
            ?? Keyword(path, "height", button.Height, ButtonHeights)
            ?? Keyword(path, "style", button.Style, ButtonStyles)
            ?? Color(path, "color", button.Color)
            ?? Keyword(path, "gravity", button.Gravity, Gravities)
            ?? Keyword(path, "adjustMode", button.AdjustMode, AdjustModes);
    }

    private static Error? Video(FlexVideo video, string path)
    {
        if (video.AltContent is null)
        {
            return Fail(Join(path, "altContent"), "video 的 altContent 必填(image 或 box)。", "the altContent of a video is required (an image or a box).");
        }

        if (video.AltContent is not (FlexImage or FlexBox or RawFlexComponent))
        {
            return Fail(
                Join(path, "altContent"),
                $"video 的 altContent 只能是 image 或 box,實際是 {video.AltContent.Type}。",
                $"the altContent of a video can only be an image or a box, but is {video.AltContent.Type}.");
        }

        return Url(path, "url", video.Url)
            ?? Url(path, "previewUrl", video.PreviewUrl)
            ?? AspectRatio(path, video.AspectRatio)
            ?? Component(video.AltContent, Join(path, "altContent"))
            ?? ActionOf(video.Action, Join(path, "action"));
    }

    private static Error? ActionOf(LineAction? action, string path)
    {
        if (action is null)
        {
            return null;
        }

        var result = action.Validate(LineMessagingLimits.MaxActionLabelLength, path);
        return result.IsFailure ? result.Error : null;
    }

    private static Error? Url(string path, string field, string? value)
    {
        var fieldPath = Join(path, field);
        if (string.IsNullOrWhiteSpace(value))
        {
            return Fail(fieldPath, $"{field} 必填。", $"{field} is required.");
        }

        return LineMessageValidation.CheckMediaUrl(fieldPath, value) is { } message
            ? Error.Validation(LineErrorCodes.InvalidFlex, message)
            : null;
    }

    private static Error? AspectRatio(string path, string? value) =>
        value is not null && !AspectRatioPattern().IsMatch(value)
            ? Fail(
                Join(path, "aspectRatio"),
                $"aspectRatio 格式必須是「寬:高」(例如 1.91:1),實際是:{LineMessageValidation.Quote(value)}。",
                $"aspectRatio must be width:height (such as 1.91:1), but is: {LineMessageValidation.Quote(value)}.")
            : null;

    private static Error? Color(string path, string field, string? value) =>
        value is not null && !ColorPattern().IsMatch(value)
            ? Fail(
                Join(path, field),
                $"{field} 必須是 #RRGGBB 或 #RRGGBBAA,實際是:{LineMessageValidation.Quote(value)}。",
                $"{field} must be #RRGGBB or #RRGGBBAA, but is: {LineMessageValidation.Quote(value)}.")
            : null;

    private static Error? RequiredColor(string path, string field, string? value) =>
        string.IsNullOrEmpty(value)
            ? Fail(Join(path, field), $"{field} 必填。", $"{field} is required.")
            : Color(path, field, value);

    private static Error? NonNegative(string path, string field, int? value) =>
        value is < 0
            ? Fail(
                Join(path, field),
                string.Create(CultureInfo.InvariantCulture, $"{field} 不可為負數,實際是 {value}。"),
                string.Create(CultureInfo.InvariantCulture, $"{field} must not be negative, but is {value}."))
            : null;

    private static Error? Keyword(string path, string field, string? value, string[] allowed) =>
        value is not null && Array.IndexOf(allowed, value) < 0
            ? Fail(
                Join(path, field),
                $"{field} 必須是 {string.Join(" / ", allowed)} 其中之一,實際是:{LineMessageValidation.Quote(value)}。",
                $"{field} must be one of {string.Join(" / ", allowed)}, but is: {LineMessageValidation.Quote(value)}.")
            : null;

    private static Error? Spacing(string path, string field, string? value) =>
        Dimension(path, field, value, SpacingKeywords, percent: false);

    private static Error? Margin(string path, string field, string? value) =>
        Dimension(path, field, value, SpacingKeywords, percent: false);

    private static Error? Offsets(string path, string? top, string? bottom, string? start, string? end) =>
        Dimension(path, "offsetTop", top, SpacingKeywords, percent: true)
        ?? Dimension(path, "offsetBottom", bottom, SpacingKeywords, percent: true)
        ?? Dimension(path, "offsetStart", start, SpacingKeywords, percent: true)
        ?? Dimension(path, "offsetEnd", end, SpacingKeywords, percent: true);

    private static Error? Dimension(string path, string field, string? value, string[] keywords, bool percent)
    {
        if (value is null
            || Array.IndexOf(keywords, value) >= 0
            || PixelPattern().IsMatch(value)
            || (percent && PercentPattern().IsMatch(value)))
        {
            return null;
        }

        var forms = new List<string>(keywords) { "{n}px" };
        if (percent)
        {
            forms.Add("{n}%");
        }

        var list = string.Join(" / ", forms);
        return Fail(
            Join(path, field),
            $"{field} 必須是 {list} 其中之一,實際是:{LineMessageValidation.Quote(value)}。",
            $"{field} must be one of {list}, but is: {LineMessageValidation.Quote(value)}.");
    }

    private static string Join(string path, string name) => path.Length == 0 ? name : string.Concat(path, ".", name);

    private static string Index(string path, int index) => string.Create(CultureInfo.InvariantCulture, $"{path}[{index}]");

    private static Error Fail(string path, string chinese, string english)
    {
        var where = path.Length == 0 ? "(根)" : path;
        var whereEnglish = path.Length == 0 ? "(root)" : path;
        return Error.Validation(
            LineErrorCodes.InvalidFlex,
            $"Flex 版面 {where}:{chinese} Flex layout {whereEnglish}: {english}");
    }
}
