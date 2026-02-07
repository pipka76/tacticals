using Godot;
using Godot.Collections;

namespace tacticals.Code.Game;

public partial class AudioManager : Node
{
    [Export] public SoundLibrary Library;

    private readonly Dictionary<StringName, SoundEvent> _map = new();
    private readonly Dictionary<ulong, Node> _active = new(); // handle -> player node
    private ulong _nextHandle = 1;

    public override void _Ready()
    {
        _map.Clear();
        if (Library == null) return;

        foreach (var entry in Library.Sounds)
        {
            if (entry?.Event == null || entry.Id.IsEmpty) continue;
            if (!_map.ContainsKey(entry.Id))
                _map.Add(entry.Id, entry.Event);
        }
    }

    // --- PLAY METHODS (return handle) ---

    public SoundHandle Play3D(StringName id, Vector3 worldPos, bool loop = false)
    {
        if (!IsInsideTree())
            return default;

        if (!_map.TryGetValue(id, out var sfx) || sfx.Variations.Count == 0)
            return default;

        var stream = sfx.Variations[GD.RandRange(0, sfx.Variations.Count - 1)];
        float pitch = (sfx.PitchMin == sfx.PitchMax)
            ? sfx.PitchMin
            : (float)GD.RandRange(sfx.PitchMin, sfx.PitchMax);

        var p = new AudioStreamPlayer3D
        {
            Stream = stream,
            Bus = sfx.Bus,
            VolumeDb = sfx.VolumeDb,
            PitchScale = pitch
        };

        AddChild(p);
        p.GlobalPosition = worldPos;

        ulong handle = _nextHandle++;
        _active[handle] = p;

        if (!loop)
        {
            p.Finished += () =>
            {
                _active.Remove(handle);
                p.QueueFree();
            };
        }
        else
        {
            // Simple loop behavior: restart on Finished
            p.Finished += () => { if (_active.ContainsKey(handle)) p.Play(); };
        }

        p.Play();
        return new SoundHandle(handle);
    }

    public SoundHandle PlayUi(StringName id, bool loop = false)
    {
        if (!_map.TryGetValue(id, out var sfx) || sfx.Variations.Count == 0)
            return default;

        var stream = sfx.Variations[GD.RandRange(0, sfx.Variations.Count - 1)];
        float pitch = (sfx.PitchMin == sfx.PitchMax)
            ? sfx.PitchMin
            : (float)GD.RandRange(sfx.PitchMin, sfx.PitchMax);

        var p = new AudioStreamPlayer
        {
            Stream = stream,
            Bus = sfx.Bus,
            VolumeDb = sfx.VolumeDb,
            PitchScale = pitch
        };

        AddChild(p);

        ulong handle = _nextHandle++;
        _active[handle] = p;

        if (!loop)
        {
            p.Finished += () =>
            {
                _active.Remove(handle);
                p.QueueFree();
            };
        }
        else
        {
            p.Finished += () => { if (_active.ContainsKey(handle)) p.Play(); };
        }

        p.Play();
        return new SoundHandle(handle);
    }

    // --- STOP METHODS ---

    public bool Stop(SoundHandle handle, bool immediate = true)
    {
        if (!handle.IsValid) return false;
        if (!_active.TryGetValue(handle.Id, out var node)) return false;

        // Remove first so a Finished callback can't re-add or restart
        _active.Remove(handle.Id);

        if (node is AudioStreamPlayer asp)
        {
            if (immediate) asp.Stop();
            asp.QueueFree();
            return true;
        }

        if (node is AudioStreamPlayer2D asp2)
        {
            if (immediate) asp2.Stop();
            asp2.QueueFree();
            return true;
        }

        if (node is AudioStreamPlayer3D asp3)
        {
            if (immediate) asp3.Stop();
            asp3.QueueFree();
            return true;
        }

        node.QueueFree();
        return true;
    }

    public void StopAll()
    {
        foreach (var kv in _active)
        {
            if (kv.Value is AudioStreamPlayer asp) asp.Stop();
            else if (kv.Value is AudioStreamPlayer2D asp2) asp2.Stop();
            else if (kv.Value is AudioStreamPlayer3D asp3) asp3.Stop();
            kv.Value.QueueFree();
        }
        _active.Clear();
    }
}

public readonly struct SoundHandle
{
    public readonly ulong Id;
    public SoundHandle(ulong id) => Id = id;
    public bool IsValid => Id != 0;
}