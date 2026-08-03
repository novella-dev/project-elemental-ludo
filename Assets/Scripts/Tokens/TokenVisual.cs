using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ElementalLudo.Tokens
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TokenVisual : MonoBehaviour
    {
        private const int RadialSegments = 40;
        private const string GeneratedModelName = "__TokenModel";
        private const string GeneratedOutlineName = "__TokenOutline";
        private const string OutlineShaderName =
            "Elemental Ludo/Token Outline";
        private const string LegacyPlaceholderName =
            "Fire Token (import manually)";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int GltfBaseColorId =
            Shader.PropertyToID("baseColorFactor");
        private static readonly int GltfEmissiveFactorId =
            Shader.PropertyToID("emissiveFactor");
        private static readonly int EmissionColorId =
            Shader.PropertyToID("_EmissionColor");
        private static readonly int MetallicFactorId =
            Shader.PropertyToID("metallicFactor");
        private static readonly int RoughnessFactorId =
            Shader.PropertyToID("roughnessFactor");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
        private static readonly int AlphaCutoffId =
            Shader.PropertyToID("alphaCutoff");
        private static readonly int CullId = Shader.PropertyToID("_Cull");
        private static readonly int CullModeId = Shader.PropertyToID("_CullMode");
        private static readonly int BuiltInCullModeId =
            Shader.PropertyToID("_BUILTIN_CullMode");
        private static readonly int TransmissionFactorId =
            Shader.PropertyToID("transmissionFactor");
        private static readonly Color NeutralColor =
            new Color32(190, 193, 193, 255);
        private static readonly HashSet<int> CleanedSceneHandles =
            new HashSet<int>();

        [SerializeField, Min(0f)]
        [Tooltip("Rotation speed around the token's own vertical axis. Set to zero to disable spinning.")]
        private float idleSpinSpeed = 90f;

        [SerializeField, Range(0f, 20f)]
        [Tooltip("Small spinning-top lean that keeps rotation visible on symmetrical token models.")]
        private float idleSpinTilt = 8f;

        [SerializeField] private Material outlineMaterialTemplate;

        private static readonly Vector2[] Profile =
        {
            new Vector2(0.27f, 0.01f),
            new Vector2(0.33f, -0.06f),
            new Vector2(0.34f, -0.15f),
            new Vector2(0.28f, -0.22f),
            new Vector2(0.20f, -0.39f),
            new Vector2(0.15f, -0.54f),
            new Vector2(0.15f, -0.59f),
            new Vector2(0.21f, -0.62f),
            new Vector2(0.27f, -0.70f),
            new Vector2(0.29f, -0.78f),
            new Vector2(0.25f, -0.87f),
            new Vector2(0.16f, -0.94f),
            new Vector2(0.025f, -0.98f)
        };

        private readonly List<Vector3> vertices = new List<Vector3>(640);
        private readonly List<Vector2> uvs = new List<Vector2>(640);
        private readonly List<int> triangles = new List<int>(1200);

        private Mesh tokenMesh;
        private MaterialPropertyBlock propertyBlock;
        private Color currentColor = NeutralColor;
        private Color customModelTint = Color.white;
        private Color customModelEmission = Color.black;
        private TokenInteractionState interactionState;
        private GameObject customModelPrefab;
        private GameObject customModelInstance;
        private Renderer[] customModelRenderers;
        private TokenModelMaterialMode customModelMaterialMode;
        private bool customModelHasOutline;
        private float customModelOutlineWidth;
        private Color customModelOutlineColor;
        private readonly List<Material> customModelMaterials =
            new List<Material>();
        private readonly List<Mesh> customOutlineMeshes = new List<Mesh>();
        private Quaternion idleSpinBaseRotation;
        private float idleSpinAngle;
        private bool idleSpinInitialized;

        private MeshRenderer ProceduralRenderer => GetComponent<MeshRenderer>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSceneCleanupState()
        {
            CleanedSceneHandles.Clear();
        }

        private void OnEnable()
        {
            InitializeIdleSpin();
            RemoveOrphanedSceneModels();
            RebuildMesh();
            ApplyColor();
        }

        private void OnValidate()
        {
            RebuildMesh();
            ApplyColor();
        }

        private void Update()
        {
            if (!Application.isPlaying || idleSpinSpeed <= 0f)
            {
                return;
            }

            if (!idleSpinInitialized)
            {
                InitializeIdleSpin();
            }

            idleSpinAngle = Mathf.Repeat(
                idleSpinAngle + idleSpinSpeed * Time.deltaTime,
                360f);
            Quaternion spin = Quaternion.AngleAxis(
                idleSpinAngle,
                Vector3.forward);
            Quaternion tilt = Quaternion.AngleAxis(
                idleSpinTilt,
                Vector3.right);
            transform.localRotation = idleSpinBaseRotation * spin * tilt;
        }

        private void OnDisable()
        {
            if (!idleSpinInitialized)
            {
                return;
            }

            transform.localRotation = idleSpinBaseRotation;
            idleSpinInitialized = false;
        }

        private void InitializeIdleSpin()
        {
            idleSpinBaseRotation = transform.localRotation;
            idleSpinAngle = 0f;
            idleSpinInitialized = true;
        }

        [ContextMenu("Rebuild Token Mesh")]
        public void RebuildMesh()
        {
            vertices.Clear();
            uvs.Clear();
            triangles.Clear();

            for (int profileIndex = 0; profileIndex < Profile.Length; profileIndex++)
            {
                Vector2 profilePoint = Profile[profileIndex];
                float verticalUv = (float)profileIndex / (Profile.Length - 1);

                for (int segment = 0; segment < RadialSegments; segment++)
                {
                    float horizontalUv = (float)segment / RadialSegments;
                    float angle = horizontalUv * Mathf.PI * 2f;
                    vertices.Add(new Vector3(
                        Mathf.Cos(angle) * profilePoint.x,
                        Mathf.Sin(angle) * profilePoint.x,
                        profilePoint.y));
                    uvs.Add(new Vector2(horizontalUv, verticalUv));
                }
            }

            for (int profileIndex = 0; profileIndex < Profile.Length - 1; profileIndex++)
            {
                int currentRing = profileIndex * RadialSegments;
                int nextRing = (profileIndex + 1) * RadialSegments;

                for (int segment = 0; segment < RadialSegments; segment++)
                {
                    int nextSegment = (segment + 1) % RadialSegments;
                    int current = currentRing + segment;
                    int currentNext = currentRing + nextSegment;
                    int above = nextRing + segment;
                    int aboveNext = nextRing + nextSegment;

                    triangles.Add(current);
                    triangles.Add(aboveNext);
                    triangles.Add(above);
                    triangles.Add(current);
                    triangles.Add(currentNext);
                    triangles.Add(aboveNext);
                }
            }

            if (tokenMesh == null)
            {
                tokenMesh = new Mesh
                {
                    name = "Procedural Ludo Token",
                    hideFlags = HideFlags.DontSave
                };
            }
            else
            {
                tokenMesh.Clear();
            }

            tokenMesh.SetVertices(vertices);
            tokenMesh.SetUVs(0, uvs);
            tokenMesh.SetTriangles(triangles, 0);
            tokenMesh.RecalculateNormals();
            tokenMesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = tokenMesh;

            MeshRenderer meshRenderer = ProceduralRenderer;
            meshRenderer.enabled = customModelPrefab == null;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        public void SetStyle(PlayerStyle style)
        {
            if (style == null)
            {
                currentColor = NeutralColor;
                customModelTint = Color.white;
                customModelEmission = Color.black;
                SetCustomModel(
                    null,
                    Vector3.zero,
                    1f,
                    1f,
                    TokenModelMaterialMode.Preserve,
                    false,
                    0f,
                    Color.black);
            }
            else
            {
                currentColor = style.TokenColor;
                customModelTint = style.TokenModelTint;
                customModelEmission = style.TokenModelEmission;
                SetCustomModel(
                    style.TokenModel,
                    style.TokenModelEulerAngles,
                    style.TokenModelFootprint,
                    style.TokenModelHeight,
                    style.TokenModelMaterialMode,
                    style.TokenModelOutline,
                    style.TokenModelOutlineWidth,
                    style.TokenModelOutlineColor);
            }

            ApplyColor();
        }

        public void SetColor(Color color)
        {
            currentColor = color;
            ApplyColor();
        }

        public void UseNeutralColor()
        {
            currentColor = NeutralColor;
            ApplyColor();
        }

        public void SetInteractionState(TokenInteractionState state)
        {
            interactionState = state;
            ApplyColor();
        }

        private void SetCustomModel(
            GameObject modelPrefab,
            Vector3 eulerAngles,
            float targetFootprint,
            float targetHeight,
            TokenModelMaterialMode materialMode,
            bool hasOutline,
            float outlineWidth,
            Color outlineColor)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (customModelPrefab == modelPrefab &&
                customModelInstance != null &&
                customModelMaterialMode == materialMode &&
                customModelHasOutline == hasOutline &&
                Mathf.Approximately(customModelOutlineWidth, outlineWidth) &&
                customModelOutlineColor == outlineColor)
            {
                customModelInstance.transform.localPosition = Vector3.zero;
                customModelInstance.transform.localRotation =
                    Quaternion.Euler(eulerAngles);
                customModelInstance.transform.localScale = Vector3.one;
                FitCustomModel(targetFootprint, targetHeight);
                DisableUnownedCopiesOfCustomModel();
                return;
            }

            DestroyCustomModelInstance();
            RemoveOrphanedModelInstances();
            customModelPrefab = modelPrefab;
            customModelMaterialMode = materialMode;
            customModelHasOutline = hasOutline;
            customModelOutlineWidth = outlineWidth;
            customModelOutlineColor = outlineColor;
            ProceduralRenderer.enabled = modelPrefab == null;

            if (modelPrefab == null)
            {
                return;
            }

            customModelInstance = Instantiate(modelPrefab, transform, false);
            customModelInstance.name = GeneratedModelName;

            Transform modelTransform = customModelInstance.transform;
            modelTransform.localPosition = Vector3.zero;
            modelTransform.localRotation = Quaternion.Euler(eulerAngles);
            modelTransform.localScale = Vector3.one;

            SetLayerRecursively(customModelInstance, gameObject.layer);
            DisableModelColliders(customModelInstance);
            customModelRenderers =
                customModelInstance.GetComponentsInChildren<Renderer>(true);
            if (materialMode != TokenModelMaterialMode.Preserve)
            {
                ConfigureCustomModelMaterials(materialMode);
            }

            FitCustomModel(targetFootprint, targetHeight);
            if (hasOutline)
            {
                CreateCustomModelOutlines(outlineWidth, outlineColor);
            }

            DisableUnownedCopiesOfCustomModel();
        }

        private void CreateCustomModelOutlines(
            float outlineWidth,
            Color outlineColor)
        {
            Material outlineMaterial;
            if (outlineMaterialTemplate != null)
            {
                outlineMaterial = new Material(outlineMaterialTemplate);
            }
            else
            {
                Shader outlineShader = Shader.Find(OutlineShaderName);
                if (outlineShader == null)
                {
                    Debug.LogWarning(
                        $"Token outline shader '{OutlineShaderName}' was not found.",
                        this);
                    return;
                }

                outlineMaterial = new Material(outlineShader);
            }

            outlineMaterial.name = "Token Outline (Runtime)";
            outlineMaterial.hideFlags = HideFlags.DontSave;
            outlineMaterial.SetColor("_OutlineColor", outlineColor);
            outlineMaterial.SetFloat(
                "_OutlineWidth",
                Mathf.Clamp(outlineWidth, 0f, 0.08f));
            customModelMaterials.Add(outlineMaterial);

            foreach (Renderer sourceRenderer in customModelRenderers)
            {
                if (sourceRenderer is MeshRenderer meshRenderer)
                {
                    CreateMeshOutline(meshRenderer, outlineMaterial);
                }
                else if (sourceRenderer is SkinnedMeshRenderer skinnedRenderer)
                {
                    CreateSkinnedMeshOutline(
                        skinnedRenderer,
                        outlineMaterial);
                }
            }
        }

        private void CreateMeshOutline(
            MeshRenderer sourceRenderer,
            Material outlineMaterial)
        {
            MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                return;
            }

            GameObject outlineObject = CreateOutlineObject(sourceRenderer);
            MeshFilter outlineFilter = outlineObject.AddComponent<MeshFilter>();
            outlineFilter.sharedMesh = CreateOutlineMesh(sourceFilter.sharedMesh);
            MeshRenderer outlineRenderer =
                outlineObject.AddComponent<MeshRenderer>();
            outlineRenderer.sharedMaterials = CreateOutlineMaterials(
                outlineMaterial,
                sourceFilter.sharedMesh.subMeshCount);
            ConfigureOutlineRenderer(outlineRenderer, sourceRenderer);
        }

        private void CreateSkinnedMeshOutline(
            SkinnedMeshRenderer sourceRenderer,
            Material outlineMaterial)
        {
            if (sourceRenderer.sharedMesh == null)
            {
                return;
            }

            GameObject outlineObject = CreateOutlineObject(sourceRenderer);
            SkinnedMeshRenderer outlineRenderer =
                outlineObject.AddComponent<SkinnedMeshRenderer>();
            outlineRenderer.sharedMesh = CreateOutlineMesh(
                sourceRenderer.sharedMesh);
            outlineRenderer.bones = sourceRenderer.bones;
            outlineRenderer.rootBone = sourceRenderer.rootBone;
            outlineRenderer.localBounds = sourceRenderer.localBounds;
            outlineRenderer.updateWhenOffscreen =
                sourceRenderer.updateWhenOffscreen;
            outlineRenderer.sharedMaterials = CreateOutlineMaterials(
                outlineMaterial,
                sourceRenderer.sharedMesh.subMeshCount);
            ConfigureOutlineRenderer(outlineRenderer, sourceRenderer);
        }

        private Mesh CreateOutlineMesh(Mesh sourceMesh)
        {
            if (!sourceMesh.isReadable)
            {
                Debug.LogWarning(
                    $"Cannot smooth the outline of unreadable mesh " +
                    $"'{sourceMesh.name}'.",
                    this);
                return sourceMesh;
            }

            Mesh outlineMesh = Instantiate(sourceMesh);
            outlineMesh.name = $"{sourceMesh.name} (Outline)";
            outlineMesh.hideFlags = HideFlags.DontSave;
            SmoothSharedVertexNormals(outlineMesh);
            customOutlineMeshes.Add(outlineMesh);
            return outlineMesh;
        }

        private static void SmoothSharedVertexNormals(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            if (vertices.Length == 0 || normals.Length != vertices.Length)
            {
                return;
            }

            Dictionary<Vector3, Vector3> normalSums =
                new Dictionary<Vector3, Vector3>(vertices.Length);
            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 position = vertices[index];
                normalSums.TryGetValue(position, out Vector3 normalSum);
                normalSums[position] = normalSum + normals[index];
            }

            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 normalSum = normalSums[vertices[index]];
                if (normalSum.sqrMagnitude > 0.000001f)
                {
                    normals[index] = normalSum.normalized;
                }
            }

            mesh.normals = normals;
        }

        private static GameObject CreateOutlineObject(Renderer sourceRenderer)
        {
            GameObject outlineObject = new GameObject(GeneratedOutlineName)
            {
                hideFlags = HideFlags.DontSave,
                layer = sourceRenderer.gameObject.layer
            };
            outlineObject.transform.SetParent(sourceRenderer.transform, false);
            return outlineObject;
        }

        private static Material[] CreateOutlineMaterials(
            Material outlineMaterial,
            int subMeshCount)
        {
            int materialCount = Mathf.Max(1, subMeshCount);
            Material[] materials = new Material[materialCount];
            for (int index = 0; index < materialCount; index++)
            {
                materials[index] = outlineMaterial;
            }

            return materials;
        }

        private static void ConfigureOutlineRenderer(
            Renderer outlineRenderer,
            Renderer sourceRenderer)
        {
            outlineRenderer.enabled = sourceRenderer.enabled;
            outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineRenderer.lightProbeUsage = LightProbeUsage.Off;
            outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            outlineRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            outlineRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
        }

        private void ConfigureCustomModelMaterials(
            TokenModelMaterialMode materialMode)
        {
            if (customModelRenderers == null)
            {
                return;
            }

            foreach (Renderer modelRenderer in customModelRenderers)
            {
                Material[] sourceMaterials = modelRenderer.sharedMaterials;
                Material[] runtimeMaterials =
                    new Material[sourceMaterials.Length];
                for (int materialIndex = 0;
                     materialIndex < sourceMaterials.Length;
                     materialIndex++)
                {
                    Material sourceMaterial = sourceMaterials[materialIndex];
                    if (sourceMaterial == null)
                    {
                        continue;
                    }

                    Material runtimeMaterial = new Material(sourceMaterial)
                    {
                        name = $"{sourceMaterial.name} ({materialMode} Token)",
                        hideFlags = HideFlags.DontSave
                    };
                    if (materialMode == TokenModelMaterialMode.AlphaClip)
                    {
                        ConfigureAlphaClipMaterial(runtimeMaterial);
                    }
                    else
                    {
                        ConfigureOpaqueMaterial(runtimeMaterial);
                    }

                    ConfigureDoubleSidedMaterial(runtimeMaterial);
                    ConfigureCartoonSurface(runtimeMaterial);
                    runtimeMaterials[materialIndex] = runtimeMaterial;
                    customModelMaterials.Add(runtimeMaterial);
                }

                modelRenderer.sharedMaterials = runtimeMaterials;
            }
        }

        private static void ConfigureOpaqueMaterial(Material material)
        {
            material.DisableKeyword("_TRANSMISSION");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Opaque");
            SetMaterialFloatIfPresent(material, TransmissionFactorId, 0f);
            SetMaterialFloatIfPresent(material, SurfaceId, 0f);
            SetMaterialFloatIfPresent(material, AlphaClipId, 0f);
            SetMaterialFloatIfPresent(material, ZWriteId, 1f);
            SetMaterialFloatIfPresent(
                material,
                SrcBlendId,
                (float)BlendMode.One);
            SetMaterialFloatIfPresent(
                material,
                DstBlendId,
                (float)BlendMode.Zero);
            material.SetShaderPassEnabled("DepthOnly", true);
            material.renderQueue = (int)RenderQueue.Geometry;
        }

        private static void ConfigureAlphaClipMaterial(Material material)
        {
            material.DisableKeyword("_TRANSMISSION");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            SetMaterialFloatIfPresent(material, TransmissionFactorId, 0f);
            SetMaterialFloatIfPresent(material, SurfaceId, 0f);
            SetMaterialFloatIfPresent(material, AlphaClipId, 1f);
            SetMaterialFloatIfPresent(material, AlphaCutoffId, 0.1f);
            SetMaterialFloatIfPresent(material, ZWriteId, 1f);
            SetMaterialFloatIfPresent(
                material,
                SrcBlendId,
                (float)BlendMode.One);
            SetMaterialFloatIfPresent(
                material,
                DstBlendId,
                (float)BlendMode.Zero);
            material.SetShaderPassEnabled("DepthOnly", true);
            material.renderQueue = (int)RenderQueue.AlphaTest;
        }

        private static void ConfigureDoubleSidedMaterial(Material material)
        {
            SetMaterialFloatIfPresent(material, CullId, (float)CullMode.Off);
            SetMaterialFloatIfPresent(material, CullModeId, (float)CullMode.Off);
            SetMaterialFloatIfPresent(
                material,
                BuiltInCullModeId,
                (float)CullMode.Off);
            material.doubleSidedGI = true;
        }

        private static void ConfigureCartoonSurface(Material material)
        {
            SetMaterialFloatIfPresent(material, MetallicFactorId, 0f);
            SetMaterialFloatIfPresent(material, RoughnessFactorId, 0.6f);
            material.EnableKeyword("_EMISSION");
        }

        private static void SetMaterialFloatIfPresent(
            Material material,
            int propertyId,
            float value)
        {
            if (material.HasProperty(propertyId))
            {
                material.SetFloat(propertyId, value);
            }
        }

        private void FitCustomModel(float targetFootprint, float targetHeight)
        {
            if (!TryGetCustomModelBounds(out Bounds modelBounds))
            {
                Debug.LogWarning(
                    $"Token model '{customModelPrefab.name}' has no renderers.",
                    this);
                return;
            }

            float footprint = Mathf.Max(modelBounds.size.x, modelBounds.size.y);
            float height = modelBounds.size.z;
            if (footprint <= Mathf.Epsilon || height <= Mathf.Epsilon)
            {
                Debug.LogWarning(
                    $"Token model '{customModelPrefab.name}' has invalid bounds.",
                    this);
                return;
            }

            float scale = Mathf.Min(
                Mathf.Max(0.01f, targetFootprint) / footprint,
                Mathf.Max(0.01f, targetHeight) / height);
            customModelInstance.transform.localScale = Vector3.one * scale;

            if (!TryGetCustomModelBounds(out modelBounds))
            {
                return;
            }

            customModelInstance.transform.localPosition = new Vector3(
                -modelBounds.center.x,
                -modelBounds.center.y,
                -modelBounds.max.z);
        }

        private bool TryGetCustomModelBounds(out Bounds result)
        {
            result = default;
            bool hasBounds = false;
            if (customModelRenderers == null)
            {
                return false;
            }

            foreach (Renderer modelRenderer in customModelRenderers)
            {
                if (modelRenderer == null)
                {
                    continue;
                }

                Bounds rendererBounds = modelRenderer.localBounds;
                Matrix4x4 toTokenSpace =
                    transform.worldToLocalMatrix * modelRenderer.localToWorldMatrix;
                Vector3 center = rendererBounds.center;
                Vector3 extents = rendererBounds.extents;

                for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
                {
                    Vector3 corner = center + new Vector3(
                        (cornerIndex & 1) == 0 ? -extents.x : extents.x,
                        (cornerIndex & 2) == 0 ? -extents.y : extents.y,
                        (cornerIndex & 4) == 0 ? -extents.z : extents.z);
                    Vector3 tokenSpaceCorner =
                        toTokenSpace.MultiplyPoint3x4(corner);

                    if (!hasBounds)
                    {
                        result = new Bounds(tokenSpaceCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        result.Encapsulate(tokenSpaceCorner);
                    }
                }
            }

            return hasBounds;
        }

        private void ApplyColor()
        {
            MeshRenderer proceduralRenderer = ProceduralRenderer;
            if (proceduralRenderer != null && proceduralRenderer.enabled)
            {
                ApplyRendererColor(
                    proceduralRenderer,
                    currentColor,
                    Color.black,
                    0);
            }

            if (customModelRenderers == null)
            {
                return;
            }

            foreach (Renderer modelRenderer in customModelRenderers)
            {
                if (modelRenderer == null)
                {
                    continue;
                }

                Material[] materials = modelRenderer.sharedMaterials;
                for (int materialIndex = 0;
                     materialIndex < materials.Length;
                     materialIndex++)
                {
                    Material material = materials[materialIndex];
                    Color baseColor = GetMaterialColor(material);
                    Color tinted = baseColor * customModelTint;
                    ApplyRendererColor(
                        modelRenderer,
                        tinted,
                        customModelEmission,
                        materialIndex);
                }
            }
        }

        private void ApplyRendererColor(
            Renderer targetRenderer,
            Color baseColor,
            Color emissionColor,
            int materialIndex)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.Clear();
            targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);
            Color displayColor = interactionState switch
            {
                TokenInteractionState.Selected =>
                    Color.Lerp(baseColor, Color.white, 0.55f),
                TokenInteractionState.Selectable =>
                    Color.Lerp(baseColor, Color.white, 0.32f),
                TokenInteractionState.Disabled =>
                    Color.Lerp(baseColor, new Color(0.2f, 0.2f, 0.2f), 0.58f),
                _ => baseColor
            };
            propertyBlock.SetColor(BaseColorId, displayColor);
            propertyBlock.SetColor(ColorId, displayColor);
            propertyBlock.SetColor(GltfBaseColorId, displayColor);
            Color displayEmission = interactionState switch
            {
                // The baseColor term matters: some styles ship no emission at
                // all (red is pure black), and those still have to glow.
                TokenInteractionState.Selected =>
                    emissionColor * 2.2f + baseColor * 0.45f,
                TokenInteractionState.Disabled => emissionColor * 0.15f,
                _ => emissionColor
            };
            propertyBlock.SetColor(
                GltfEmissiveFactorId,
                displayEmission);
            propertyBlock.SetColor(EmissionColorId, displayEmission);
            targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
        }

        private static Color GetMaterialColor(Material material)
        {
            if (material == null)
            {
                return Color.white;
            }

            if (material.HasProperty(GltfBaseColorId))
            {
                return material.GetColor(GltfBaseColorId);
            }

            if (material.HasProperty(BaseColorId))
            {
                return material.GetColor(BaseColorId);
            }

            return material.HasProperty(ColorId)
                ? material.GetColor(ColorId)
                : Color.white;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static void DisableModelColliders(GameObject root)
        {
            foreach (Collider modelCollider in
                     root.GetComponentsInChildren<Collider>(true))
            {
                modelCollider.enabled = false;
            }
        }

        private void DisableUnownedCopiesOfCustomModel()
        {
            if (customModelRenderers == null || customModelRenderers.Length == 0)
            {
                return;
            }

            HashSet<Mesh> customMeshes = new HashSet<Mesh>();
            foreach (Renderer modelRenderer in customModelRenderers)
            {
                Mesh mesh = GetRendererMesh(modelRenderer);
                if (mesh != null)
                {
                    customMeshes.Add(mesh);
                }
            }

            if (customMeshes.Count == 0)
            {
                return;
            }

            int disabledCount = 0;
            foreach (Renderer sceneRenderer in FindObjectsByType<Renderer>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (sceneRenderer == null ||
                    sceneRenderer.gameObject.scene != gameObject.scene ||
                    !customMeshes.Contains(GetRendererMesh(sceneRenderer)))
                {
                    continue;
                }

                TokenVisual owner =
                    sceneRenderer.GetComponentInParent<TokenVisual>(true);
                bool belongsToActiveModel =
                    owner != null &&
                    owner.customModelInstance != null &&
                    (sceneRenderer.transform ==
                         owner.customModelInstance.transform ||
                     sceneRenderer.transform.IsChildOf(
                         owner.customModelInstance.transform));
                if (belongsToActiveModel)
                {
                    continue;
                }

                sceneRenderer.enabled = false;
                disabledCount++;
            }

            if (disabledCount > 0)
            {
                Debug.Log(
                    $"Disabled {disabledCount} unowned renderer copy/copies " +
                    $"of token model '{customModelPrefab.name}'.");
            }
        }

        private static Mesh GetRendererMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                return skinnedMeshRenderer.sharedMesh;
            }

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            return meshFilter == null ? null : meshFilter.sharedMesh;
        }

        private void RemoveOrphanedModelInstances()
        {
            for (int childIndex = transform.childCount - 1;
                 childIndex >= 0;
                 childIndex--)
            {
                GameObject child = transform.GetChild(childIndex).gameObject;
                if (child.name != GeneratedModelName)
                {
                    continue;
                }

                DestroyGeneratedObject(child);
            }

            customModelInstance = null;
            customModelRenderers = null;
        }

        private void RemoveOrphanedSceneModels()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Scene scene = gameObject.scene;
            if (!scene.IsValid() || !CleanedSceneHandles.Add(scene.handle))
            {
                return;
            }

            int removedCount = 0;
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                bool isLegacyPlaceholder =
                    rootObject.name == LegacyPlaceholderName;
                bool isOrphanedGeneratedModel =
                    rootObject.name == GeneratedModelName &&
                    rootObject.GetComponentInChildren<TokenVisual>(true) == null;
                if (!isLegacyPlaceholder && !isOrphanedGeneratedModel)
                {
                    continue;
                }

                removedCount++;
                Destroy(rootObject);
            }

            if (removedCount > 0)
            {
                Debug.Log(
                    $"Removed {removedCount} orphaned token model(s) from " +
                    $"scene '{scene.name}'.");
            }
        }

        private void DestroyCustomModelInstance()
        {
            if (customModelInstance != null)
            {
                DestroyGeneratedObject(customModelInstance);
            }

            foreach (Material customMaterial in customModelMaterials)
            {
                DestroyGeneratedObject(customMaterial);
            }

            customModelMaterials.Clear();

            foreach (Mesh outlineMesh in customOutlineMeshes)
            {
                DestroyGeneratedObject(outlineMesh);
            }

            customOutlineMeshes.Clear();

            customModelInstance = null;
            customModelRenderers = null;
            customModelMaterialMode = TokenModelMaterialMode.Preserve;
            customModelHasOutline = false;
            customModelOutlineWidth = 0f;
            customModelOutlineColor = Color.black;
        }

        private static void DestroyGeneratedObject(Object target)
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

        private void OnDestroy()
        {
            DestroyCustomModelInstance();
            if (tokenMesh == null)
            {
                return;
            }

            DestroyGeneratedObject(tokenMesh);
            tokenMesh = null;
        }
    }
}
