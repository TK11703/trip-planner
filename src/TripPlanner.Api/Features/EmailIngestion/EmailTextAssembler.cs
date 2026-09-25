using System.Text;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>
/// Assembles the text recognition reads from a message: subject, body, then any text recovered
/// from attachments.
///
/// Lifted verbatim from <see cref="RelayMessageProcessor"/> so both callers — first ingestion and
/// the re-recognition of a draft that predates transport recognition — provably feed the
/// recognizer the same shape. A second copy would let the two drift, and a draft would then be
/// re-examined against text subtly unlike the text it was first recognized from.
/// </summary>
internal static class EmailTextAssembler
{
    public static string Assemble(string subject, string? bodyText, string[] attachmentText)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(subject))
        {
            builder.Append("Subject: ").AppendLine(subject).AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(bodyText))
        {
            builder.AppendLine(bodyText).AppendLine();
        }

        for (var i = 0; i < attachmentText.Length; i++)
        {
            builder.AppendLine($"--- Attachment {i + 1} ---").AppendLine(attachmentText[i]).AppendLine();
        }

        return builder.ToString().Trim();
    }
}
