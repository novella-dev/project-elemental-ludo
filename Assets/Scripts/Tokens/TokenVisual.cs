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
        private const string LegacyPlaceholderName =
            "Fire Token (import manually)";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color NeutralColor =
            new Color32(190, 193, 193, 255);
        private static readonly HashSet<int> CleanedSceneHandles =
            new HashSet<int>();

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
        private TokenInteractionState interactionState;
        private GameObject customModelPrefab;
        private GameObject customModelInstance;
        private Renderer[] customModelRenderers;

        private MeshRenderer ProceduralRenderer => GetComponent<MeshRenderer>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSceneCleanupState()
        {
            CleanedSceneHandles.Clear();
        }

        private void OnEnable()
        {
            RemoveOrphanedSceneModels();
            RemoveOrphanedModelInstances();
            RebuildMesh();
            ApplyColor();
        }

        private void OnValidate()
        {
            RebuildMesh();
            ApplyColor();
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
                SetCustomModel(null, Vector3.zero, 1f, 1f);
            }
            else
            {
                currentColor = style.TokenColor;
                SetCustomModel(
                    style.TokenModel,
                    style.TokenModelEulerAngles,
                    style.TokenModelFootprint,
                    style.TokenModelHeight);
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
            float targetHeight)
        {
            // OnValidate also runs while Unity imports persistent prefab assets.
            // Instantiate the model only in Play mode; the procedural pawn remains
            // as the lightweight edit-mode preview.
            if (!Application.isPlaying)
            {
                return;
            }

            if (customModelPrefab == modelPrefab && customModelInstance != null)
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
            customModelPrefab = modelPrefab;
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
            FitCustomModel(targetFootprint, targetHeight);
            DisableUnownedCopiesOfCustomModel();
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
                ApplyRendererColor(proceduralRenderer, currentColor, 0);
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
                    ApplyRendererColor(
                        modelRenderer,
                        baseColor,
                        materialIndex);
                }
            }
        }

        private void ApplyRendererColor(
            Renderer targetRenderer,
            Color baseColor,
            int materialIndex)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.Clear();
            targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);
            Color displayColor = interactionState switch
            {
                TokenInteractionState.Selectable =>
                    Color.Lerp(baseColor, Color.white, 0.32f),
                TokenInteractionState.Disabled =>
                    Color.Lerp(baseColor, new Color(0.2f, 0.2f, 0.2f), 0.58f),
                _ => baseColor
            };
            propertyBlock.SetColor(BaseColorId, displayColor);
            propertyBlock.SetColor(ColorId, displayColor);
            targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
        }

        private static Color GetMaterialColor(Material material)
        {
            if (material == null)
            {
                return Color.white;
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

            customModelInstance = null;
            customModelRenderers = null;
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
