using Markdig;
using Markdig.Extensions.Mathematics;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Text;
using System.Text.RegularExpressions;

namespace LocalFork;

internal static class ReleaseNotesSanitizer
{
    private static readonly Regex AtMarkerPattern = new(
        @"@|&commat;|&#0*64;|&#[xX]0*40;",
        RegexOptions.CultureInvariant
    );
    private static readonly Regex GitHubMentionPattern = new(
        @"(?<![A-Za-z0-9])(?<markers>@+)(?<mention>[A-Za-z0-9](?:[A-Za-z0-9_-]{0,37}[A-Za-z0-9])?(?:/[A-Za-z0-9._-]+)?)\b",
        RegexOptions.CultureInvariant
    );
    private static readonly Regex UrlWithAtMarkerPattern = new(
        @"(?:[A-Za-z][A-Za-z0-9+.-]*://|(?<![\p{L}\p{N}\p{M}_])www\.)[^\s<>]*?(?<marker>@|&commat;|&#0*64;|&#[xX]0*40;)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    );

    public static string StripGitHubMentionMarkers(string releaseNotes)
    {
        var pipeline = new MarkdownPipelineBuilder { PreciseSourceLocation = true }
            .UseMathematics()
            .Build();
        var document = Markdown.Parse(releaseNotes, pipeline);
        var protectedRanges = FindProtectedSourceRanges(document, releaseNotes.Length);
        ValidateUnsupportedAtMarkers(releaseNotes, document, protectedRanges);
        var sourceRanges = FindMentionEligibleSourceRanges(document, releaseNotes.Length);
        var markerEdits = new List<(int Start, int Length)>();
        foreach (Match match in GitHubMentionPattern.Matches(releaseNotes))
        {
            var matchEnd = match.Index + match.Length - 1;
            if (Contains(protectedRanges, match.Index, matchEnd)) continue;
            if (!Contains(sourceRanges, match.Index, matchEnd))
                throw Unsupported(match.Index, "an unclassified Markdown context");
            markerEdits.Add((match.Groups["markers"].Index, match.Groups["markers"].Length));
        }
        if (markerEdits.Count == 0) return releaseNotes;

        var result = new StringBuilder(releaseNotes);
        foreach (var edit in markerEdits.OrderByDescending(edit => edit.Start)) result.Remove(edit.Start, edit.Length);
        return result.ToString();
    }

    private static SourceSpan[] FindProtectedSourceRanges(MarkdownDocument document, int sourceLength)
    {
        var ranges = new List<SourceSpan>();
        foreach (var node in document.Descendants())
        {
            if (node is MathBlock or MathInline) continue;
            if (node is not (CodeBlock or CodeInline or LinkInline or AutolinkInline or LinkReferenceDefinition)) continue;
            if (IsValid(node.Span, sourceLength)) ranges.Add(node.Span);
        }
        return MergeRanges(ranges);
    }

    private static SourceSpan[] FindMentionEligibleSourceRanges(MarkdownDocument document, int sourceLength)
    {
        var ranges = new List<SourceSpan>();
        foreach (var literal in document.Descendants<LiteralInline>().OrderBy(literal => literal.Span.Start))
        {
            if (IsValid(literal.Span, sourceLength) && !IsInsideLink(literal)) ranges.Add(literal.Span);
        }
        return MergeRanges(ranges);
    }

    private static void ValidateUnsupportedAtMarkers(string releaseNotes, MarkdownDocument document, SourceSpan[] protectedRanges)
    {
        foreach (var math in document.Descendants().Where(node => node is MathBlock or MathInline))
        {
            if (IsValid(math.Span, releaseNotes.Length) && ContainsAtMarker(releaseNotes, math.Span))
                throw Unsupported(math.Span.Start, "mathematics");
        }

        foreach (var htmlBlock in document.Descendants<HtmlBlock>())
        {
            if (IsValid(htmlBlock.Span, releaseNotes.Length) && ContainsAtMarker(releaseNotes, htmlBlock.Span))
                throw Unsupported(htmlBlock.Span.Start, "raw HTML");
        }
        foreach (var leaf in document.Descendants<LeafBlock>())
        {
            if (leaf is HtmlBlock || leaf.Inline is null || !leaf.Inline.Descendants<HtmlInline>().Any()) continue;
            if (IsValid(leaf.Span, releaseNotes.Length) && ContainsAtMarker(releaseNotes, leaf.Span))
                throw Unsupported(leaf.Span.Start, "a paragraph containing raw HTML");
        }

        foreach (var entity in document.Descendants<HtmlEntityInline>())
        {
            if (entity.Transcoded.ToString() != "@" || Contains(protectedRanges, entity.Span.Start, entity.Span.End)) continue;
            throw Unsupported(entity.Span.Start, "an HTML-encoded @ marker");
        }

        foreach (Match match in UrlWithAtMarkerPattern.Matches(releaseNotes))
        {
            var marker = match.Groups["marker"];
            if (!Contains(protectedRanges, marker.Index, marker.Index + marker.Length - 1))
                throw Unsupported(match.Index, "a URL that Markdig does not recognize as a link");
        }
    }

    private static bool IsInsideLink(Inline inline)
    {
        for (var parent = inline.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is LinkInline) return true;
        }
        return false;
    }

    private static bool ContainsAtMarker(string source, SourceSpan span) => AtMarkerPattern.IsMatch(source.Substring(span.Start, span.End - span.Start + 1));

    private static bool Contains(SourceSpan[] ranges, int start, int end) => ranges.Any(range => start >= range.Start && end <= range.End);

    private static bool IsValid(SourceSpan span, int sourceLength) => span.Start >= 0 && span.End >= span.Start && span.End < sourceLength;

    private static SourceSpan[] MergeRanges(IEnumerable<SourceSpan> sourceRanges)
    {
        var merged = new List<SourceSpan>();
        foreach (var span in sourceRanges.OrderBy(span => span.Start))
        {
            if (merged.Count == 0 || merged[^1].End + 1 < span.Start)
            {
                merged.Add(span);
                continue;
            }
            var previous = merged[^1];
            if (span.End > previous.End) previous.End = span.End;
            merged[^1] = previous;
        }
        return merged.ToArray();
    }

    private static InvalidOperationException Unsupported(int offset, string context) => new(
        $"Release notes contain a potentially active @ marker in {context} at source offset {offset}. "
        + "Only Markdig-recognized code and links may retain @ markers; rewrite the upstream note before creating the Draft."
    );
}
