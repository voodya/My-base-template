using System;

/// <summary>
/// Settings of a clicker round. Default instance is registered by ClickerServiceInstaller
/// via RegisterDefaultInstance, a state transition can override it with a payload:
/// TryChangeScope&lt;ClickerEntryPoint&gt;(new ClickerSettings { RoundDuration = 5f })
/// </summary>
[Serializable]
public class ClickerSettings
{
    public float RoundDuration = 10f;
}
