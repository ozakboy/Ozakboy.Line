namespace Ozakboy.Line.Messaging;

/// <summary>
/// 影片或音訊訊息內容的轉檔狀態。
/// The transcoding status of a video or audio message's content.
/// </summary>
public enum LineContentTranscodingStatus
{
    /// <summary>
    /// 轉檔中,內容還不能下載。
    /// Still transcoding; the content cannot be downloaded yet.
    /// </summary>
    Processing,

    /// <summary>
    /// 轉檔完成,可以下載內容。
    /// Transcoding finished; the content can be downloaded.
    /// </summary>
    Succeeded,

    /// <summary>
    /// 轉檔失敗,內容無法下載。
    /// Transcoding failed; the content cannot be downloaded.
    /// </summary>
    Failed,
}
