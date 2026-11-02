using System;
using SteveTheTroll.Core.Pipeline;
using SteveTheTroll.Core.Results;
using SteveTheTroll.Trolls.Orders.Domain;

namespace SteveTheTroll.Trolls.Orders.Application
{
    /// <summary>
    /// D-170, orders slice: OrderInput -> validation (key pressed, player alive, no menu open: else Ignored; an own troll in range, this client can steer it: else Rejected)
    /// -> OrderRules -> OperationResponse of OrderKind. Runs every frame (the key check is the first step), allocates nothing. Game-free.
    /// </summary>
    internal sealed class OrderPipeline
    {
        internal const string NoKey = "attack key not pressed", Dead = "no living player", Blocked = "a menu, chat or the console is open",
            NoTroll = "no own tamed troll within the order range", NoSteer = "the troll cannot be steered from this client";

        readonly OperationPipeline<OrderInput, OrderKind> _pipeline;

        internal OrderPipeline(Action<string> trace = null)
        {
            _pipeline = new OperationPipeline<OrderInput, OrderKind>("OrderPipeline", i => OperationResponse<OrderKind>.Success(OrderRules.Decide(i)))
                .Validate("key", i => ValidationResult.IgnoreUnless(i.KeyPressed, NoKey))
                .Validate("player", i => ValidationResult.IgnoreUnless(i.PlayerAlive, Dead))
                .Validate("ui", i => ValidationResult.IgnoreUnless(!i.UiBlocked, Blocked))
                .Validate("troll", i => ValidationResult.RejectUnless(i.HasTroll, NoTroll))
                .Validate("steer", i => ValidationResult.RejectUnless(i.CanSteer, NoSteer));
            _pipeline.Trace = trace;
        }

        /// Expected outcome: Success with the order; Ignored without a key press / player / with a menu open; Rejected without a steerable own troll.
        internal OperationResponse<OrderKind> Evaluate(OrderInput input) { return _pipeline.Run(input); }
    }
}
