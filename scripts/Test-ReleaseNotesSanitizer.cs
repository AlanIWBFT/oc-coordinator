#!/usr/bin/env dotnet
#:property PublishAot=false
#:package Markdig@1.3.2
#:include ReleaseNotesSanitizer.cs

using LocalFork;

var fencedCode = "~~~text\r\n@inside-fence\r\n~~~~";
var mathBlock = "$$\r\n@inside-math\r\n$$";
var successCases = new (string Name, string Input, string Expected)[]
{
    ("plain mentions", "Thanks @alice and @org/team.", "Thanks alice and org/team."),
    ("inline code", "Run `@opencode-ai/sdk`, then notify @alice.", "Run `@opencode-ai/sdk`, then notify alice."),
    ("long closing fence", fencedCode, fencedCode),
    ("indented code", "    @inside-indented-code", "    @inside-indented-code"),
    ("balanced link", "[docs](relative_(part)/@destination) and @alice", "[docs](relative_(part)/@destination) and alice"),
    ("angle link", "[docs](<relative)/@destination>) and @alice", "[docs](<relative)/@destination>) and alice"),
    ("invalid link lookalike", "Changed ](owner @alice)", "Changed ](owner alice)"),
    ("explicit link label", "[@alice](https://github.com/alice) and @bob", "[@alice](https://github.com/alice) and bob"),
    ("image alt", "![@alice](https://example.com/image.png) and @bob", "![@alice](https://example.com/image.png) and bob"),
    ("reference link", "[docs][ref] and @alice\r\n\r\n[ref]: https://example.com/@destination", "[docs][ref] and alice\r\n\r\n[ref]: https://example.com/@destination"),
    ("link title", "[docs](relative \"Owner) @alice\") and @bob", "[docs](relative \"Owner) @alice\") and bob"),
    ("commonmark autolink", "<https://example.com/@alice> and @bob", "<https://example.com/@alice> and bob"),
    ("commonmark tel autolink", "<tel:@alice> and @bob", "<tel:@alice> and bob"),
    ("tel text", "tel:@alice and @bob", "tel:alice and bob"),
    ("embedded www text", "prefixwww.example.com/@alice and @bob", "prefixwww.example.com/alice and bob"),
    ("Unicode embedded www text", "\u524D\u7F00www.example.com/@alice and @bob", "\u524D\u7F00www.example.com/alice and bob"),
    ("raw HTML without marker", "<strong>release notes</strong>", "<strong>release notes</strong>"),
    ("math without marker", "Formula $x + y$", "Formula $x + y$"),
    ("email and git", "alice@example.com git@github.com @bob", "alice@example.com git@github.com bob"),
    ("escaped mention", "\\@alice and @bob", "\\alice and bob"),
    ("underscore-prefixed mention", "_@alice and @bob", "_alice and bob"),
    ("repeated marker mention", "@@alice and @bob", "alice and bob"),
    ("embedded repeated marker", "user@@alice and @bob", "user@alice and bob"),
    ("encoded marker in code", "`&commat;alice`", "`&commat;alice`"),
    ("encoded marker in link", "[&commat;alice](https://github.com/alice)", "[&commat;alice](https://github.com/alice)"),
};

var rejectedCases = new (string Name, string Input, string ErrorContains)[]
{
    ("bare URL", "See https://example.com/users/@alice", "URL that Markdig does not recognize"),
    ("FTP URL", "See ftp://example.com/@alice", "URL that Markdig does not recognize"),
    ("standalone www URL", "See www.example.com/@alice", "URL that Markdig does not recognize"),
    ("uppercase bare URL", "See HTTPS://example.com/users/@alice", "URL that Markdig does not recognize"),
    ("localhost URL", "See https://localhost/@alice", "URL that Markdig does not recognize"),
    ("punctuation-prefixed URL", "See {https://example.com/@alice}", "URL that Markdig does not recognize"),
    ("digit-prefixed URL", "See 1https://example.com/@alice", "URL that Markdig does not recognize"),
    ("non-ASCII-prefixed URL", "中文https://example.com/@alice", "URL that Markdig does not recognize"),
    ("surrogate-prefixed URL", "🙂https://example.com/@alice", "URL that Markdig does not recognize"),
    ("custom-scheme URL", "ssh://example.com/@alice", "URL that Markdig does not recognize"),
    ("uppercase FTP URL", "FTP://example.com/@alice", "URL that Markdig does not recognize"),
    ("letter-prefixed scheme URL", "xhttps://example.com/@alice", "URL that Markdig does not recognize"),
    ("inline HTML code", "Use <code>@opencode-ai/sdk</code>", "raw HTML"),
    ("formatted inline HTML code", "Use <code>*@opencode-ai/sdk*</code>", "raw HTML"),
    ("HTML anchor", "<a href=\"https://github.com/alice\">@alice</a>", "raw HTML"),
    ("HTML formatting", "<strong>@alice</strong>", "raw HTML"),
    ("self-closing HTML code", "<code /> @alice", "raw HTML"),
    ("raw HTML beside mention", "<strong>release</strong> by @alice", "paragraph containing raw HTML"),
    ("raw HTML beside email", "<strong>Contact</strong> alice@example.com", "paragraph containing raw HTML"),
    ("inline math", "Formula $@alice$", "mathematics"),
    ("math block", mathBlock, "mathematics"),
    ("named at entity", "&commat;alice", "HTML-encoded @ marker"),
    ("decimal at entity", "&#64;alice", "HTML-encoded @ marker"),
    ("hex at entity", "&#x40;alice", "HTML-encoded @ marker"),
    ("encoded email", "alice&commat;example.com", "HTML-encoded @ marker"),
    ("encoded marker without mention", "&commat; package", "HTML-encoded @ marker"),
    ("encoded mention in HTML code", "<code>&commat;alice</code>", "raw HTML"),
    ("encoded mention in math", "$&commat;alice$", "mathematics"),
};

var failures = new List<string>();
foreach (var test in successCases)
{
    var actual = ReleaseNotesSanitizer.StripGitHubMentionMarkers(test.Input);
    if (actual != test.Expected)
        failures.Add($"{test.Name}\nExpected: {test.Expected}\nActual:   {actual}");
    var repeated = ReleaseNotesSanitizer.StripGitHubMentionMarkers(actual);
    if (repeated != actual)
        failures.Add($"{test.Name} is not idempotent\nFirst:  {actual}\nSecond: {repeated}");
}
foreach (var test in rejectedCases)
{
    try
    {
        var actual = ReleaseNotesSanitizer.StripGitHubMentionMarkers(test.Input);
        failures.Add($"{test.Name} should have failed\nActual: {actual}");
    }
    catch (InvalidOperationException exception) when (exception.Message.Contains(test.ErrorContains, StringComparison.Ordinal))
    {
    }
    catch (Exception exception)
    {
        failures.Add($"{test.Name} failed unexpectedly\n{exception}");
    }
}
if (failures.Count != 0) throw new InvalidOperationException(string.Join("\n\n", failures));
Console.WriteLine($"Passed {successCases.Length} sanitizer cases and {rejectedCases.Length} fail-closed cases.");
