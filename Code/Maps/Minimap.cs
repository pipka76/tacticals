using Godot;
using System;
using tacticals.Code.Maps.Generators;

public partial class Minimap : Node2D
{
	private Sprite2D _texture;
	private Sprite2D _foresttexture;
	private int PIXEL_SIZE = 4;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_texture = GetNode<Sprite2D>("MinimapTexture");
		_foresttexture= GetNode<Sprite2D>("ForestMap");
	}

	public void Generate(MapBlock[][] map)
	{
		// Create an image
		var img = Image.CreateEmpty(map.Length * PIXEL_SIZE, map[0].Length * PIXEL_SIZE, false, Image.Format.Rgba8);
		img.Fill(Colors.Transparent); // Optional

		for (int i = 0; i < map.Length; i++)
		{
			for (int j = 0; j < map[0].Length; j++)
			{
				switch (map[i][j].BlockType)
				{
					case MapBlockType.PLAIN:
						img.FillRect(new Rect2I(map[i][j].Coordinates * PIXEL_SIZE, PIXEL_SIZE, PIXEL_SIZE), new Color("green"));
						break;
					case MapBlockType.RIVER:
						img.FillRect(new Rect2I(map[i][j].Coordinates * PIXEL_SIZE, PIXEL_SIZE, PIXEL_SIZE), new Color("blue"));
						break;
					default:
						break;
				}

				switch (map[i][j].StructureType)
				{
					case MapBlockStructureType.BASE:
						img.FillRect(new Rect2I(map[i][j].Coordinates * PIXEL_SIZE, PIXEL_SIZE, PIXEL_SIZE), new Color("brown"));
						break;
                    case MapBlockStructureType.TANK:
                        img.FillRect(new Rect2I(map[i][j].Coordinates * PIXEL_SIZE, PIXEL_SIZE, PIXEL_SIZE), new Color("orange"));
                        break;
                    case MapBlockStructureType.TOWER:
                        img.FillRect(new Rect2I(map[i][j].Coordinates * PIXEL_SIZE, PIXEL_SIZE, PIXEL_SIZE), new Color("red"));
                        break;
                    case MapBlockStructureType.BUNKER:
                        img.FillRect(new Rect2I(map[i][j].Coordinates * PIXEL_SIZE, PIXEL_SIZE, PIXEL_SIZE), new Color("purple"));
                        break;
					//case MapBlockStructureType.FOREST:
					//	img.FillRect(new Rect2I(map[i][j].Coordinates * PIXEL_SIZE, PIXEL_SIZE, PIXEL_SIZE), new Color("darkgreen"));
					//	break;
                }
			}
		}

		// Create an ImageTexture and set the image
		_texture = GetNode<Sprite2D>("MinimapTexture");
		_texture.Texture = ImageTexture.CreateFromImage(img);
	}
}
