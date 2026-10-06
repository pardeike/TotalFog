using Verse;

namespace RimWorldRealFoW;

public abstract class ThingSubComp
{
    public CompMainComponent mainComponent;

    public ThingWithComps parent;

    public virtual void CompTick()
    {
    }

    public virtual void CompTickRare()
    {
    }

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