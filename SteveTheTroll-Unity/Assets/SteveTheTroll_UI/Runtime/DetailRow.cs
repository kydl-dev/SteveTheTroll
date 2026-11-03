namespace SteveTheTroll.Ui;

/// <summary>
///     One line of a DetailsView block: a label and its value at quality 1; MaxValue is the value at the item's max
///     quality and is empty when
///     the value does not change with quality (from WhipItUp, shared through the mod template). Plain data, no Unity /
///     Valheim types.
/// </summary>
internal sealed class DetailRow
{
    internal readonly string Label;
    internal readonly string MaxValue;
    internal readonly string Value;

    internal DetailRow(string label, string value, string maxValue = "")
    {
        Label = label ?? "";
        Value = value ?? "";
        MaxValue = maxValue ?? "";
    }

    internal bool HasMax => MaxValue.Length > 0;

    public override string ToString()
    {
        return Label + " = " + Value + (HasMax ? " -> " + MaxValue : "");
    }
}
