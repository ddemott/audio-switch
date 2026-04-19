namespace AudioSwitch.Core.Models;

public sealed class ComponentLibrary
{
    public List<OutputDeviceComponent> Outputs { get; set; } = new();

    public List<InputDeviceComponent> Inputs { get; set; } = new();

    public List<SpatialAudioComponent> SpatialModes { get; set; } = new();

    public List<EqualizerComponent> Equalizers { get; set; } = new();

    public IEnumerable<Component> All
    {
        get
        {
            foreach (var c in Outputs) yield return c;
            foreach (var c in Inputs) yield return c;
            foreach (var c in SpatialModes) yield return c;
            foreach (var c in Equalizers) yield return c;
        }
    }

    public Component? FindById(string id) =>
        All.FirstOrDefault(c => c.Id == id);

    public bool Add(Component component)
    {
        if (FindById(component.Id) is not null)
        {
            return false;
        }
        switch (component)
        {
            case OutputDeviceComponent o: Outputs.Add(o); return true;
            case InputDeviceComponent i: Inputs.Add(i); return true;
            case SpatialAudioComponent s: SpatialModes.Add(s); return true;
            case EqualizerComponent e: Equalizers.Add(e); return true;
            default: return false;
        }
    }

    public bool Remove(string id)
    {
        return Outputs.RemoveAll(c => c.Id == id) > 0
            || Inputs.RemoveAll(c => c.Id == id) > 0
            || SpatialModes.RemoveAll(c => c.Id == id) > 0
            || Equalizers.RemoveAll(c => c.Id == id) > 0;
    }
}
