using Microsoft.Extensions.Logging;

namespace D3B.Bot.Transfers;

/// <summary>The transfer's log messages, each naming its bot by its character's id.</summary>
internal static partial class TransferLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: took {Quantity} of item {Item} out of the {Place}")]
    public static partial void Took(ILogger logger, long character, int item, int quantity, string place);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: put {Quantity} of item {Item} in the {Place}")]
    public static partial void Put(ILogger logger, long character, int item, int quantity, string place);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: gives up item {Item} for the transfer, {Outcome}")]
    public static partial void GivenUp(ILogger logger, long character, int item, string outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: would move {Quantity} of item {Item} out of the {Place}")]
    public static partial void Would(ILogger logger, long character, int item, long quantity, string place);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: transfer over, {Quantity} objects of {Items} items moved in {Rounds} rounds, {GivenUp} items given up")]
    public static partial void Over(ILogger logger, long character, long quantity, int items, int rounds, int givenUp);
}
