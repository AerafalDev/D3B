using System.Text.RegularExpressions;
using D3B.App.Localization;
using D3B.Data;
using D3B.Data.Texts;

namespace D3B.App.ViewModels.Settings;

/// <summary>Names the NPCs, and reads the questions they ask and the replies they offer, from the pack's texts in the app's language (D385).</summary>
/// <remarks>The server sends none of them, only ids (D383). Without the pack's table or its text, a name falls back on the id.</remarks>
internal sealed partial class NpcNames
{
    private readonly Localizer _localizer;
    private readonly GameTables? _tables;
    private readonly GameTexts? _texts;

    /// <summary>Initializes a new instance of the <see cref="NpcNames"/> class.</summary>
    /// <param name="localizer">The app's texts and language.</param>
    /// <param name="tables">The pack's tables, which pair each NPC, question and reply with its text.</param>
    /// <param name="texts">The pack's texts.</param>
    public NpcNames(Localizer localizer, GameTables? tables, GameTexts? texts)
    {
        _localizer = localizer;
        _tables = tables;
        _texts = texts;
    }

    /// <summary>Gets what an NPC is called.</summary>
    /// <param name="npcId">The NPC.</param>
    /// <returns>Its name, or <c>NPC</c> and its id.</returns>
    public string Npc(int npcId)
    {
        return Read(() => _tables!.Npcs.TryGetNpc(npcId, out var npc) ? npc.NameId : null) is { } name ? name : _localizer.Format("NpcNumber", npcId);
    }

    /// <summary>Reads a reply.</summary>
    /// <param name="replyId">The reply.</param>
    /// <param name="text">What it reads, when the pack holds it.</param>
    /// <returns>Whether the pack holds its text.</returns>
    public bool TryReply(int replyId, out string text)
    {
        text = Read(() => _tables!.Npcs.TryGetReply(replyId, out var reply) ? reply.TextId : null) ?? "";

        return text.Length > 0;
    }

    /// <summary>Reads a question.</summary>
    /// <param name="questionId">The question.</param>
    /// <param name="text">What it reads, when the pack holds it.</param>
    /// <returns>Whether the pack holds its text.</returns>
    public bool TryQuestion(int questionId, out string text)
    {
        text = Read(() => _tables!.Npcs.TryGetQuestion(questionId, out var question) ? question.TextId : null) ?? "";

        return text.Length > 0;
    }

    /// <summary>Gets what a reply is called: its text, or <c>Reply</c> and its id.</summary>
    /// <param name="replyId">The reply.</param>
    /// <param name="length">The most characters kept, the rest replaced by an ellipsis; 0 keeps all.</param>
    /// <returns>The name.</returns>
    public string Reply(int replyId, int length = 0)
    {
        return TryReply(replyId, out var text) ? Clip(text, length) : _localizer.Format("RepliesReply", replyId);
    }

    /// <summary>Gets what a question is called: its text, or <c>Question</c> and its id.</summary>
    /// <param name="questionId">The question.</param>
    /// <param name="length">The most characters kept, the rest replaced by an ellipsis; 0 keeps all.</param>
    /// <returns>The name.</returns>
    public string Question(int questionId, int length = 0)
    {
        return TryQuestion(questionId, out var text) ? Clip(text, length) : _localizer.Format("RepliesQuestion", questionId);
    }

    /// <summary>Keeps the start of a text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="length">The most characters kept; 0 keeps all.</param>
    /// <returns>The text, cut at a word and ended by an ellipsis when it was longer.</returns>
    public static string Clip(string text, int length)
    {
        if (length <= 0 || text.Length <= length)
            return text;

        var cut = text.LastIndexOf(' ', length);

        return text[..(cut > length / 2 ? cut : length)].TrimEnd(',', ';', ':', ' ') + "…";
    }

    [GeneratedRegex("<[^>]*>|\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Markup();

    // The texts carry the client's markup, and line breaks a picker has no use for.
    private string? Read(Func<int?> textOf)
    {
        if (_tables is null || _texts is null)
            return null;

        try
        {
            return textOf() is { } id && _texts.TryGet(_localizer.GameLanguage, id, out var text) ? Markup().Replace(text, " ").Trim() : null;
        }
        catch (KeyNotFoundException)
        {
            // A pack of a build before the NPCs' texts holds no table: the ids name them.
            return null;
        }
    }
}
