using System.Collections.Generic;
using System;
using NUnit.Framework;
using SteveTheTroll.Core.Commands;
using SteveTheTroll.Core.Pipeline;
using SteveTheTroll.Core.Results;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-169 Architecture V2, phase 1: the typed responses, validation results, the command dispatcher and the operation pipeline.</summary>
    [TestFixture]
    public class CoreFoundationTests
    {
        // ---- OperationResponse ----
        [Test] public void Success_CarriesTheValue()
        {
            var r = OperationResponse<int>.Success(4, "2+2");
            Assert.That(r.IsSuccess, Is.True);
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Success));
            Assert.That(r.Value, Is.EqualTo(4));
            Assert.That(r.Reason, Is.EqualTo("2+2"));
        }
        [TestCase("Rejected")] [TestCase("Ignored")] [TestCase("Failed")]
        public void NonSuccess_HasDefaultValue_AndIsNotSuccess(string kind)
        {
            var s = (ResponseStatus)Enum.Parse(typeof(ResponseStatus), kind);
            var r = s == ResponseStatus.Rejected ? OperationResponse<string>.Rejected("no") : s == ResponseStatus.Ignored ? OperationResponse<string>.Ignored("no") : OperationResponse<string>.Failed("no");
            Assert.That(r.Status, Is.EqualTo(s));
            Assert.That(r.IsSuccess, Is.False);
            Assert.That(r.Value, Is.Null);
            Assert.That(r.ToString(), Is.EqualTo(s + ": no"));
        }
        [Test] public void NullReason_BecomesEmpty() { Assert.That(new OperationResponse<int>(ResponseStatus.Failed, 0, null).Reason, Is.EqualTo("")); }
        [Test] public void DefaultResponse_IsSuccessWithEmptyReason_SoNeverReturnDefault()
        {
            // documents the trap: default(OperationResponse) reads as Success; code must always use the factories
            var r = default(OperationResponse<int>);
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Success));
            Assert.That(r.Reason, Is.Null);
        }
        [Test] public void As_KeepsStatusAndReason()
        {
            var r = OperationResponse<int>.Rejected("not owner").As<string>();
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Rejected));
            Assert.That(r.Reason, Is.EqualTo("not owner"));
            Assert.That(r.Value, Is.Null);
        }

        // ---- ValidationResult ----
        [Test] public void Validation_Helpers()
        {
            Assert.That(ValidationResult.Valid.IsValid, Is.True);
            Assert.That(ValidationResult.RejectUnless(true, "x").IsValid, Is.True);
            Assert.That(ValidationResult.RejectUnless(false, "x").Status, Is.EqualTo(ResponseStatus.Rejected));
            Assert.That(ValidationResult.IgnoreUnless(false, "x").Status, Is.EqualTo(ResponseStatus.Ignored));
            Assert.That(ValidationResult.Ignore("x").Reason, Is.EqualTo("x"));
            Assert.That(ValidationResult.Reject("y").ToString(), Is.EqualTo("Rejected: y"));
        }

        // ---- OperationPipeline ----
        static OperationPipeline<int, int> Doubler(List<string> trace = null)
        {
            var p = new OperationPipeline<int, int>("doubler", x => OperationResponse<int>.Success(x * 2))
                .Validate("not negative", x => ValidationResult.RejectUnless(x >= 0, "negative"))
                .Validate("not zero", x => ValidationResult.IgnoreUnless(x != 0, "nothing to double"));
            if (trace != null) p.Trace = trace.Add;
            return p;
        }
        [Test] public void Pipeline_AllValid_Decides() { var r = Doubler().Run(3); Assert.That(r.IsSuccess, Is.True); Assert.That(r.Value, Is.EqualTo(6)); }
        [Test] public void Pipeline_FirstInvalidStops_WithItsStatus()
        {
            var r = Doubler().Run(-1);
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Rejected));
            Assert.That(r.Reason, Is.EqualTo("negative"));
            Assert.That(Doubler().Run(0).Status, Is.EqualTo(ResponseStatus.Ignored));
        }
        [Test] public void Pipeline_StepsRunInOrder_AndLaterStepsDoNotRunAfterAStop()
        {
            var seen = new List<string>();
            var p = new OperationPipeline<int, int>("order", x => { seen.Add("decide"); return OperationResponse<int>.Success(x); })
                .Validate("a", x => { seen.Add("a"); return ValidationResult.Valid; })
                .Validate("b", x => { seen.Add("b"); return ValidationResult.Reject("stop"); })
                .Validate("c", x => { seen.Add("c"); return ValidationResult.Valid; });
            p.Run(1);
            Assert.That(seen, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(p.StepCount, Is.EqualTo(3));
        }
        [Test] public void Pipeline_TraceNamesTheStoppingStep()
        {
            var t = new List<string>();
            Doubler(t).Run(-5);
            Doubler(t).Run(2);
            Assert.That(t[0], Does.Contain("stopped at not negative"));
            Assert.That(t[1], Does.Contain("2 checks passed"));
        }
        sealed class Halver : IOperationStep<int, int> { public OperationResponse<int> Run(int x) { return x % 2 == 0 ? OperationResponse<int>.Success(x / 2) : OperationResponse<int>.Rejected("odd"); } }
        [Test] public void Pipeline_WithAStepObject() { var p = new OperationPipeline<int, int>("halver", new Halver()); Assert.That(p.Run(8).Value, Is.EqualTo(4)); Assert.That(p.Run(3).Status, Is.EqualTo(ResponseStatus.Rejected)); }
        [Test] public void Pipeline_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new OperationPipeline<int, int>("x", (Func<int, OperationResponse<int>>)null));
            Assert.Throws<ArgumentNullException>(() => new OperationPipeline<int, int>("x", (IOperationStep<int, int>)null));
            Assert.Throws<ArgumentNullException>(() => Doubler().Validate("x", null));
        }

        // ---- CommandDispatcher ----
        sealed class Add : ICommand<int> { internal readonly int A, B; internal Add(int a, int b) { A = a; B = b; } }
        sealed class Shout : ICommand<string> { internal readonly string Text; internal Shout(string t) { Text = t; } }
        sealed class Boom : ICommand<int> { }
        sealed class AddHandler : ICommandHandler<Add, int> { public OperationResponse<int> Handle(Add c) { return OperationResponse<int>.Success(c.A + c.B); } }
        sealed class ShoutHandler : ICommandHandler<Shout, string>
        {
            public OperationResponse<string> Handle(Shout c) { return string.IsNullOrEmpty(c.Text) ? OperationResponse<string>.Ignored("nothing to shout") : OperationResponse<string>.Success(c.Text.ToUpperInvariant()); }
        }
        sealed class BoomHandler : ICommandHandler<Boom, int> { public OperationResponse<int> Handle(Boom c) { throw new InvalidOperationException("kaputt"); } }

        [Test] public void Dispatcher_RoutesByCommandType()
        {
            var d = new CommandDispatcher();
            Assert.That(d.Register(new AddHandler()), Is.True);
            d.Register(new ShoutHandler());
            Assert.That(d.Count, Is.EqualTo(2));
            Assert.That(d.Dispatch(new Add(2, 2)).Value, Is.EqualTo(4));
            Assert.That(d.Dispatch(new Shout("troll")).Value, Is.EqualTo("TROLL"));
            Assert.That(d.Dispatch(new Shout("")).Status, Is.EqualTo(ResponseStatus.Ignored));
        }
        [Test] public void Dispatcher_NoHandler_Failed() { var r = new CommandDispatcher().Dispatch(new Add(1, 1)); Assert.That(r.Status, Is.EqualTo(ResponseStatus.Failed)); Assert.That(r.Reason, Does.Contain("Add")); }
        [Test] public void Dispatcher_NullCommand_Failed() { Assert.That(new CommandDispatcher().Dispatch<int>(null).Status, Is.EqualTo(ResponseStatus.Failed)); }
        [Test] public void Dispatcher_ThrowingHandler_FailedNotThrown()
        {
            var d = new CommandDispatcher();
            d.Register(new BoomHandler());
            var r = d.Dispatch(new Boom());
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Failed));
            Assert.That(r.Reason, Does.Contain("InvalidOperationException").And.Contain("kaputt"));
        }
        [Test] public void Dispatcher_ReRegister_Replaces_AndTraces()
        {
            var t = new List<string>();
            var d = new CommandDispatcher(t.Add);
            d.Register(new AddHandler());
            d.Register(new AddHandler());
            Assert.That(d.Count, Is.EqualTo(1));
            Assert.That(t[1], Does.Contain("replaced"));
            d.Dispatch(new Add(1, 2));
            Assert.That(t[2], Does.Contain("Add -> Success"));
        }
        [Test] public void Dispatcher_NullHandler_Throws() { Assert.Throws<ArgumentNullException>(() => new CommandDispatcher().Register<Add, int>(null)); }
    }
}
