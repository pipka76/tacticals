using Godot;

namespace tacticals.Code.Game;

[GlobalClass]
public partial class SoundEntry : Resource
{
    [Export] public StringName Id;
    [Export] public SoundEvent Event;
}