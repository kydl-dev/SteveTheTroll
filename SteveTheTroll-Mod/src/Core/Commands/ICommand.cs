namespace SteveTheTroll.Core.Commands
{
    /// <summary>
    /// Architecture V2 (D-169): a meaningful request (tame this troll, send it home, farm this) whose handler answers with an OperationResponse of TResult.
    /// Internal calculations stay plain methods; only requests become commands. Commands are immutable data.
    /// </summary>
    internal interface ICommand<TResult>
    {
    }
}
