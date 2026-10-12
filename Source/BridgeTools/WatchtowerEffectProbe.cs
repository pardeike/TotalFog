using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HarmonyLib;
using RimWorld;
using TotalFog.Watchtowers;
using UnityEngine;
using Verse;

namespace TotalFog.BridgeTools;

/// <summary>Bounded native particle receipts. No production result is replaced.</summary>
internal static class WatchtowerEffectProbe
{
    private static bool collecting;
    private static readonly List<FleckStatic> born = new();
    private static readonly List<Mote> motes = new();
    private static Pawn actor;
    private static int movedFlecks,
        drawnFlecks,
        drawnMotes;
    private static bool invalidMotion;
    private static CompShield shield;
    private static int shieldDraws;
    private static readonly List<object> geometry = new();

    internal static object Receipt =>
        new
        {
            movedFlecks,
            drawnFlecks,
            drawnMotes,
            invalidMotion,
            shieldDraws,
            geometry = geometry.ToArray(),
            looseMotes = motes.Select(m => m.def.defName).ToArray(),
            moteState = motes
                .Select(m => new
                {
                    m.def.defName,
                    m.Spawned,
                    m.Destroyed,
                    m.Alpha,
                    native = m.exactPosition.ToString(),
                    drawn = m.DrawPos.ToString(),
                    bonus = TowerEffects.MoteOffset(m).ToString(),
                    graphic = m.Graphic.GetType().FullName,
                    visible = Visibility.IsVisible(m),
                })
                .ToArray(),
        };

    internal static void Install(Harmony probe)
    {
        void Observe(Type type, string method, string observer) =>
            probe.Patch(
                AccessTools.Method(type, method),
                postfix: new HarmonyMethod(typeof(WatchtowerEffectProbe), observer)
                {
                    priority = Priority.Last,
                }
            );
        Observe(typeof(FleckStatic), nameof(FleckStatic.Setup), nameof(Born));
        Observe(typeof(ThingMaker), nameof(ThingMaker.MakeThing), nameof(Made));
        Observe(typeof(FleckThrown), "NextPosition", nameof(Moved));
        probe.Patch(
            AccessTools.Method(typeof(CompShield), "Draw"),
            prefix: new HarmonyMethod(typeof(WatchtowerEffectProbe), nameof(DrawnShield))
        );
        probe.Patch(
            AccessTools.Method(
                typeof(Graphic_Mote),
                nameof(Graphic_Mote.DrawMote),
                new[]
                {
                    typeof(GraphicData),
                    typeof(Material),
                    typeof(Color),
                    typeof(Vector3),
                    typeof(Rot4),
                    typeof(ThingDef),
                    typeof(Thing),
                    typeof(int),
                    typeof(bool),
                    typeof(MaterialPropertyBlock),
                }
            ),
            postfix: new HarmonyMethod(typeof(WatchtowerEffectProbe), nameof(DrawnMote))
        );
        probe.Patch(
            AccessTools.Method(
                typeof(FleckStatic),
                nameof(FleckStatic.Draw),
                new[] { typeof(float), typeof(DrawBatch) }
            ),
            postfix: new HarmonyMethod(typeof(WatchtowerEffectProbe), nameof(DrawnFleck))
        );
        Reset();
    }

    private static void Born(ref FleckStatic __instance)
    {
        if (collecting && born.Count < 128)
            born.Add(__instance);
    }

    private static void Made(Thing __result)
    {
        if (collecting && __result is Mote mote && motes.Count < 128)
            motes.Add(mote);
    }

    private static void Moved(
        ref FleckThrown __instance,
        float deltaTime,
        FleckDrawPosition __result
    )
    {
        var data = __instance.baseData;
        if (
            actor == null
            || data.map != actor.Map
            || data.def.defName != "AirPuff"
            || (data.spawnPosition - actor.Position.ToVector3Shifted()).MagnitudeHorizontal() > 1
        )
            return;
        var extra = data.position.unattachedDrawOffset - data.def.unattachedDrawOffset;
        if (extra == Vector3.zero)
            return;
        Interlocked.Increment(ref movedFlecks);
        if (
            (__result.unattachedDrawOffset - data.def.unattachedDrawOffset - extra).sqrMagnitude
                > .000001f
            || (
                __result.worldPosition
                - data.position.worldPosition
                - __instance.velocity * deltaTime
            ).sqrMagnitude > .000001f
        )
            invalidMotion = true;
    }

    private static void DrawnFleck(ref FleckStatic __instance, bool __runOriginal)
    {
        if (
            __runOriginal
            && actor != null
            && __instance.map == actor.Map
            && (__instance.spawnPosition - actor.Position.ToVector3Shifted()).MagnitudeHorizontal()
                < 1
            && __instance.position.unattachedDrawOffset.z
                > __instance.def.unattachedDrawOffset.z + 1
        )
            Interlocked.Increment(ref drawnFlecks);
    }

    private static void DrawnMote(Thing thing)
    {
        if (
            thing is Mote mote
            && motes.Contains(mote)
            && (
                mote.DrawPos - mote.exactPosition - TowerEffects.MoteOffset(mote)
            ).MagnitudeHorizontal() < .001f
            && TowerEffects.MoteOffset(mote).z > 1
        )
            Interlocked.Increment(ref drawnMotes);
    }

    private static void DrawnShield(CompShield __instance)
    {
        if (
            __instance == shield
            && actor != null
            && TowerPresentation.DrawingDepth > 0
            && actor.Drawer.DrawPos.z > actor.Position.z + 1.5f
        )
            Interlocked.Increment(ref shieldDraws);
    }

    internal static void CompareNativeEffects(Pawn raised, Pawn ground, Action<string, bool> check)
    {
        actor = raised;
        foreach (var name in new[] { "Damage_HitFlesh", "Deflect_Metal" })
        {
            var definition = DefDatabase<EffecterDef>.GetNamed(name);
            var original = definition
                .children.Select(d => d.fleckDef?.unattachedDrawOffset ?? Vector3.zero)
                .ToArray();
            // Native parent jitter is seeded from the effecter's identity.
            // Reuse that parent for both actors while keeping native child state.
            var effect = definition.Spawn();
            FleckStatic[] plain,
                lifted;
            try
            {
                plain = Trigger(effect, ground, 89432);
                lifted = Trigger(effect, raised, 89432);
            }
            finally
            {
                effect.Cleanup();
            }
            check(
                "native effect particle count " + name,
                plain.Length > 0 && plain.Length == lifted.Length
            );
            for (int i = 0; i < plain.Length; i++)
            {
                var a = plain[i];
                var b = lifted[i];
                geometry.Add(
                    new
                    {
                        name,
                        i,
                        sameDef = a.def == b.def,
                        plain = a.position.worldPosition.ToString(),
                        lifted = b.position.worldPosition.ToString(),
                        groundCenter = ground.DrawPos.ToString(),
                        raisedCenter = raised.DrawPos.ToString(),
                        plainOffset = a.position.unattachedDrawOffset.ToString(),
                        liftedOffset = b.position.unattachedDrawOffset.ToString(),
                        definitionOffset = b.def.unattachedDrawOffset.ToString(),
                        expected = TowerPresentation.Offset(raised).ToString(),
                        plainMap = a.map?.uniqueID,
                        liftedMap = b.map?.uniqueID,
                    }
                );
                check(
                    "native effect geometry " + name + " " + i,
                    a.def == b.def
                        && (
                            a.position.worldPosition
                            - ground.DrawPos
                            - b.position.worldPosition
                            + raised.DrawPos
                        ).MagnitudeHorizontal() < .001f
                        && a.position.worldPosition.y == b.position.worldPosition.y
                        && a.position.unattachedDrawOffset == a.def.unattachedDrawOffset
                        && (
                            b.position.unattachedDrawOffset
                            - b.def.unattachedDrawOffset
                            - TowerPresentation.Offset(raised)
                        ).sqrMagnitude < .000001f
                        && a.map == null
                        && b.map == raised.Map
                );
            }
            check(
                "native effect definition retained " + name,
                definition
                    .children.Select(d => d.fleckDef?.unattachedDrawOffset ?? Vector3.zero)
                    .SequenceEqual(original)
            );
        }
        check(
            "loose native sparks receive drawing projection",
            motes.Any(m => m.def.defName.Contains("Spark") && TowerEffects.MoteOffset(m).z > 1)
        );
        // DamageWorker's own cell-targeted effects have no Thing in TargetInfo.
        // Exercise the actual injury producer, separately from direct effecter controls.
        born.Clear();
        collecting = true;
        try
        {
            raised.TakeDamage(new DamageInfo(DamageDefOf.Bullet, 2f, 1000f, instigator: ground));
        }
        finally
        {
            collecting = false;
        }
        check(
            "actual native injury effects are raised",
            born.Count > 0
                && born.All(f =>
                    f.map == raised.Map
                    && f.position.unattachedDrawOffset.z > f.def.unattachedDrawOffset.z + 1
                )
        );
        var belt = (Apparel)
            ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_ShieldBelt"));
        raised.apparel.Wear(belt);
        shield = belt.GetComp<CompShield>();
        // Charge this owned fixture belt without replacing any damage result.
        var energy = AccessTools.FieldRefAccess<CompShield, float>("energy");
        energy(shield) = .8f;
        float health = raised.health.summaryHealth.SummaryHealthPercent;
        born.Clear();
        collecting = true;
        DamageWorker.DamageResult absorbed;
        try
        {
            absorbed = raised.TakeDamage(
                new DamageInfo(DamageDefOf.Bullet, 2f, 1000f, instigator: ground)
            );
        }
        finally
        {
            collecting = false;
        }
        check(
            "native shield absorption preserves damage policy",
            shield.ShieldState == ShieldState.Active
                && energy(shield) < .8f
                && absorbed.totalDamageDealt == 0
                && raised.health.summaryHealth.SummaryHealthPercent == health
        );
        check(
            "native shield impact particles receive drawing projection",
            born.Count > 0
                && born.All(f =>
                    f.map == raised.Map
                    && f.position.unattachedDrawOffset.z > f.def.unattachedDrawOffset.z + 1
                )
        );
    }

    private static FleckStatic[] Trigger(Effecter effect, Pawn target, int seed)
    {
        born.Clear();
        collecting = true;
        Rand.PushState(seed);
        try
        {
            effect.Trigger(target, target);
            return born.ToArray();
        }
        finally
        {
            Rand.PopState();
            collecting = false;
        }
    }

    internal static void VerifyPlayback(Action<string, bool> check)
    {
        check(
            "actual moving native flecks retain projection and physics",
            movedFlecks > 0 && !invalidMotion
        );
        check("actual native impact flecks draw at platform height", drawnFlecks > 0);
        check("actual loose native sparks draw at platform height", drawnMotes > 0);
        check("actual native shield drawing reads raised tracker", shieldDraws > 0);
    }

    internal static void Reset()
    {
        collecting = false;
        born.Clear();
        motes.Clear();
        actor = null;
        movedFlecks = drawnFlecks = drawnMotes = 0;
        invalidMotion = false;
        shield = null;
        shieldDraws = 0;
        geometry.Clear();
    }
}
