namespace SteveTheTroll.Core.Results
{
    /// <summary>
    /// Architecture V2 (D-169): the answer of one validation step of a pipeline. Valid lets the pipeline go on; otherwise it stops with Rejected (a rule refused) or Ignored
    /// (does not apply now). Readonly struct: no allocation per check.
    /// </summary>
    internal readonly struct ValidationResult
    {
        /// Success = valid; Rejected / Ignored = the pipeline stops with this status.
        internal ResponseStatus Status { get; }
        internal string Reason { get; }

        ValidationResult(ResponseStatus status, string reason)
        {
            Status = status;
            Reason = reason ?? "";
        }

        internal bool IsValid { get { return Status == ResponseStatus.Success; } }

        internal static readonly ValidationResult Valid = new ValidationResult(ResponseStatus.Success, "");
        internal static ValidationResult Reject(string reason) { return new ValidationResult(ResponseStatus.Rejected, reason); }
        internal static ValidationResult Ignore(string reason) { return new ValidationResult(ResponseStatus.Ignored, reason); }

        /// Valid when `condition` holds, else Rejected with `reason` (no allocation when the reason is a constant).
        internal static ValidationResult RejectUnless(bool condition, string reason) { return condition ? Valid : Reject(reason); }
        /// Valid when `condition` holds, else Ignored with `reason`.
        internal static ValidationResult IgnoreUnless(bool condition, string reason) { return condition ? Valid : Ignore(reason); }

        public override string ToString() { return IsValid ? "Valid" : Status + ": " + Reason; }
    }
}
