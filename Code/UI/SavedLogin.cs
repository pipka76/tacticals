using Godot;

/// <summary>
/// "Remember me" credentials, encrypted with this machine's id (user://login.cfg).
/// The password is stored rather than the login token because the API's tokens don't survive its restarts.
/// </summary>
public static class SavedLogin
{
    private const string FilePath = "user://login.cfg";
    private const string Section = "login";

    public static bool TryLoad(out string email, out string password)
    {
        email = password = null;
        var config = new ConfigFile();
        if (config.LoadEncryptedPass(FilePath, OS.GetUniqueId()) != Error.Ok)
            return false;

        email = (string)config.GetValue(Section, "email", "");
        password = (string)config.GetValue(Section, "password", "");
        return email != "" && password != "";
    }

    public static void Save(string email, string password)
    {
        var config = new ConfigFile();
        config.SetValue(Section, "email", email);
        config.SetValue(Section, "password", password);
        var err = config.SaveEncryptedPass(FilePath, OS.GetUniqueId());
        if (err != Error.Ok)
            GD.Print($"SavedLogin.Save: {err}");
    }

    public static void Clear()
    {
        if (FileAccess.FileExists(FilePath))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(FilePath));
    }
}
