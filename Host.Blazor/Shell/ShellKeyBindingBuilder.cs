using Client.Infrastructure.Abstractions;

namespace Host.Blazor.Shell;

public class ShellKeyBindingBuilder
{
    public record ResolvedBinding(KeyBinding Binding, string OwnerId, int Priority);

    private readonly List<(string Key, KeyBinding Binding)> _shellKeys = new();

    public void AddShellKey(string key, string label, Func<object> createMessage)
        => _shellKeys.Add((key, new KeyBinding(key, label, createMessage)));

    public Dictionary<string, ResolvedBinding> Build(
        IEnumerable<IFooterModule> visibleFooters,
        IEnumerable<IHeaderModule> visibleHeaders,
        IEnumerable<ISidebarModule> visibleSidebars,
        IEnumerable<IStageModule> activeStage)
    {
        var result = new Dictionary<string, ResolvedBinding>(StringComparer.Ordinal);

        InsertLayer(result, visibleFooters,  prio: 1);
        InsertLayer(result, visibleHeaders,  prio: 2);
        InsertLayer(result, visibleSidebars, prio: 3);
        InsertLayer(result, activeStage,     prio: 4);

        foreach (var (key, binding) in _shellKeys)
        {
            if (result.TryGetValue(key, out var existing))
                Console.WriteLine(
                    $"[Shell] \u26a0 Key-Konflikt: '{key}' " +
                    $"— Shell (Prio 5) überschreibt [{existing.OwnerId}] (Prio {existing.Priority})");
            result[key] = new ResolvedBinding(binding, "shell", 5);
        }

        return result;
    }

    private static void InsertLayer(
        Dictionary<string, ResolvedBinding> result,
        IEnumerable<IUiModule> modules, int prio)
    {
        foreach (var module in modules)
        foreach (var binding in module.KeyBindings)
        {
            if (result.TryGetValue(binding.Key, out var existing))
                Console.WriteLine(
                    $"[Shell] \u26a0 Key-Konflikt: '{binding.Key}' " +
                    $"von [{module.Id}] (Prio {prio}) überschreibt [{existing.OwnerId}] (Prio {existing.Priority})");
            result[binding.Key] = new ResolvedBinding(binding, module.Id, prio);
        }
    }
}
