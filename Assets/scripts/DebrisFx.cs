using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trozos (cubitos) que salen despedidos con gravedad, giran, se encogen y se desvanecen.
/// Se usa al romperse una plataforma, al ocultarse una temporizada y al morir el jugador.
/// Pool fijo creado en código; no instancia nada durante el juego. Usa el material de efectos (FX_Unlit).
/// </summary>
public class DebrisFx : MonoBehaviour
{
    private const int PoolSize = 72;
    private const float Gravity = 24f;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private class Piece
    {
        public Transform tf;
        public MeshRenderer renderer;
        public Vector3 velocity;
        public Vector3 spin;
        public Color color;
        public float size;
        public float age;
        public float life;
        public bool alive;
    }

    private static DebrisFx instance;
    private static Material sMaterial;

    private readonly List<Piece> pieces = new List<Piece>(PoolSize);
    private MaterialPropertyBlock block;

    /// <summary>Material transparente para los trozos (lo asigna LevelGenerator).</summary>
    public static void Init(Material material)
    {
        sMaterial = material;
    }

    private static DebrisFx Get()
    {
        if (instance != null) return instance;
        if (sMaterial == null) return null;
        GameObject go = new GameObject("DebrisFx");
        instance = go.AddComponent<DebrisFx>();
        instance.Build();
        return instance;
    }

    private void Build()
    {
        block = new MaterialPropertyBlock();
        Mesh cube = FxUtil.CubeMesh();
        Material mat = new Material(sMaterial);
        for (int i = 0; i < PoolSize; i++)
        {
            GameObject go = new GameObject("Debris");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = cube;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
            pieces.Add(new Piece { tf = go.transform, renderer = mr });
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>
    /// Lanza 'count' trozos repartidos dentro de una caja (centro +- extents), con velocidad base
    /// más una dispersión aleatoria.
    /// </summary>
    public static void Burst(Vector3 center, Vector3 extents, int count, Color color, Vector3 baseVelocity,
                             float spread, float life, float size)
    {
        DebrisFx fx = Get();
        if (fx == null) return;
        for (int i = 0; i < count; i++)
        {
            Piece p = fx.Take();
            Vector3 offset = new Vector3(Random.Range(-extents.x, extents.x), Random.Range(-extents.y, extents.y), Random.Range(-extents.z, extents.z));
            Vector2 r = Random.insideUnitCircle * spread;
            p.velocity = baseVelocity + new Vector3(r.x, r.y, Random.Range(-0.3f, 0.3f)) + new Vector3(offset.x, offset.y, 0f) * 1.5f;
            p.spin = new Vector3(Random.Range(-540f, 540f), Random.Range(-540f, 540f), Random.Range(-540f, 540f));
            p.color = color;
            p.size = size * Random.Range(0.6f, 1.35f);
            p.age = 0f;
            p.life = life * Random.Range(0.75f, 1.2f);
            p.alive = true;
            p.tf.SetPositionAndRotation(center + offset, Random.rotation);
            p.tf.localScale = Vector3.one * p.size;
            p.tf.gameObject.SetActive(true);
            fx.Apply(p, 1f);
        }
    }

    /// <summary>Oculta todos los trozos (reinicio de partida).</summary>
    public static void ClearAll()
    {
        if (instance == null) return;
        for (int i = 0; i < instance.pieces.Count; i++)
        {
            instance.pieces[i].alive = false;
            instance.pieces[i].tf.gameObject.SetActive(false);
        }
    }

    private Piece Take()
    {
        Piece oldest = pieces[0];
        for (int i = 0; i < pieces.Count; i++)
        {
            if (!pieces[i].alive) return pieces[i];
            if (pieces[i].age / pieces[i].life > oldest.age / oldest.life) oldest = pieces[i];
        }
        return oldest;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        for (int i = 0; i < pieces.Count; i++)
        {
            Piece p = pieces[i];
            if (!p.alive) continue;
            p.age += dt;
            float k = p.age / p.life;
            if (k >= 1f)
            {
                p.alive = false;
                p.tf.gameObject.SetActive(false);
                continue;
            }
            p.velocity.y -= Gravity * dt;
            p.velocity *= 1f - 0.6f * dt;   // un poco de rozamiento con el aire
            p.tf.position += p.velocity * dt;
            p.tf.rotation = Quaternion.Euler(p.spin * dt) * p.tf.rotation;
            // Mantiene el tamaño la primera mitad y luego se encoge y desvanece
            float fade = k < 0.45f ? 1f : 1f - (k - 0.45f) / 0.55f;
            p.tf.localScale = Vector3.one * (p.size * Mathf.Lerp(0.35f, 1f, fade));
            Apply(p, fade);
        }
    }

    private void Apply(Piece p, float fade)
    {
        Color c = p.color;
        c.a *= fade;
        block.SetColor(BaseColorId, c);
        p.renderer.SetPropertyBlock(block);
    }
}
