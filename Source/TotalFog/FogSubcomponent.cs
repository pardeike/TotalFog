using Verse;

namespace TotalFog;

public abstract class FogSubcomponent
{
    public CompFog mainComponent;

    public ThingWithComps parent;

    public virtual void CompTick()
    {
    }

    public virtual void CompTickRare() => CompTick();

    public virtual void PostDeSpawn(Map map)
    {
    }

    public virtual void PostExposeData()
    {
    }

    public virtual void PostSpawnSetup(bool respawningAfterLoad)
    {
    }

    public virtual void ReceiveCompSignal(string signal)
    {
    }
}