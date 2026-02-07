using Godot;
using Godot.Collections;

namespace tacticals.Code.Game;

[GlobalClass]
public partial class SoundEvent : Resource
{
    [Export] public Array<AudioStream> Variations = new();
    [Export] public string Bus = "SFX";

    [Export] public float VolumeDb = 0f;
    [Export] public float PitchMin = 1.0f;
    [Export] public float PitchMax = 1.0f;

    // Optional knobs you’ll likely want eventually:
    [Export] public float CooldownSeconds = 0.0f; // prevent spam
    [Export] public int MaxSimultaneous = 0;      // 0 = unlimited (you can enforce in manager)

}
