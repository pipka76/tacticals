using System.Runtime.InteropServices;
using Godot;

public class River
{
    public Vector2I Start;
    public Vector2I End;

    public void Draw(MapBlock[][] mm)
    {
        if (Start.X == 0)
        {
            int lastY;
            var step =  (float)(End.Y - Start.Y) / mm.Length;
            float currentY = Start.Y;
            lastY = (int)currentY;
            
            for (int i = Start.X; i <= End.X; i++)
            {
                if(lastY != (int)currentY)
                    mm[i][lastY].BlockType = MapBlockType.RIVER;
                mm[i][(int)currentY].BlockType = MapBlockType.RIVER;
                lastY = (int)currentY;
                currentY += step;
            }
            
            return;
        }

        if (Start.Y == 0)
        {
            int lastX;
            var step =  (float)(End.X - Start.X) / mm[0].Length;
            float currentX = Start.X;
            lastX = (int)currentX;
            
            for (int j = Start.Y; j <= End.Y; j++)
            {
                if(lastX != (int)currentX)
                    mm[lastX][j].BlockType = MapBlockType.RIVER;
                mm[(int)currentX][j].BlockType = MapBlockType.RIVER;
                lastX = (int)currentX;
                currentX += step;
            }
            
            return;
        }
    }
}