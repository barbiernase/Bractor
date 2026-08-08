namespace Client.Infrastructure.Abstractions;
public record KeyBinding(string Key, string Label, Func<object> CreateMessage);
