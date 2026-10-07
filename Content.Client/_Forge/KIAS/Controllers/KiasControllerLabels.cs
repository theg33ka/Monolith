using System.Linq;
using Content.Shared._Forge.KIAS.Controllers;

namespace Content.Client._Forge.KIAS.Controllers;

public static class KiasControllerLabels
{
    public static string Port(KiasGraphPort port) => Loc.TryGetString(port.Name, out var name) ? name : port.Id;
    public static string Profile(string id, IEnumerable<KiasGraphPort> ports)
    {
        if (Loc.TryGetString($"kias-controller-profile-{id.ToLowerInvariant()}", out var name)) return name;
        return Loc.GetString("kias-controller-native-profile") + ": "
            + string.Join(" / ", ports.Where(port => !port.Id.StartsWith('$')).Take(2).Select(Port));
    }
    public static string Error(string error)
    {
        var parts = error.Split(':', 2);
        if (!Loc.TryGetString($"kias-controller-error-{parts[0]}", out var description)) return error;
        return parts.Length > 1 ? $"{description} ({parts[1]})" : description;
    }
}
