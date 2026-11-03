namespace SteveTheTroll.Features.Trolls.Contracts;

/// <summary>
///     Architecture V2 (D-169): network ownership of the troll's object. Executors check IsOwner before they act, so a
///     client that lost the troll to another peer
///     never moves it or writes to it (ownership is a requirement of the execution boundary, not something each feature
///     has to remember).
/// </summary>
internal interface ITrollOwnership
{
    /// The troll's object exists and has a valid network view.
    bool IsValid { get; }

    /// This client owns the troll's network object now.
    bool IsOwner { get; }
}
