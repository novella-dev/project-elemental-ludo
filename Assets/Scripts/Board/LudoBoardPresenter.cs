using UnityEngine;

namespace ElementalLudo.Board
{
    /// <summary>
    /// Puts the board into the look a mode asks for. Keeps that switching out
    /// of the game controller, which has no business knowing which renderer
    /// draws what.
    ///
    /// Classic swaps the sand surface for a plain board, brings back the
    /// colour discs in the corners, and hides the elemental biome plates.
    /// Everything else leaves the board as built.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LudoBoardPresenter : MonoBehaviour
    {
        [SerializeField] private LudoBoardRenderer surfaceRenderer;
        [SerializeField] private LudoBoardDepthRenderer depthRenderer;
        [SerializeField] private LudoBoardTerrainRenderer terrainRenderer;

        private void Awake()
        {
            ResolveMissingReferences();
        }

        public void Apply(bool classicBoard)
        {
            ResolveMissingReferences();

            if (surfaceRenderer != null)
            {
                surfaceRenderer.SetClassicBoard(classicBoard);
            }

            if (depthRenderer != null)
            {
                depthRenderer.SetShowHomeDiscs(classicBoard);
            }

            if (terrainRenderer != null)
            {
                terrainRenderer.gameObject.SetActive(!classicBoard);
            }
        }

        private void ResolveMissingReferences()
        {
            if (surfaceRenderer == null)
            {
                surfaceRenderer = FindFirstObjectByType<LudoBoardRenderer>();
            }

            if (depthRenderer == null)
            {
                depthRenderer = FindFirstObjectByType<LudoBoardDepthRenderer>();
            }

            if (terrainRenderer == null)
            {
                // Has to include inactive objects: once Classic has hidden it,
                // it would otherwise never be found again to bring back.
                terrainRenderer = FindFirstObjectByType<LudoBoardTerrainRenderer>(
                    FindObjectsInactive.Include);
            }
        }
    }
}
