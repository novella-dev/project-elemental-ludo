using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalLudo.Board
{
    /// <summary>
    /// Turns each of the four corner quadrants into an elemental biome
    /// platform: a volcanic range (Fire/Red), a lake (Water/Blue), a forest
    /// (Plant/Green) and a storm plateau (Lightning/Yellow). Replaces the
    /// old colored home circles — each biome keeps its player's color as a
    /// tint on the raised rim so you can still tell whose corner it is.
    ///
    /// Pure procedural vertex-colored geometry, same technique and shader as
    /// LudoBoardRenderer/LudoBoardDepthRenderer, so it needs no art assets.
    /// Everything here is decoration: it never touches routes, layout or
    /// gameplay.
    ///
    /// Layout notes, since everything is laid out blind (no Editor here):
    /// a corner quadrant spans 8x8 world units, e.g. x[-11,-3] y[3,11] for
    /// red, centered on that player's home at (-7,7). The four tokens sit at
    /// (±0.96, ±0.96) around that center and are ~0.34 wide, so they occupy
    /// roughly a 1.7-radius disc. Every decoration is placed outside that
    /// disc and inside the rim so nothing buries a token.
    ///
    /// Depth convention matches the rest of the board: more negative Z is
    /// higher. Tokens rest at z = -0.34, just above the -0.30 ground.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LudoBoardTerrainRenderer : MonoBehaviour
    {
        private const string BoardShaderName = "Elemental Ludo/Board Vertex Color";
        private const string OutlineShaderName = "Elemental Ludo/Token Outline";
        private const string OutlineObjectName = "__TerrainCelOutline";
        private const string SmokeRootName = "__VolcanoSmokeParticles";
        private const string SmokeMaterialResourceName =
            "LudoSmokeParticleMaterial";

        private const float WaterRippleAnimation = 1f;
        private const float TreeAnimation = 2f;
        private const float LightningAnimation = 3f;
        private const float CloudAnimation = 4f;
        private const float LavaAnimation = 5f;

        private const int RadialSegments = 16;
        private const int FloorSubdivisions = 20;

        private const float GroundDepth = -0.30f;
        private const float RimTopDepth = -0.40f;
        private const float SkirtBottomDepth = 0.16f;
        private const float RimWidth = 0.55f;

        // Parked tokens sit at (±TokenOffset, ±TokenOffset) from the corner
        // center; decorations stay clear of that disc.
        private const float TokenOffset = 0.96f;

        private static readonly Color PlayerRed = LudoBoardVisualStyle.Red;
        private static readonly Color PlayerBlue = LudoBoardVisualStyle.Blue;
        private static readonly Color PlayerGreen = LudoBoardVisualStyle.Green;
        private static readonly Color PlayerYellow = LudoBoardVisualStyle.Yellow;

        // Fire
        private static readonly Color AshDark = new Color(0.16f, 0.04f, 0.06f);
        private static readonly Color AshWarm = new Color(0.42f, 0.07f, 0.06f);
        private static readonly Color Ember = new Color(0.95f, 0.16f, 0.02f);
        private static readonly Color RockLow = new Color(0.28f, 0.025f, 0.045f);
        private static readonly Color RockHigh = new Color(0.78f, 0.075f, 0.035f);
        private static readonly Color LavaEdge = new Color(1f, 0.10f, 0.01f);
        private static readonly Color LavaMid = new Color(1f, 0.48f, 0.02f);
        private static readonly Color LavaCore = new Color(1f, 0.94f, 0.30f);
        private static readonly Color LavaCrust = new Color(0.07f, 0.008f, 0.012f);
        private static readonly Color SmokeDark = new Color(0.025f, 0.028f, 0.035f, 0.88f);
        private static readonly Color SmokeLight = new Color(0.16f, 0.17f, 0.19f, 0.68f);

        // Water
        private static readonly Color WaterDeep = new Color(0.015f, 0.16f, 0.58f);
        private static readonly Color WaterMid = new Color(0.02f, 0.52f, 0.96f);
        private static readonly Color WaterShallow = new Color(0.12f, 0.84f, 1f);
        private static readonly Color WaterFoam = new Color(0.76f, 0.98f, 1f);

        // Plant
        private static readonly Color GrassDark = new Color(0.025f, 0.31f, 0.07f);
        private static readonly Color GrassLight = new Color(0.16f, 0.70f, 0.12f);
        private static readonly Color TrunkDark = new Color(0.25f, 0.10f, 0.025f);
        private static readonly Color TrunkLight = new Color(0.58f, 0.27f, 0.06f);
        private static readonly Color LeafDark = new Color(0.015f, 0.29f, 0.045f);
        private static readonly Color LeafMid = new Color(0.045f, 0.72f, 0.07f);
        private static readonly Color LeafLight = new Color(0.38f, 1f, 0.14f);

        // Lightning
        private static readonly Color StormGroundDark = new Color(0.13f, 0.14f, 0.19f);
        private static readonly Color StormGroundLight = new Color(0.30f, 0.31f, 0.39f);
        private static readonly Color StormRimGrey = new Color(0.34f, 0.36f, 0.45f);
        private static readonly Color CloudShadow = new Color(0.52f, 0.54f, 0.58f);
        private static readonly Color CloudMid = new Color(0.59f, 0.61f, 0.65f);
        private static readonly Color CloudTop = new Color(0.66f, 0.68f, 0.72f);
        private static readonly Color BoltCore = new Color(1f, 0.98f, 0.72f);
        private static readonly Color BoltEdge = new Color(0.98f, 0.82f, 0.22f);

        [Header("Cartoon Styling")]
        [Tooltip("Physical width of the scenery's dark cel outline in board world units.")]
        [SerializeField, Range(0.005f, 0.08f)]
        private float celOutlineWidth = 0.018f;
        [SerializeField]
        private Color celOutlineColor = new Color(0.01f, 0.015f, 0.02f, 1f);

        private readonly List<Vector3> vertices = new List<Vector3>(16384);
        private readonly List<Color> colors = new List<Color>(16384);
        private readonly List<int> triangles = new List<int>(24576);
        private readonly List<Vector4> animationData = new List<Vector4>(16384);
        private readonly List<Vector3> outlineVertices = new List<Vector3>(8192);
        private readonly List<int> outlineTriangles = new List<int>(12288);
        private readonly List<Vector4> outlineAnimationData = new List<Vector4>(8192);
        private readonly List<SmokeEmitterDefinition> smokeEmitterDefinitions =
            new List<SmokeEmitterDefinition>(4);

        private Mesh terrainMesh;
        private Mesh outlineMesh;
        private Material fallbackMaterial;
        private Material outlineMaterial;
        private Material smokeParticleMaterial;
        private GameObject outlineObject;
        private GameObject smokeRoot;
        private bool captureOutlineGeometry;
#if UNITY_EDITOR
        private bool editorRebuildScheduled;
#endif
        private Vector4 currentAnimationData;

        private readonly struct SmokeEmitterDefinition
        {
            public SmokeEmitterDefinition(Vector3 localPosition, float scale)
            {
                LocalPosition = localPosition;
                Scale = scale;
            }

            public Vector3 LocalPosition { get; }
            public float Scale { get; }
        }

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ScheduleEditorRebuild();
                return;
            }
#endif
            Rebuild();
        }

#if UNITY_EDITOR
        private void ScheduleEditorRebuild()
        {
            if (editorRebuildScheduled)
            {
                return;
            }

            editorRebuildScheduled = true;
            UnityEditor.EditorApplication.delayCall += PerformScheduledEditorRebuild;
        }

        private void PerformScheduledEditorRebuild()
        {
            editorRebuildScheduled = false;
            if (this != null && isActiveAndEnabled)
            {
                Rebuild();
            }
        }
#endif

        [ContextMenu("Rebuild Terrain")]
        public void Rebuild()
        {
            captureOutlineGeometry = false;
            currentAnimationData = Vector4.zero;
            vertices.Clear();
            colors.Clear();
            triangles.Clear();
            animationData.Clear();
            outlineVertices.Clear();
            outlineTriangles.Clear();
            outlineAnimationData.Clear();
            smokeEmitterDefinitions.Clear();

            float inner = LudoBoardLayout.ToWorldCoordinate(LudoBoardLayout.CenterHalfExtent);
            float outer = LudoBoardLayout.ToWorldCoordinate(9.5f);

            BuildFireCorner(new Vector2(-outer, inner), new Vector2(-inner, outer));
            BuildWaterCorner(new Vector2(inner, inner), new Vector2(outer, outer));
            BuildPlantCorner(new Vector2(-outer, -outer), new Vector2(-inner, -inner));
            BuildLightningCorner(new Vector2(inner, -outer), new Vector2(outer, -inner));

            if (terrainMesh == null)
            {
                terrainMesh = new Mesh
                {
                    name = "Ludo Board Elemental Terrain",
                    hideFlags = HideFlags.DontSave
                };
            }
            else
            {
                terrainMesh.Clear();
            }

            terrainMesh.SetVertices(vertices);
            terrainMesh.SetColors(colors);
            terrainMesh.SetUVs(1, animationData);
            terrainMesh.SetTriangles(triangles, 0);
            terrainMesh.RecalculateNormals();
            SmoothSharedVertexNormals(terrainMesh);
            terrainMesh.RecalculateBounds();

            GetComponent<MeshFilter>().sharedMesh = terrainMesh;

            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            if (meshRenderer.sharedMaterial == null)
            {
                Shader shader = Shader.Find(BoardShaderName);
                if (shader != null)
                {
                    if (fallbackMaterial == null || fallbackMaterial.shader != shader)
                    {
                        DestroyGeneratedObject(fallbackMaterial);
                        fallbackMaterial = new Material(shader)
                        {
                            name = "Ludo Board Terrain Material (Generated)",
                            hideFlags = HideFlags.DontSave
                        };
                    }

                    meshRenderer.sharedMaterial = fallbackMaterial;
                }
            }

            RebuildOutline();
            RebuildSmokeEmitters();
        }

        private void OnDestroy()
        {
            DestroyGeneratedObject(outlineObject);
            DestroyGeneratedObject(smokeRoot);
            DestroyGeneratedObject(terrainMesh);
            DestroyGeneratedObject(outlineMesh);
            DestroyGeneratedObject(fallbackMaterial);
            DestroyGeneratedObject(outlineMaterial);
        }

        private void RebuildOutline()
        {
            if (outlineMesh == null)
            {
                outlineMesh = new Mesh
                {
                    name = "Ludo Board Terrain Cel Outline",
                    hideFlags = HideFlags.DontSave
                };
            }
            else
            {
                outlineMesh.Clear();
            }

            outlineMesh.indexFormat = outlineVertices.Count > ushort.MaxValue
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;
            outlineMesh.SetVertices(outlineVertices);
            outlineMesh.SetUVs(1, outlineAnimationData);
            outlineMesh.SetTriangles(outlineTriangles, 0);
            outlineMesh.RecalculateNormals();
            SmoothSharedVertexNormals(outlineMesh);
            outlineMesh.RecalculateBounds();

            MeshRenderer outlineRenderer = GetOrCreateOutlineRenderer();
            outlineRenderer.enabled = outlineTriangles.Count > 0;
            outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineRenderer.lightProbeUsage = LightProbeUsage.Off;
            outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            Shader outlineShader = Shader.Find(OutlineShaderName);
            if (outlineShader == null)
            {
                outlineRenderer.enabled = false;
                Debug.LogWarning(
                    $"Terrain outline shader '{OutlineShaderName}' was not found.",
                    this);
                return;
            }

            if (outlineMaterial == null || outlineMaterial.shader != outlineShader)
            {
                DestroyGeneratedObject(outlineMaterial);
                outlineMaterial = new Material(outlineShader)
                {
                    name = "Ludo Terrain Cel Outline (Generated)",
                    hideFlags = HideFlags.DontSave
                };
            }

            outlineMaterial.SetFloat("_OutlineWidth", celOutlineWidth);
            outlineMaterial.SetColor("_OutlineColor", celOutlineColor);
            outlineMaterial.SetFloat("_TerrainAnimationEnabled", 1f);
            outlineRenderer.sharedMaterial = outlineMaterial;
        }

        private MeshRenderer GetOrCreateOutlineRenderer()
        {
            if (outlineObject == null)
            {
                Transform existing = transform.Find(OutlineObjectName);
                outlineObject = existing != null ? existing.gameObject : null;
            }

            if (outlineObject == null)
            {
                outlineObject = new GameObject(OutlineObjectName)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = gameObject.layer
                };
                outlineObject.transform.SetParent(transform, false);
            }

            outlineObject.layer = gameObject.layer;
            MeshFilter outlineFilter = outlineObject.GetComponent<MeshFilter>();
            if (outlineFilter == null)
            {
                outlineFilter = outlineObject.AddComponent<MeshFilter>();
            }

            MeshRenderer outlineRenderer = outlineObject.GetComponent<MeshRenderer>();
            if (outlineRenderer == null)
            {
                outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
            }

            outlineFilter.sharedMesh = outlineMesh;
            return outlineRenderer;
        }

        private static void SmoothSharedVertexNormals(Mesh mesh)
        {
            Vector3[] meshVertices = mesh.vertices;
            Vector3[] meshNormals = mesh.normals;
            if (meshVertices.Length == 0 || meshNormals.Length != meshVertices.Length)
            {
                return;
            }

            Dictionary<Vector3, Vector3> normalSums =
                new Dictionary<Vector3, Vector3>(meshVertices.Length);
            for (int index = 0; index < meshVertices.Length; index++)
            {
                Vector3 position = meshVertices[index];
                normalSums.TryGetValue(position, out Vector3 normalSum);
                normalSums[position] = normalSum + meshNormals[index];
            }

            for (int index = 0; index < meshVertices.Length; index++)
            {
                Vector3 normalSum = normalSums[meshVertices[index]];
                if (normalSum.sqrMagnitude > 0.000001f)
                {
                    meshNormals[index] = normalSum.normalized;
                }
            }

            mesh.normals = meshNormals;
        }

        private void RebuildSmokeEmitters()
        {
            if (smokeParticleMaterial == null)
            {
                smokeParticleMaterial = Resources.Load<Material>(
                    SmokeMaterialResourceName);
            }

            if (smokeRoot == null)
            {
                Transform existing = transform.Find(SmokeRootName);
                smokeRoot = existing != null ? existing.gameObject : null;
            }

            DestroyGeneratedObject(smokeRoot);
            smokeRoot = new GameObject(SmokeRootName)
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = gameObject.layer
            };
            smokeRoot.transform.SetParent(transform, false);

            for (int emitterIndex = 0;
                 emitterIndex < smokeEmitterDefinitions.Count;
                 emitterIndex++)
            {
                CreateSmokeEmitter(
                    smokeEmitterDefinitions[emitterIndex],
                    emitterIndex);
            }
        }

        private void CreateSmokeEmitter(
            SmokeEmitterDefinition definition,
            int emitterIndex)
        {
            GameObject emitterObject = new GameObject(
                $"Volcano Smoke {emitterIndex + 1}")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = gameObject.layer
            };
            emitterObject.transform.SetParent(smokeRoot.transform, false);
            emitterObject.transform.localPosition = definition.LocalPosition;

            ParticleSystem particles = emitterObject.AddComponent<ParticleSystem>();
            particles.useAutoRandomSeed = false;
            particles.randomSeed = (uint)(1709 + emitterIndex * 977);

            ParticleSystem.MainModule main = particles.main;
            main.duration = 4f;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.gravityModifier = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.045f + definition.Scale * 0.015f,
                0.075f + definition.Scale * 0.028f);
            main.startRotation = new ParticleSystem.MinMaxCurve(
                0f,
                Mathf.PI * 2f);
            main.startColor = Color.white;
            main.maxParticles = 96;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 12f + definition.Scale * 5f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = definition.Scale * 0.15f;
            shape.radiusThickness = 1f;

            ParticleSystem.VelocityOverLifetimeModule velocity =
                particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.045f, 0.045f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.035f, 0.035f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.72f, -0.38f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.strength = 0.10f + definition.Scale * 0.055f;
            noise.frequency = 0.58f;
            noise.scrollSpeed = 0.24f;
            noise.damping = true;
            noise.octaveCount = 2;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
                particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.38f),
                    new Keyframe(0.22f, 0.78f),
                    new Keyframe(1f, 1.25f)));

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient smokeGradient = new Gradient();
            smokeGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(SmokeDark, 0f),
                    new GradientColorKey(SmokeDark, 0.48f),
                    new GradientColorKey(SmokeLight, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.74f, 0.12f),
                    new GradientAlphaKey(0.50f, 0.68f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = smokeGradient;

            ParticleSystem.RotationOverLifetimeModule rotation =
                particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.42f, 0.42f);

            ParticleSystemRenderer particleRenderer =
                emitterObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.alignment = ParticleSystemRenderSpace.View;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            particleRenderer.lightProbeUsage = LightProbeUsage.Off;
            particleRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            particleRenderer.sortingFudge = 1f;
            particleRenderer.sharedMaterial = smokeParticleMaterial;
            particleRenderer.enabled = smokeParticleMaterial != null;

            if (smokeParticleMaterial == null)
            {
                Debug.LogWarning(
                    $"Smoke particle material resource " +
                    $"'{SmokeMaterialResourceName}' was not found.",
                    this);
            }

            if (Application.isPlaying)
            {
                particles.Play(true);
            }
            else
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        // ------------------------------------------------------------------
        // Corners
        // ------------------------------------------------------------------

        private void BuildFireCorner(Vector2 min, Vector2 max)
        {
            Vector2 center = (min + max) * 0.5f;

            AddGround(min, max, (x, y) =>
            {
                float n = FractalNoise(x * 0.55f + 3.1f, y * 0.55f + 8.7f);
                Color rock = Color.Lerp(AshDark, AshWarm, n);
                float e = FractalNoise(x * 1.7f + 41f, y * 1.7f + 19f);
                if (e > 0.74f)
                {
                    rock = Color.Lerp(rock, Ember, (e - 0.74f) / 0.26f * 0.55f);
                }

                return rock;
            });
            AddRim(min, max, Color.Lerp(RockLow, PlayerRed, 0.5f));

            // A meandering main channel feeds three narrowing branches. Each
            // outer branch terminates in an irregular pool instead of a flat
            // ribbon edge.
            Vector2[] mainFlow = ArcPoints(center, 2.05f, 18f, 252f, 22);
            Vector2[] eastFlow = RadialSpur(center, 60f, 2.0f, 3.3f);
            Vector2[] northFlow = RadialSpur(center, 110f, 2.0f, 3.3f);
            Vector2[] westFlow = RadialSpur(center, 208f, 2.0f, 3.2f);

            AddLavaRibbon(mainFlow, 0.34f, true, true);
            AddLavaRibbon(eastFlow, 0.17f, false, true);
            AddLavaRibbon(northFlow, 0.15f, false, true);
            AddLavaRibbon(westFlow, 0.16f, false, true);

            AddLavaPool(eastFlow[eastFlow.Length - 1], 0.31f, 0.17f);
            AddLavaPool(northFlow[northFlow.Length - 1], 0.26f, 0.43f);
            AddLavaPool(westFlow[westFlow.Length - 1], 0.29f, 0.71f);
            AddLavaPool(mainFlow[0], 0.22f, 0.29f);
            AddLavaPool(mainFlow[mainFlow.Length - 1], 0.24f, 0.83f);

            // Semicircular range: one main cone plus three smaller ones,
            // with a pair of cinder cones filling the gaps.
            AddCelStyledGeometry(() =>
            {
                AddVolcano(center + PolarOffset(135f, 3.0f), 1.25f, 2.45f);
                AddVolcano(center + PolarOffset(82f, 2.5f), 0.90f, 1.70f);
                AddVolcano(center + PolarOffset(188f, 2.4f), 1.00f, 1.95f);
                AddVolcano(center + PolarOffset(40f, 2.5f), 0.68f, 1.15f);
                AddCinderCone(center + PolarOffset(228f, 2.4f), 0.50f, 0.75f);
                AddCinderCone(center + PolarOffset(160f, 1.95f), 0.34f, 0.45f);
            });
        }

        private void BuildWaterCorner(Vector2 min, Vector2 max)
        {
            Vector2 center = (min + max) * 0.5f;

            AddCelStyledGeometry(
                () => AddGround(min, max, (x, y) =>
                {
                    Vector2 local = new Vector2(x - center.x, y - center.y);
                    // Square falloff (distance to the nearest edge, not radial),
                    // so the shading follows the square plate: dark in the middle
                    // as if deep, bright at the shore.
                    float shore = Mathf.Clamp01(
                        Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.y)) / 4f);
                    Color water = Color.Lerp(
                        WaterDeep,
                        WaterMid,
                        Mathf.Clamp01(shore * 1.6f));
                    water = Color.Lerp(
                        water,
                        WaterShallow,
                        Mathf.Clamp01((shore - 0.55f) / 0.45f));

                    float n = FractalNoise(x * 0.7f + 11f, y * 0.7f + 5f);
                    water = Color.Lerp(water, WaterShallow, n * 0.22f);

                    // Long shallow swell bands, so the surface isn't just noise.
                    float swell = Mathf.Sin(
                        (x + y) * 1.15f + n * 2.2f) * 0.5f + 0.5f;
                    water = Color.Lerp(
                        water,
                        WaterFoam,
                        Mathf.Pow(swell, 6f) * 0.30f);
                    return water;
                }));
            AddCelStyledGeometry(() =>
                AddRim(min, max, Color.Lerp(WaterMid, PlayerBlue, 0.55f)));

            // Ripple rings spreading from each parked water drop.
            for (int tokenIndex = 0; tokenIndex < 4; tokenIndex++)
            {
                Vector2 tokenCenter = center + TokenCorner(tokenIndex);
                for (int rippleIndex = 0; rippleIndex < 3; rippleIndex++)
                {
                    float phase = rippleIndex / 3f + tokenIndex * 0.035f;
                    float colorBlend = rippleIndex / 2f;
                    Color rippleColor = Color.Lerp(
                        WaterFoam,
                        WaterShallow,
                        colorBlend * 0.25f);

                    AddAnimatedGeometry(
                        new Vector4(
                            WaterRippleAnimation,
                            phase,
                            tokenCenter.x,
                            tokenCenter.y),
                        () => AddRing(
                            tokenCenter,
                            0.52f,
                            0.028f,
                            GroundDepth - 0.015f,
                            rippleColor));
                }
            }

            // A few rocks breaking the surface near the shore.
            AddRock(center + PolarOffset(38f, 2.85f), 0.42f, 0.34f);
            AddRock(center + PolarOffset(126f, 2.70f), 0.30f, 0.24f);
            AddRock(center + PolarOffset(214f, 2.90f), 0.36f, 0.28f);
            AddRock(center + PolarOffset(300f, 2.65f), 0.24f, 0.18f);
        }

        private void BuildPlantCorner(Vector2 min, Vector2 max)
        {
            Vector2 center = (min + max) * 0.5f;

            AddGround(min, max, (x, y) =>
            {
                float n = FractalNoise(x * 0.85f + 17f, y * 0.85f + 29f);
                Color grass = Color.Lerp(GrassDark, GrassLight, n);
                float patch = FractalNoise(x * 0.35f + 61f, y * 0.35f + 47f);
                return Color.Lerp(grass, GrassDark, Mathf.Clamp01((patch - 0.6f) * 1.6f) * 0.5f);
            });
            AddRim(min, max, Color.Lerp(TrunkDark, PlayerGreen, 0.55f));

            // Ring of trees around the parked tokens, varied in size.
            AddAnimatedCelGeometry(
                new Vector4(TreeAnimation, 0f, 0f, 0f),
                () =>
                {
                    AddTree(center + new Vector2(-2.50f, -0.40f), 1.00f);
                    AddTree(center + new Vector2(-1.90f, -2.00f), 1.20f);
                    AddTree(center + new Vector2(-0.30f, -2.60f), 0.90f);
                    AddTree(center + new Vector2(1.50f, -2.30f), 1.10f);
                    AddTree(center + new Vector2(2.50f, -0.90f), 0.85f);
                    AddTree(center + new Vector2(2.20f, 1.50f), 1.10f);
                    AddTree(center + new Vector2(0.50f, 2.50f), 1.00f);
                    AddTree(center + new Vector2(-1.80f, 2.20f), 0.80f);
                    AddTree(center + new Vector2(-2.90f, 1.10f), 0.90f);

                    // Low bushes filling the gaps between trunks.
                    AddBush(center + new Vector2(-2.75f, -1.35f), 0.34f);
                    AddBush(center + new Vector2(0.65f, -2.95f), 0.28f);
                    AddBush(center + new Vector2(2.95f, 0.35f), 0.32f);
                    AddBush(center + new Vector2(-0.70f, 2.95f), 0.26f);
                    AddBush(center + new Vector2(-2.95f, -0.05f), 0.24f);
                });
        }

        private void BuildLightningCorner(Vector2 min, Vector2 max)
        {
            Vector2 center = (min + max) * 0.5f;

            AddGround(min, max, (x, y) =>
            {
                float n = FractalNoise(x * 0.8f + 23f, y * 0.8f + 13f);
                Color darkStormYellow = Color.Lerp(
                    StormGroundDark,
                    PlayerYellow,
                    0.40f);
                Color lightStormYellow = Color.Lerp(
                    StormGroundLight,
                    PlayerYellow,
                    0.40f);
                return Color.Lerp(darkStormYellow, lightStormYellow, n);
            });
            AddRim(min, max, Color.Lerp(StormRimGrey, PlayerYellow, 0.60f));

            // Small puffs cradling each parked bolt, low enough that only the
            // very bottom of the token is wrapped by cloud.
            AddCelStyledGeometry(() =>
            {
                for (int index = 0; index < 4; index++)
                {
                    AddCloud(center + TokenCorner(index), 0.78f, 0.26f, 3);
                }
            });

            // Bigger banks further out, each with a bolt above it.
            Vector2 bankA = center + new Vector2(-2.20f, 1.20f);
            Vector2 bankB = center + new Vector2(0.20f, 2.20f);
            Vector2 bankC = center + new Vector2(2.30f, 0.50f);
            Vector2 bankD = center + new Vector2(-1.20f, -2.30f);
            Vector2 bankE = center + new Vector2(1.90f, -2.10f);

            AddCelStyledGeometry(() =>
            {
                AddCloud(bankA, 1.05f, 0.85f, 5, true);
                AddCloud(bankB, 1.15f, 0.95f, 5, true);
                AddCloud(bankC, 0.95f, 0.80f, 4, true);
                AddCloud(bankD, 1.05f, 0.90f, 5, true);
                AddCloud(bankE, 0.85f, 0.70f, 4, true);

                AddLightningBolt(bankA, new Vector2(-0.6f, 0.8f),
                    0.85f, 1.35f, 0.30f, 0f);
                AddLightningBolt(bankB, new Vector2(0.9f, 0.45f),
                    0.95f, 1.55f, 0.34f, 1f);
                AddLightningBolt(bankC, new Vector2(0.75f, -0.25f),
                    0.80f, 1.25f, 0.28f, 2f);
                AddLightningBolt(bankD, new Vector2(-0.3f, -0.95f),
                    0.90f, 1.20f, 0.26f, 3f);
                AddLightningBolt(bankE, new Vector2(0.55f, -0.75f),
                    0.70f, 1.10f, 0.25f, 4f);
            });
        }

        // ------------------------------------------------------------------
        // Biome pieces
        // ------------------------------------------------------------------

        private void AddVolcano(Vector2 center, float radius, float height)
        {
            float rimDepth = GroundDepth - height;
            float craterRadius = radius * 0.34f;

            AddConeSurface(center, radius, GroundDepth, RockLow,
                craterRadius * 1.35f, rimDepth, RockHigh);
            AddConeSurface(center, craterRadius * 1.35f, rimDepth, RockHigh,
                craterRadius, rimDepth + 0.05f, LavaEdge);
            AddConeSurface(center, craterRadius, rimDepth + 0.05f, LavaEdge,
                craterRadius * 0.6f, rimDepth + height * 0.16f, LavaMid);
            AddCap(center, craterRadius * 0.6f, rimDepth + height * 0.16f, LavaCore);
            RegisterSmokeEmitter(
                center,
                rimDepth + height * 0.16f - 0.04f,
                radius);
        }

        private void RegisterSmokeEmitter(
            Vector2 center,
            float baseDepth,
            float volcanoRadius)
        {
            smokeEmitterDefinitions.Add(
                new SmokeEmitterDefinition(
                    new Vector3(center.x, center.y, baseDepth),
                    volcanoRadius));
        }

        private void AddCinderCone(Vector2 center, float radius, float height)
        {
            float topDepth = GroundDepth - height;
            AddConeSurface(center, radius, GroundDepth, RockLow, radius * 0.3f, topDepth, RockHigh);
            AddCap(center, radius * 0.3f, topDepth, LavaEdge);
        }

        private void AddRock(Vector2 center, float radius, float height)
        {
            float topDepth = GroundDepth - height;
            Color low = new Color(0.28f, 0.30f, 0.32f);
            Color high = new Color(0.52f, 0.55f, 0.56f);
            AddConeSurface(center, radius, GroundDepth, low, radius * 0.45f, topDepth, high, 8);
            AddCap(center, radius * 0.45f, topDepth, high, 8);
        }

        private void AddTree(Vector2 center, float scale)
        {
            float trunkTop = GroundDepth - 0.62f * scale;
            AddConeSurface(center, 0.17f * scale, GroundDepth, TrunkDark,
                0.12f * scale, trunkTop, TrunkLight, 8);

            // Three overlapping skirts, each fading lighter toward its tip.
            AddConeSurface(center, 0.66f * scale, trunkTop + 0.10f * scale, LeafDark,
                0.34f * scale, trunkTop - 0.46f * scale, LeafMid);
            AddConeSurface(center, 0.52f * scale, trunkTop - 0.32f * scale, LeafMid,
                0.26f * scale, trunkTop - 0.84f * scale, LeafMid);
            AddConeSurface(center, 0.38f * scale, trunkTop - 0.70f * scale, LeafMid,
                0f, trunkTop - 1.28f * scale, LeafLight);
        }

        private void AddBush(Vector2 center, float radius)
        {
            AddDome(center, radius, GroundDepth, radius * 0.85f, LeafDark, LeafLight, 3, 10);
        }

        private void AddCloud(
            Vector2 center,
            float radius,
            float height,
            int puffCount,
            bool drifts = false)
        {
            float randomSeed = Frac(
                center.x * 0.073f
                + center.y * 0.113f
                + radius * 0.37f);
            float transitionSpeed = Mathf.Lerp(
                0.22f,
                0.30f,
                Frac(randomSeed * 3.71f));
            float driftRadius = drifts
                ? Mathf.Lerp(0.10f, 0.16f, Frac(randomSeed * 5.23f))
                : 0f;

            AddAnimatedGeometry(
                new Vector4(
                    CloudAnimation,
                    randomSeed,
                    transitionSpeed,
                    driftRadius),
                () =>
                {
                    AddDome(
                        center,
                        radius,
                        GroundDepth,
                        height,
                        CloudShadow,
                        CloudTop,
                        4);

                    // Satellite puffs around the main body so the silhouette
                    // stays lumpy rather than becoming a clean dome.
                    for (int index = 0; index < puffCount; index++)
                    {
                        float angle = 360f * index / puffCount + 22f;
                        Vector2 offset = PolarOffset(angle, radius * 0.72f);
                        float puffRadius = radius * Mathf.Lerp(
                            0.42f,
                            0.62f,
                            Frac(index * 0.37f + 0.2f));
                        float puffHeight = height * Mathf.Lerp(
                            0.55f,
                            0.85f,
                            Frac(index * 0.61f + 0.5f));
                        AddDome(
                            center + offset,
                            puffRadius,
                            GroundDepth,
                            puffHeight,
                            CloudShadow,
                            CloudMid,
                            3,
                            10);
                    }
                });
        }

        private void AddLightningBolt(
            Vector2 basePosition,
            Vector2 facing,
            float startHeight,
            float height,
            float width,
            float stormCloudIndex)
        {
            Vector2 direction = facing.sqrMagnitude <= Mathf.Epsilon
                ? Vector2.right
                : facing.normalized;
            Vector2 side = new Vector2(-direction.y, direction.x);

            // Zigzag in the vertical plane spanned by `direction` and depth.
            float[] alongOffsets = { 0f, 0.34f, -0.12f, 0.30f, -0.05f };
            float baseDepth = GroundDepth - startHeight;

            AddAnimatedGeometry(
                new Vector4(
                    LightningAnimation,
                    stormCloudIndex,
                    baseDepth,
                    1f / height),
                () =>
            {
                for (int segment = 0; segment < alongOffsets.Length - 1; segment++)
                {
                    float t0 = (float)segment / (alongOffsets.Length - 1);
                    float t1 = (float)(segment + 1) / (alongOffsets.Length - 1);

                    Vector2 low = basePosition + direction * alongOffsets[segment];
                    Vector2 high = basePosition + direction * alongOffsets[segment + 1];
                    float lowDepth = baseDepth - height * t0;
                    float highDepth = baseDepth - height * t1;
                    float lowWidth = width * Mathf.Lerp(1f, 0.35f, t0);
                    float highWidth = width * Mathf.Lerp(1f, 0.35f, t1);

                    Color lowColor = Color.Lerp(BoltEdge, BoltCore, t0);
                    Color highColor = Color.Lerp(BoltEdge, BoltCore, t1);

                    AddQuadColored(
                        new Vector3(low.x - side.x * lowWidth * 0.5f, low.y - side.y * lowWidth * 0.5f, lowDepth),
                        new Vector3(low.x + side.x * lowWidth * 0.5f, low.y + side.y * lowWidth * 0.5f, lowDepth),
                        new Vector3(high.x + side.x * highWidth * 0.5f, high.y + side.y * highWidth * 0.5f, highDepth),
                        new Vector3(high.x - side.x * highWidth * 0.5f, high.y - side.y * highWidth * 0.5f, highDepth),
                        lowColor, lowColor, highColor, highColor);
                }
            });
        }

        /// <summary>
        /// Lava flow drawn as a wide dim glow with a bright core on top, so
        /// the channel reads as molten rather than as a flat orange stripe.
        /// </summary>
        private void AddLavaRibbon(
            Vector2[] points,
            float width,
            bool taperStart,
            bool taperEnd)
        {
            AddAnimatedGeometry(
                new Vector4(LavaAnimation, 0f, 0f, 0f),
                () =>
                {
                    AddTaperedRibbonLayer(
                        points,
                        width * 2.9f,
                        GroundDepth - 0.006f,
                        LavaCrust,
                        taperStart,
                        taperEnd);
                    AddTaperedRibbonLayer(
                        points,
                        width * 2.0f,
                        GroundDepth - 0.012f,
                        LavaEdge,
                        taperStart,
                        taperEnd);
                    AddTaperedRibbonLayer(
                        points,
                        width * 1.12f,
                        GroundDepth - 0.020f,
                        LavaMid,
                        taperStart,
                        taperEnd);
                    AddTaperedRibbonLayer(
                        points,
                        width * 0.34f,
                        GroundDepth - 0.029f,
                        LavaCore,
                        taperStart,
                        taperEnd);
                });
        }

        private void AddTaperedRibbonLayer(
            Vector2[] points,
            float width,
            float depth,
            Color color,
            bool taperStart,
            bool taperEnd)
        {
            if (points == null || points.Length < 2)
            {
                return;
            }

            for (int index = 0; index < points.Length - 1; index++)
            {
                Vector2 current = points[index];
                Vector2 next = points[index + 1];
                Vector2 currentSide = RibbonSide(points, index);
                Vector2 nextSide = RibbonSide(points, index + 1);
                float currentWidth = LavaWidthAt(
                    index,
                    points.Length,
                    width,
                    taperStart,
                    taperEnd);
                float nextWidth = LavaWidthAt(
                    index + 1,
                    points.Length,
                    width,
                    taperStart,
                    taperEnd);

                AddQuadColored(
                    new Vector3(
                        current.x - currentSide.x * currentWidth * 0.5f,
                        current.y - currentSide.y * currentWidth * 0.5f,
                        depth),
                    new Vector3(
                        current.x + currentSide.x * currentWidth * 0.5f,
                        current.y + currentSide.y * currentWidth * 0.5f,
                        depth),
                    new Vector3(
                        next.x + nextSide.x * nextWidth * 0.5f,
                        next.y + nextSide.y * nextWidth * 0.5f,
                        depth),
                    new Vector3(
                        next.x - nextSide.x * nextWidth * 0.5f,
                        next.y - nextSide.y * nextWidth * 0.5f,
                        depth),
                    color,
                    color,
                    color,
                    color);
            }

            float startWidth = LavaWidthAt(
                0,
                points.Length,
                width,
                taperStart,
                taperEnd);
            float endWidth = LavaWidthAt(
                points.Length - 1,
                points.Length,
                width,
                taperStart,
                taperEnd);
            AddCap(points[0], startWidth * 0.5f, depth, color, 12);
            AddCap(points[points.Length - 1], endWidth * 0.5f, depth, color, 12);
        }

        private static Vector2 RibbonSide(Vector2[] points, int index)
        {
            Vector2 tangent;
            if (index <= 0)
            {
                tangent = points[1] - points[0];
            }
            else if (index >= points.Length - 1)
            {
                tangent = points[points.Length - 1] - points[points.Length - 2];
            }
            else
            {
                tangent = points[index + 1] - points[index - 1];
            }

            if (tangent.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector2.up;
            }

            tangent.Normalize();
            return new Vector2(-tangent.y, tangent.x);
        }

        private static float LavaWidthAt(
            int index,
            int pointCount,
            float width,
            bool taperStart,
            bool taperEnd)
        {
            float t = pointCount <= 1 ? 0f : (float)index / (pointCount - 1);
            float startScale = taperStart
                ? Mathf.SmoothStep(0.14f, 1f, Mathf.Clamp01(t * 4f))
                : 1f;
            float endScale = taperEnd
                ? Mathf.SmoothStep(0.14f, 1f, Mathf.Clamp01((1f - t) * 4f))
                : 1f;
            float irregularity = 1f
                + Mathf.Sin(index * 1.73f + 0.4f) * 0.10f
                + Mathf.Sin(index * 0.67f + 1.8f) * 0.055f;
            return width * Mathf.Min(startScale, endScale) * irregularity;
        }

        private void AddLavaPool(Vector2 center, float radius, float seed)
        {
            AddAnimatedGeometry(
                new Vector4(LavaAnimation, 0f, 0f, 0f),
                () =>
                {
                    Vector2 warmCenter = center
                        + PolarOffset(seed * 360f, radius * 0.055f);
                    Vector2 hotCenter = center
                        + PolarOffset((seed + 0.37f) * 360f, radius * 0.09f);

                    AddIrregularDisc(
                        center,
                        radius,
                        GroundDepth - 0.007f,
                        LavaCrust,
                        seed);
                    AddIrregularDisc(
                        warmCenter,
                        radius * 0.76f,
                        GroundDepth - 0.015f,
                        LavaEdge,
                        seed + 0.21f);
                    AddIrregularDisc(
                        hotCenter,
                        radius * 0.46f,
                        GroundDepth - 0.023f,
                        LavaMid,
                        seed + 0.46f);
                    AddIrregularDisc(
                        hotCenter + PolarOffset(seed * 270f, radius * 0.035f),
                        radius * 0.17f,
                        GroundDepth - 0.031f,
                        LavaCore,
                        seed + 0.73f);
                });
        }

        private void AddIrregularDisc(
            Vector2 center,
            float radius,
            float depth,
            Color color,
            float seed)
        {
            const int segments = 18;
            for (int segment = 0; segment < segments; segment++)
            {
                float startAngle = Mathf.PI * 2f * segment / segments;
                float endAngle = Mathf.PI * 2f * (segment + 1) / segments;
                float startRadius = IrregularPoolRadius(radius, startAngle, seed);
                float endRadius = IrregularPoolRadius(radius, endAngle, seed);

                AddTriangleColored(
                    new Vector3(center.x, center.y, depth),
                    new Vector3(
                        center.x + Mathf.Cos(startAngle) * startRadius,
                        center.y + Mathf.Sin(startAngle) * startRadius,
                        depth),
                    new Vector3(
                        center.x + Mathf.Cos(endAngle) * endRadius,
                        center.y + Mathf.Sin(endAngle) * endRadius,
                        depth),
                    color,
                    color,
                    color);
            }
        }

        private static float IrregularPoolRadius(
            float radius,
            float angle,
            float seed)
        {
            float wobble = 0.86f
                + Mathf.Sin(angle * 3f + seed * 11f) * 0.10f
                + Mathf.Sin(angle * 5f - seed * 7f) * 0.055f;
            return radius * wobble;
        }

        // ------------------------------------------------------------------
        // Ground plate
        // ------------------------------------------------------------------

        private void AddGround(Vector2 min, Vector2 max, Func<float, float, Color> colorAt)
        {
            float stepX = (max.x - min.x) / FloorSubdivisions;
            float stepY = (max.y - min.y) / FloorSubdivisions;

            for (int iy = 0; iy < FloorSubdivisions; iy++)
            {
                float y0 = min.y + iy * stepY;
                float y1 = y0 + stepY;

                for (int ix = 0; ix < FloorSubdivisions; ix++)
                {
                    float x0 = min.x + ix * stepX;
                    float x1 = x0 + stepX;

                    AddQuadColored(
                        new Vector3(x0, y0, GroundDepth),
                        new Vector3(x1, y0, GroundDepth),
                        new Vector3(x1, y1, GroundDepth),
                        new Vector3(x0, y1, GroundDepth),
                        colorAt(x0, y0), colorAt(x1, y0), colorAt(x1, y1), colorAt(x0, y1));
                }
            }
        }

        /// <summary>
        /// Raised border framing the biome, tinted with the player's color
        /// (this is what replaces the old colored home circle), plus the
        /// outer skirt that gives the whole corner plate its thickness.
        /// </summary>
        private void AddRim(Vector2 min, Vector2 max, Color rimColor)
        {
            Color wall = Shade(rimColor, 0.42f);
            Color skirt = Shade(rimColor, 0.62f);

            AddFlatQuad(new Vector2(min.x, min.y), new Vector2(max.x, min.y + RimWidth), RimTopDepth, rimColor);
            AddFlatQuad(new Vector2(min.x, max.y - RimWidth), new Vector2(max.x, max.y), RimTopDepth, rimColor);
            AddFlatQuad(new Vector2(min.x, min.y + RimWidth), new Vector2(min.x + RimWidth, max.y - RimWidth), RimTopDepth, rimColor);
            AddFlatQuad(new Vector2(max.x - RimWidth, min.y + RimWidth), new Vector2(max.x, max.y - RimWidth), RimTopDepth, rimColor);

            // Inner walls dropping from the rim down to the ground plate.
            float ix0 = min.x + RimWidth;
            float ix1 = max.x - RimWidth;
            float iy0 = min.y + RimWidth;
            float iy1 = max.y - RimWidth;

            // Match the token ink: a slim, opaque line frames both edges of
            // the coloured rim without changing its footprint or collider.
            const float inkWidth = 0.07f;
            const float inkDepthOffset = -0.012f;
            float inkDepth = RimTopDepth + inkDepthOffset;
            Color ink = LudoBoardVisualStyle.Ink;

            AddFlatQuad(
                new Vector2(min.x, min.y),
                new Vector2(max.x, min.y + inkWidth),
                inkDepth,
                ink);
            AddFlatQuad(
                new Vector2(min.x, max.y - inkWidth),
                new Vector2(max.x, max.y),
                inkDepth,
                ink);
            AddFlatQuad(
                new Vector2(min.x, min.y + inkWidth),
                new Vector2(min.x + inkWidth, max.y - inkWidth),
                inkDepth,
                ink);
            AddFlatQuad(
                new Vector2(max.x - inkWidth, min.y + inkWidth),
                new Vector2(max.x, max.y - inkWidth),
                inkDepth,
                ink);

            AddFlatQuad(
                new Vector2(ix0, iy0),
                new Vector2(ix1, iy0 + inkWidth),
                inkDepth,
                ink);
            AddFlatQuad(
                new Vector2(ix0, iy1 - inkWidth),
                new Vector2(ix1, iy1),
                inkDepth,
                ink);
            AddFlatQuad(
                new Vector2(ix0, iy0 + inkWidth),
                new Vector2(ix0 + inkWidth, iy1 - inkWidth),
                inkDepth,
                ink);
            AddFlatQuad(
                new Vector2(ix1 - inkWidth, iy0 + inkWidth),
                new Vector2(ix1, iy1 - inkWidth),
                inkDepth,
                ink);

            AddWall(new Vector2(ix0, iy0), new Vector2(ix1, iy0), RimTopDepth, GroundDepth, wall);
            AddWall(new Vector2(ix1, iy1), new Vector2(ix0, iy1), RimTopDepth, GroundDepth, wall);
            AddWall(new Vector2(ix0, iy1), new Vector2(ix0, iy0), RimTopDepth, GroundDepth, wall);
            AddWall(new Vector2(ix1, iy0), new Vector2(ix1, iy1), RimTopDepth, GroundDepth, wall);

            // Outer skirt down to the board slab.
            AddWall(new Vector2(min.x, min.y), new Vector2(max.x, min.y), RimTopDepth, SkirtBottomDepth, skirt);
            AddWall(new Vector2(max.x, max.y), new Vector2(min.x, max.y), RimTopDepth, SkirtBottomDepth, skirt);
            AddWall(new Vector2(min.x, max.y), new Vector2(min.x, min.y), RimTopDepth, SkirtBottomDepth, skirt);
            AddWall(new Vector2(max.x, min.y), new Vector2(max.x, max.y), RimTopDepth, SkirtBottomDepth, skirt);
        }

        // ------------------------------------------------------------------
        // Primitives
        // ------------------------------------------------------------------

        private void AddConeSurface(
            Vector2 center,
            float radiusA, float depthA, Color colorA,
            float radiusB, float depthB, Color colorB,
            int segments = RadialSegments)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                float startAngle = Mathf.PI * 2f * segment / segments;
                float endAngle = Mathf.PI * 2f * (segment + 1) / segments;
                Vector2 startDirection = new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle));
                Vector2 endDirection = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));

                Vector3 aStart = new Vector3(
                    center.x + startDirection.x * radiusA, center.y + startDirection.y * radiusA, depthA);
                Vector3 aEnd = new Vector3(
                    center.x + endDirection.x * radiusA, center.y + endDirection.y * radiusA, depthA);
                Vector3 bStart = new Vector3(
                    center.x + startDirection.x * radiusB, center.y + startDirection.y * radiusB, depthB);
                Vector3 bEnd = new Vector3(
                    center.x + endDirection.x * radiusB, center.y + endDirection.y * radiusB, depthB);

                if (radiusA <= 0.0001f)
                {
                    AddTriangleColored(aStart, bStart, bEnd, colorA, colorB, colorB);
                }
                else if (radiusB <= 0.0001f)
                {
                    AddTriangleColored(bStart, aStart, aEnd, colorB, colorA, colorA);
                }
                else
                {
                    AddQuadColored(aStart, bStart, bEnd, aEnd, colorA, colorB, colorB, colorA);
                }
            }
        }

        private void AddCap(Vector2 center, float radius, float depth, Color color, int segments = RadialSegments)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                float startAngle = Mathf.PI * 2f * segment / segments;
                float endAngle = Mathf.PI * 2f * (segment + 1) / segments;

                AddTriangleColored(
                    new Vector3(center.x, center.y, depth),
                    new Vector3(center.x + Mathf.Cos(startAngle) * radius, center.y + Mathf.Sin(startAngle) * radius, depth),
                    new Vector3(center.x + Mathf.Cos(endAngle) * radius, center.y + Mathf.Sin(endAngle) * radius, depth),
                    color, color, color);
            }
        }

        private void AddDome(
            Vector2 center,
            float radius,
            float baseDepth,
            float height,
            Color bottom,
            Color top,
            int bands = 4,
            int segments = RadialSegments)
        {
            for (int band = 0; band < bands; band++)
            {
                float t0 = (float)band / bands;
                float t1 = (float)(band + 1) / bands;
                float r0 = radius * Mathf.Cos(t0 * Mathf.PI * 0.5f);
                float r1 = radius * Mathf.Cos(t1 * Mathf.PI * 0.5f);
                float d0 = baseDepth - height * Mathf.Sin(t0 * Mathf.PI * 0.5f);
                float d1 = baseDepth - height * Mathf.Sin(t1 * Mathf.PI * 0.5f);

                AddConeSurface(center, r0, d0, Color.Lerp(bottom, top, t0),
                    r1, d1, Color.Lerp(bottom, top, t1), segments);
            }
        }

        private void AddRing(Vector2 center, float radius, float thickness, float depth, Color color)
        {
            AddConeSurface(center, radius + thickness * 0.5f, depth, color,
                radius - thickness * 0.5f, depth, color, 28);
        }

        private void AddFlatQuad(Vector2 min, Vector2 max, float depth, Color color)
        {
            AddQuadColored(
                new Vector3(min.x, min.y, depth),
                new Vector3(max.x, min.y, depth),
                new Vector3(max.x, max.y, depth),
                new Vector3(min.x, max.y, depth),
                color, color, color, color);
        }

        private void AddWall(Vector2 from, Vector2 to, float topDepth, float bottomDepth, Color color)
        {
            Color bottomColor = Shade(color, 0.25f);
            AddQuadColored(
                new Vector3(from.x, from.y, topDepth),
                new Vector3(to.x, to.y, topDepth),
                new Vector3(to.x, to.y, bottomDepth),
                new Vector3(from.x, from.y, bottomDepth),
                color, color, bottomColor, bottomColor);
        }

        private void AddQuadColored(
            Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Color colorA, Color colorB, Color colorC, Color colorD)
        {
            int firstVertex = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            colors.Add(colorA);
            colors.Add(colorB);
            colors.Add(colorC);
            colors.Add(colorD);
            animationData.Add(currentAnimationData);
            animationData.Add(currentAnimationData);
            animationData.Add(currentAnimationData);
            animationData.Add(currentAnimationData);

            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 2);
            triangles.Add(firstVertex + 1);
            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 3);
            triangles.Add(firstVertex + 2);

            if (captureOutlineGeometry)
            {
                AddOutlineQuad(a, b, c, d);
            }
        }

        private void AddTriangleColored(
            Vector3 a, Vector3 b, Vector3 c,
            Color colorA, Color colorB, Color colorC)
        {
            int firstVertex = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            colors.Add(colorA);
            colors.Add(colorB);
            colors.Add(colorC);
            animationData.Add(currentAnimationData);
            animationData.Add(currentAnimationData);
            animationData.Add(currentAnimationData);

            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 2);
            triangles.Add(firstVertex + 1);

            if (captureOutlineGeometry)
            {
                AddOutlineTriangle(a, b, c);
            }
        }

        private void AddOutlineQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int firstVertex = outlineVertices.Count;
            outlineVertices.Add(a);
            outlineVertices.Add(b);
            outlineVertices.Add(c);
            outlineVertices.Add(d);
            outlineAnimationData.Add(currentAnimationData);
            outlineAnimationData.Add(currentAnimationData);
            outlineAnimationData.Add(currentAnimationData);
            outlineAnimationData.Add(currentAnimationData);

            outlineTriangles.Add(firstVertex);
            outlineTriangles.Add(firstVertex + 2);
            outlineTriangles.Add(firstVertex + 1);
            outlineTriangles.Add(firstVertex);
            outlineTriangles.Add(firstVertex + 3);
            outlineTriangles.Add(firstVertex + 2);
        }

        private void AddOutlineTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            int firstVertex = outlineVertices.Count;
            outlineVertices.Add(a);
            outlineVertices.Add(b);
            outlineVertices.Add(c);
            outlineAnimationData.Add(currentAnimationData);
            outlineAnimationData.Add(currentAnimationData);
            outlineAnimationData.Add(currentAnimationData);

            outlineTriangles.Add(firstVertex);
            outlineTriangles.Add(firstVertex + 2);
            outlineTriangles.Add(firstVertex + 1);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private void AddAnimatedCelGeometry(
            Vector4 newAnimationData,
            Action buildGeometry)
        {
            AddAnimatedGeometry(
                newAnimationData,
                () => AddCelStyledGeometry(buildGeometry));
        }

        private void AddAnimatedGeometry(
            Vector4 newAnimationData,
            Action buildGeometry)
        {
            Vector4 previousAnimationData = currentAnimationData;
            currentAnimationData = newAnimationData;
            try
            {
                buildGeometry();
            }
            finally
            {
                currentAnimationData = previousAnimationData;
            }
        }

        private void AddCelStyledGeometry(Action buildGeometry)
        {
            bool previousCaptureState = captureOutlineGeometry;
            captureOutlineGeometry = true;
            try
            {
                buildGeometry();
            }
            finally
            {
                captureOutlineGeometry = previousCaptureState;
            }
        }

        /// <summary>Offset of parked token <paramref name="index"/> (0-3) from its corner center.</summary>
        private static Vector2 TokenCorner(int index)
        {
            float x = (index & 1) == 0 ? -TokenOffset : TokenOffset;
            float y = (index & 2) == 0 ? -TokenOffset : TokenOffset;
            return new Vector2(x, y);
        }

        private static Vector2 PolarOffset(float degrees, float distance)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * distance;
        }

        private static Vector2[] ArcPoints(
            Vector2 center, float radius, float startDegrees, float endDegrees, int count)
        {
            Vector2[] points = new Vector2[count];
            for (int index = 0; index < count; index++)
            {
                float t = count == 1 ? 0f : (float)index / (count - 1);
                float meander = 1f
                    + Mathf.Sin(t * Mathf.PI * 5f + 0.35f) * 0.055f
                    + Mathf.Sin(t * Mathf.PI * 11f + 1.2f) * 0.025f;
                points[index] = center + PolarOffset(
                    Mathf.Lerp(startDegrees, endDegrees, t),
                    radius * meander);
            }

            return points;
        }

        private static Vector2[] RadialSpur(
            Vector2 center, float degrees, float innerRadius, float outerRadius)
        {
            const int count = 5;
            Vector2[] points = new Vector2[count];
            for (int index = 0; index < count; index++)
            {
                float t = (float)index / (count - 1);
                // Slight sideways drift so the spur meanders instead of
                // reading as a perfectly straight spoke.
                float angle = degrees + Mathf.Sin(t * Mathf.PI) * 9f;
                points[index] = center + PolarOffset(angle, Mathf.Lerp(innerRadius, outerRadius, t));
            }

            return points;
        }

        private static float FractalNoise(float x, float y)
        {
            float n0 = Mathf.PerlinNoise(x, y);
            float n1 = Mathf.PerlinNoise(x * 2.1f + 5.3f, y * 2.1f + 7.1f);
            float n2 = Mathf.PerlinNoise(x * 4.3f + 13.7f, y * 4.3f + 11.3f);
            return Mathf.Clamp01(n0 * 0.55f + n1 * 0.30f + n2 * 0.15f);
        }

        private static float Frac(float value)
        {
            return value - Mathf.Floor(value);
        }

        private static Color Shade(Color color, float strength)
        {
            return LudoBoardVisualStyle.Shade(color, strength);
        }

        // Qualified because `using System` makes a bare `Object` ambiguous.
        private static void DestroyGeneratedObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
