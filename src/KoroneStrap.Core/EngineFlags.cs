namespace KSCSharp.Core;

/// <summary>
/// FastFlag names for engine-level presets (Global Settings > Presets > Rendering and
/// Graphics). These are real, documented Roblox-engine flags - confirmed against multiple
/// independent, current sources rather than assumed - not something invented for KSC-Sharp.
/// Pekora being Roblox-compatible, they very likely apply the same way, though that's not
/// been verified against a real Pekora install.
///
/// Worth knowing: Roblox introduced a server-side FastFlag allowlist in September 2025 that
/// silently ignores any flag not on it. FFlagDebugGraphicsPreferD3D11/OpenGL/Vulkan and
/// DFIntTaskSchedulerTargetFps are confirmed to be on that allowlist as of this writing - but
/// that allowlist is a policy on official Roblox's own servers, not necessarily something
/// Pekora has adopted, especially for the older 2017/2018/2020/2021 client years this app
/// targets (which predate the allowlist entirely). This is why the Graphics API feature is
/// marked Experimental rather than presented as guaranteed to work.
/// </summary>
public static class EngineFlags
{
    public const string PreferD3D11 = "FFlagDebugGraphicsPreferD3D11";
    public const string PreferOpenGL = "FFlagDebugGraphicsPreferOpenGL";
    public const string PreferVulkan = "FFlagDebugGraphicsPreferVulkan";
    public const string DisableD3D11 = "FFlagDebugGraphicsDisableDirect3D11";

    public const string TaskSchedulerTargetFps = "DFIntTaskSchedulerTargetFps";

    /// <summary>Builds the flag set for a given graphics API choice, clearing the others.</summary>
    public static void ApplyGraphicsApi(Dictionary<string, object> flags, GraphicsApi api)
    {
        flags.Remove(PreferD3D11);
        flags.Remove(PreferOpenGL);
        flags.Remove(PreferVulkan);
        flags.Remove(DisableD3D11);

        switch (api)
        {
            case GraphicsApi.OpenGL:
                flags[DisableD3D11] = true;
                flags[PreferOpenGL] = true;
                break;
            case GraphicsApi.Vulkan:
                flags[DisableD3D11] = true;
                flags[PreferVulkan] = true;
                break;
            default:
                flags[PreferD3D11] = true;
                break;
        }
    }
}
