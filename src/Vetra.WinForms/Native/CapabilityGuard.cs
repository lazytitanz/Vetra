namespace Vetra.WinForms.Native;

internal static class CapabilityGuard
{
    public static void Ensure(bool enabled, string capabilityName)
    {
        if (!enabled)
        {
            throw new VetraException(
                VetraErrorCodes.CapabilityDisabled,
                $"The '{capabilityName}' capability is disabled. Enable it in VetraOptions before use.");
        }
    }
}
