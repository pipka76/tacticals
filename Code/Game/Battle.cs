using System;
using System.Collections.Generic;

public class Battle
{
    public Guid ID;
    public string Name;
    public int Owner;
    public List<int> Peers;
    public bool IsRunning;
    public string MapName;
}