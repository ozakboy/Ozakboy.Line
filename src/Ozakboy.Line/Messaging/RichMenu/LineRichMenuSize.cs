using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.RichMenu;

/// <summary>
/// 圖文選單的尺寸。
/// A rich menu's size.
/// </summary>
/// <remarks>
/// LINE 只接受兩種尺寸:2500×1686(大)與 2500×843(小)。上傳的圖片尺寸必須與這裡宣告的完全相同,
/// 不同時 LINE 在上傳圖片那一步才拒絕,而不是在建立選單時 —— 兩個步驟相隔一次 API 呼叫,
/// 錯誤訊息也不會提到是尺寸不符。
/// LINE accepts two sizes only: 2500×1686 (large) and 2500×843 (compact). The uploaded image must match what is
/// declared here exactly; a mismatch is refused at the image upload rather than at menu creation — one API call
/// later — and the error does not mention the size.
/// </remarks>
public sealed class LineRichMenuSize
{
    /// <summary>
    /// 寬度,預設 2500。
    /// The width; 2500 by default.
    /// </summary>
    [JsonPropertyName("width")]
    public int Width { get; set; } = 2500;

    /// <summary>
    /// 高度,預設 1686。
    /// The height; 1686 by default.
    /// </summary>
    [JsonPropertyName("height")]
    public int Height { get; set; } = 1686;
}
