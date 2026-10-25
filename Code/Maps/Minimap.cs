using Godot;
using System;
using tacticals.Code.Maps.Generators;

public partial class Minimap : Node2D
{
	private Sprite2D _texture;
	private Sprite2D _foresttexture;
	private const int PixelSize = 4;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_texture = GetNode<Sprite2D>("MinimapTexture");
		_foresttexture= GetNode<Sprite2D>("ForestMap");
	}

	/// <summary>Minimap picture of a map; also shown in PrepareForBattle before the level is built.</summary>
	public static Image CreateImage(MapBlock[][] map)
	{
		// Create an image
		var img = Image.CreateEmpty(map.Length * PixelSize, map[0].Length * PixelSize, false, Image.Format.Rgba8);
		img.Fill(Colors.Transparent); // Optional

		for (int i = 0; i < map.Length; i++)
		{
			for (int j = 0; j < map[0].Length; j++)
			{
				switch (map[i][j].BlockType)
				{
					case MapBlockType.PLAIN:
						img.FillRect(new Rect2I(map[i][j].Coordinates * PixelSize, PixelSize, PixelSize), new Color("green"));
						break;
					case MapBlockType.RIVER:
						img.FillRect(new Rect2I(map[i][j].Coordinates * PixelSize, PixelSize, PixelSize), new Color("blue"));
						break;
					default:
						break;
				}

                if (map[i][j].BiomeInfo != null && map[i][j].BiomeInfo.Count > 0)
                {
                    img.FillRect(new Rect2I(map[i][j].Coordinates * PixelSize, PixelSize, PixelSize), new Color("darkgreen"));
                }
    
				// switch (map[i][j].StructureType)
				// {
				// 	case MapBlockStructureType.BASE:
				// 		img.FillRect(new Rect2I(map[i][j].Coordinates * PixelSize, PixelSize, PixelSize), new Color("brown"));
				// 		break;
    //                 case MapBlockStructureType.TANK:
    //                     img.FillRect(new Rect2I(map[i][j].Coordinates * PixelSize, PixelSize, PixelSize), new Color("orange"));
    //                     break;
    //                 case MapBlockStructureType.TOWER:
    //                     img.FillRect(new Rect2I(map[i][j].Coordinates * PixelSize, PixelSize, PixelSize), new Color("red"));
    //                     break;
    //                 case MapBlockStructureType.BUNKER:
    //                     img.FillRect(new Rect2I(map[i][j].Coordinates * PixelSize, PixelSize, PixelSize), new Color("purple"));
    //                     break;
    //             }
            }
		}

		return img;
	}

	public void Generate(MapBlock[][] map)
	{
		var img = CreateImage(map);

		// Create an ImageTexture and set the image
		_texture = GetNode<Sprite2D>("MinimapTexture");
		_texture.Texture = ImageTexture.CreateFromImage(img);
	}
}
