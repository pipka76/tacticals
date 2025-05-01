using System;
using Godot;

public class GameUtils
{
    /// <summary>
    /// Generates a rectangular grid of Vector2 positions such that `center` is at the grid's center.
    /// </summary>
    /// <param name="center">The coordinate that will lie at the center of the grid.</param>
    /// <param name="columns">Number of points across (width).</param>
    /// <param name="rows">Number of points up (height).</param>
    /// <param name="spacing">Distance between adjacent points.</param>
    /// <returns>Flat array of Vector2 positions, length = columns * rows.</returns>
    public static Vector2[] GenerateGrid(Vector2 center, int columns, int rows, float spacing)
    {
        if (columns < 1 || rows < 1)
            throw new ArgumentException("columns and rows must both be at least 1");

        var result = new Vector2[columns * rows];

        // Calculate the offset so that the center lies in the exact middle
        // (columns - 1) * spacing is total width; half of that is how far to go left from center
        float halfWidth  = (columns - 1) * spacing * 0.5f;
        float halfHeight = (rows    - 1) * spacing * 0.5f;

        int idx = 0;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                float px = center.X - halfWidth  + x * spacing;
                float py = center.Y - halfHeight + y * spacing;
                result[idx++] = new Vector2(px, py);
            }
        }

        return result;
    }
}