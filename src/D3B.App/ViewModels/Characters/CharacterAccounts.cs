using D3B.App.Controls;
using D3B.App.Localization;
using D3B.Bot.Characters;
using D3B.Storage.Accounts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace D3B.App.ViewModels.Characters;

/// <summary>What the cards and the inspector change of a character in the database: its profile, its start on connection, its account forgotten (D99, D324).</summary>
/// <remarks>
/// Each write goes off the UI's thread's way; the views show it once the book read it back. A write that fails is said in
/// a toast, and the bots go on: the app holds every client's connection (D91).
/// </remarks>
internal sealed class CharacterAccounts
{
    private readonly Localizer _localizer;
    private readonly Dialogs _dialogs;
    private readonly Toasts _toasts;

    /// <summary>Gets or sets the book, once the host runs.</summary>
    public AccountBook? Book { get; set; }

    /// <summary>Initializes a new instance of the <see cref="CharacterAccounts"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="dialogs">The window's dialogs, which ask before an account is forgotten.</param>
    /// <param name="toasts">The window's toasts, which say a write failed.</param>
    public CharacterAccounts(Localizer localizer, Dialogs dialogs, Toasts toasts)
    {
        _localizer = localizer;
        _dialogs = dialogs;
        _toasts = toasts;
    }

    /// <summary>Gives a character a profile, or none, which no longer starts on connection.</summary>
    /// <param name="key">The character.</param>
    /// <param name="profile">The profile asset's id, or <see langword="null"/>.</param>
    /// <returns>A task that completes once saved, or once the failure is said.</returns>
    public Task AssignAsync(CharacterKey key, string? profile)
    {
        return WriteAsync(book => book.AssignAsync(key, profile, CancellationToken.None));
    }

    /// <summary>Says whether a character's profile starts once its client connects.</summary>
    /// <param name="key">The character.</param>
    /// <param name="starts">Whether it starts.</param>
    /// <returns>A task that completes once saved, or once the failure is said.</returns>
    public Task SetStartsOnConnectionAsync(CharacterKey key, bool starts)
    {
        return WriteAsync(book => book.SetStartsOnConnectionAsync(key, starts, CancellationToken.None));
    }

    /// <summary>Asks whether to forget a character's account, its characters and what they kept, and forgets it once confirmed.</summary>
    /// <param name="key">The character.</param>
    public void AskForget(CharacterKey key)
    {
        var account = Book?.Accounts.FirstOrDefault(known => known.Id == key.AccountId);
        var name = account is null ? key.AccountId.ToString(_localizer.Culture) : account.Tag.Length > 0 ? $"{account.Nickname}#{account.Tag}" : account.Nickname;

        _dialogs.Ask(new QuestionViewModel(_dialogs, _localizer.Format("ForgetTitle", name), _localizer["ForgetDetail"], _localizer["ForgetConfirm"], _localizer["Cancel"], () => _ = WriteAsync(book => book.ForgetAsync(key.AccountId, CancellationToken.None)), destructive: true));
    }

    private async Task WriteAsync(Func<AccountBook, Task> write)
    {
        if (Book is not { } book)
            return;

        try
        {
            await write(book).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is DbUpdateException or SqliteException or InvalidOperationException)
        {
            _toasts.Show(new Toast(_localizer.Format("SaveFailed", exception.GetBaseException().Message), Tone.Danger));
        }
    }
}
