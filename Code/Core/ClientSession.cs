using System;

/// <summary>
/// Client-side state of the battle the player is heading into, shared by the menus from
/// StartMenu through PrepareForBattle (Main3d.Current.Session).
/// </summary>
public class ClientSession
{
	/// <summary>Battle we created or joined; Guid.Empty while not in one.</summary>
	public Guid BattleId { get; set; }

	/// <summary>We registered the battle, so we're the one who starts it.</summary>
	public bool IsOwner { get; set; }

	/// <summary>Army grid placements chosen on StartMenu, pre-filled in PrepareForBattle (ArmyGrid.GetLayout).</summary>
	public string ArmyLayout { get; set; }

	public void LeaveBattle()
	{
		BattleId = Guid.Empty;
		IsOwner = false;
	}
}
