using System.Collections.Generic;

public class NavigateContext
{
    public NavigateContext()
    {
        Metadata = new Dictionary<string, string>();
    }
    
    public string Command { set; get; }
    public IDictionary<string, string> Metadata { internal set; get; }
}