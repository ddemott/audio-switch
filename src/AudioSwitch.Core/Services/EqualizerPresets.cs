using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Services;

// Curated EQ presets targeting common listening contexts. Gain values are
// conservative (~±6 dB max) so presets stack cleanly with source material
// and stay below clipping risk. Each preset carries exactly 10 gains lined
// up with EqualizerComponent.DefaultBands frequencies:
// 31 | 62 | 125 | 250 | 500 | 1k | 2k | 4k | 8k | 16k Hz.
public static class EqualizerPresets
{
    public const string CategoryMusic = "Music";
    public const string CategoryVideoConferencing = "Video conferencing";
    public const string CategoryGaming = "Gaming";

    public static IReadOnlyList<EqualizerPreset> All { get; } = new List<EqualizerPreset>
    {
        // ── Music ──
        new(CategoryMusic, "Flat",              new double[] {  0,  0,  0,  0,  0,  0,  0,  0,  0,  0 }),
        new(CategoryMusic, "Bass boost",        new double[] { +6, +5, +3, +1,  0,  0,  0,  0,  0,  0 }),
        new(CategoryMusic, "Treble boost",      new double[] {  0,  0,  0,  0,  0,  0, +1, +3, +5, +6 }),
        new(CategoryMusic, "Rock",              new double[] { +4, +3, +2, -1, -2, -1, +1, +2, +3, +3 }),
        new(CategoryMusic, "Pop",               new double[] { -1, +1, +2, +3, +2,  0, -1, -1, +1, +1 }),
        new(CategoryMusic, "Hip-hop",           new double[] { +6, +5, +2, +1, -1,  0, +1, +2, +3, +3 }),
        new(CategoryMusic, "Electronic / EDM",  new double[] { +5, +4, +1,  0, -2,  0, +1, +1, +3, +5 }),
        new(CategoryMusic, "Classical",         new double[] { +2, +2,  0,  0,  0,  0, -1, -1, +1, +2 }),
        new(CategoryMusic, "Jazz",              new double[] { +3, +2, +1, +2, -1, -1,  0, +1, +2, +3 }),
        new(CategoryMusic, "Vocal focus",       new double[] { -2, -1,  0, +1, +2, +3, +3, +2,  0,  0 }),

        // ── Video conferencing ──
        new(CategoryVideoConferencing, "Voice clarity (Zoom / Teams)",
                                                new double[] { -4, -3, -1,  0, +1, +2, +3, +2,  0,  0 }),
        new(CategoryVideoConferencing, "Background noise cut",
                                                new double[] { -6, -4, -2,  0,  0, +1, +1,  0, -3, -5 }),

        // ── Gaming ──
        new(CategoryGaming, "FPS — footsteps",  new double[] { -6, -4, -2, -1,  0, +1, +4, +5, +3, +1 }),
        new(CategoryGaming, "Competitive comms",new double[] { -4, -3, -1, +1, +2, +4, +3, +1,  0,  0 }),
        new(CategoryGaming, "Cinematic V-shape",new double[] { +5, +4, +2, -1, -3, -2, +1, +2, +4, +5 }),
    };

    public static List<EqualizerBand> BuildBands(EqualizerPreset preset)
    {
        var defaults = EqualizerComponent.DefaultBands();
        for (var i = 0; i < defaults.Count && i < preset.Gains.Length; i++)
        {
            defaults[i].Gain = preset.Gains[i];
        }
        return defaults;
    }
}
