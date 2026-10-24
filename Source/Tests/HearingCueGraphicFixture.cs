namespace UnityEngine
{
    public readonly record struct Color(float r, float g, float b, float a);

    public class Material
    {
        public int renderQueue;
    }

    public class MaterialPropertyBlock
    {
        public void SetColor(int id, Color value) { }

        public void SetFloat(int id, float value) { }
    }
}

namespace Verse
{
    public struct GraphicRequest
    {
        public int renderQueue;
    }

    public static class MatBases
    {
        public static readonly UnityEngine.Material FogOfWar = new() { renderQueue = 3500 };
    }

    public static class ShaderPropertyIDs
    {
        public const int ShockwaveColor = 1,
            ShockwaveSpan = 2;
    }

    public class Graphic_Mote
    {
        protected static readonly UnityEngine.MaterialPropertyBlock propertyBlock = new();
        protected string path;
        protected object Shader;
        protected UnityEngine.Color color;
        protected virtual bool ForcePropertyBlock => false;
        public GraphicRequest AppliedRequest { get; private set; }

        public virtual void Init(GraphicRequest request) => AppliedRequest = request;

        public virtual void DrawWorker(
            UnityEngine.Vector3 loc,
            Rot4 rot,
            ThingDef def,
            Thing thing,
            float rotation
        ) { }

        protected void DrawMoteInternal(
            UnityEngine.Vector3 loc,
            Rot4 rot,
            ThingDef def,
            Thing thing,
            int layer
        ) { }
    }
}
