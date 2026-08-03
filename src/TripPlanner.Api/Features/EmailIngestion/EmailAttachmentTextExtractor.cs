using System.Text;
using System.Text.RegularExpressions;
using TripPlanner.Contracts.EmailIngestion;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>Why an attachment could or could not be decoded.</summary>
public enum AttachmentDecodeOutcome
{
    Decoded,
    InvalidBase64,
    TooLarge
}

/// <summary>A decoded attachment plus any text recovered from it.</summary>
public sealed record DecodedAttachment(
    AttachmentDecodeOutcome Outcome,
    string FileName,
    string ContentType,
    byte[] Content,
    string? ExtractedText);

/// <summary>
/// Decodes relayed attachments and recovers text from the content types that carry it.
/// Binary formats (PDF, images, Office documents) are retained byte-for-byte but contribute
/// no text — extracting from them is deliberately out of scope.
/// </summary>
public sealed partial class EmailAttachmentTextExtractor
{
    /// <summary>Maximum decoded size of a single attachment.</summary>
    public const int MaxAttachmentBytes = 5 * 1024 * 1024;

    /// <summary>Maximum combined decoded size of one relayed request.</summary>
    public const int MaxRequestBytes = 10 * 1024 * 1024;

    public DecodedAttachment Decode(RelayEmailAttachment attachment)
    {
        var fileName = attachment.FileName?.Trim() ?? string.Empty;
        var contentType = attachment.ContentType?.Trim() ?? string.Empty;

        // Reject on the encoded length first so an oversized payload is never materialized.
        var encodedLength = attachment.ContentBase64?.Length ?? 0;
        if (EstimateDecodedLength(encodedLength) > MaxAttachmentBytes)
        {
            return new DecodedAttachment(AttachmentDecodeOutcome.TooLarge, fileName, contentType, [], null);
        }

        byte[] content;
        try
        {
            content = Convert.FromBase64String(attachment.ContentBase64 ?? string.Empty);
        }
        catch (FormatException)
        {
            return new DecodedAttachment(AttachmentDecodeOutcome.InvalidBase64, fileName, contentType, [], null);
        }

        if (content.Length > MaxAttachmentBytes)
        {
            return new DecodedAttachment(AttachmentDecodeOutcome.TooLarge, fileName, contentType, [], null);
        }

        var text = IsTextBearing(contentType) ? DecodeText(content, contentType) : null;
        return new DecodedAttachment(AttachmentDecodeOutcome.Decoded, fileName, contentType, content, text);
    }

    /// <summary>True for content types whose bytes are readable as text without a format-specific parser.</summary>
    public static bool IsTextBearing(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var media = contentType.Split(';')[0].Trim().ToLowerInvariant();
        if (media.StartsWith("text/", StringComparison.Ordinal))
        {
            return true;
        }

        return media is "application/json" or "application/xml" or "application/x-ndjson"
            || media.EndsWith("+json", StringComparison.Ordinal)
            || media.EndsWith("+xml", StringComparison.Ordinal);
    }

    /// <summary>Strips markup so recognition sees prose rather than tags.</summary>
    public static string StripMarkup(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var withoutScripts = ScriptOrStyleBlock().Replace(html, " ");
        var withoutTags = HtmlTag().Replace(withoutScripts, " ");
        var decoded = System.Net.WebUtility.HtmlDecode(withoutTags);
        return CollapseWhitespace().Replace(decoded, " ").Trim();
    }

    private static string? DecodeText(byte[] content, string contentType)
    {
        if (content.Length == 0)
        {
            return null;
        }

        var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false).GetString(content);
        var media = contentType.Split(';')[0].Trim().ToLowerInvariant();
        if (media is "text/html" or "application/xhtml+xml")
        {
            text = StripMarkup(text);
        }

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static long EstimateDecodedLength(int base64Length) => base64Length / 4L * 3L;

    [GeneratedRegex("<(script|style)[^>]*>.*?</\\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptOrStyleBlock();

    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex HtmlTag();

    [GeneratedRegex("\\s+")]
    private static partial Regex CollapseWhitespace();
}
