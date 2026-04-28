namespace Fleece.Core.Models.Graph;

/// <summary>
/// A logical (row, lane) coordinate within a graph layout.
/// </summary>
public readonly record struct GridPosition(int Row, int Lane);
