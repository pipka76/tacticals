using Godot;

public partial class TeamEntity : Node3D
{
    private TeamObjectType objectType;
    protected Node3D _selectorObject;
    
    public bool IsSelected {
        set
        {
            if(_selectorObject == null)
                return;
            
            _selectorObject.Visible = value;
        }
        get
        {
            if(_selectorObject == null)
                return false;
            
            return _selectorObject.Visible;
        }
    }

    protected void RotateTowards(Vector3 location)
    {
        // Get current position, ignore Y difference
        Vector3 direction = location - GlobalPosition;
        direction.Y = 0; // Ignore Y axis

        if (direction.LengthSquared() == 0)
            return; // Avoid errors when target is directly above or below

        direction = direction.Normalized();

        // Calculate rotation angle on Y-axis
        float targetAngle = Mathf.Atan2(-direction.X, -direction.Z);

        Rotation = new Vector3(0, targetAngle, 0);
    }

    public void Attack(Vector3 location)
    {
        RotateTowards(location);

        // var gun = this.GetComponentInChildren<IGun>();
        // if (gun != null)
        // {
        //     gun.Fire(location);
        //
        //
        //     GameServer.Current.ReportFire(this.transform.position, (location - this.transform.position).normalized, AttackTypes.RIFLE);
        // }
    }

    public virtual TeamObjectType GetTeamObjectType()
    {
        return objectType;
    }
}