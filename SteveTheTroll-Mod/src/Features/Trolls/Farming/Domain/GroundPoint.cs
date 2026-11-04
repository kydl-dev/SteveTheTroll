namespace SteveTheTroll.Features.Trolls.Farming.Domain;

/// <summary>A point, or a direction, on the ground plane (world X and Z). Game-free.</summary>
internal readonly struct GroundPoint
{
    internal GroundPoint(float x, float z)
    {
        X = x;
        Z = z;
    }

    internal float X { get; }
    internal float Z { get; }
}