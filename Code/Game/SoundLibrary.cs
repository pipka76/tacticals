using Godot;
using Godot.Collections;

namespace tacticals.Code.Game;

[GlobalClass]
public partial class SoundLibrary : Resource
{
    [Export] public Array<SoundEntry> Sounds = new();
}