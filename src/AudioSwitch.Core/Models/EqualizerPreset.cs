namespace AudioSwitch.Core.Models;

public sealed record EqualizerPreset(string Category, string Name, double[] Gains);
