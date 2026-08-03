using ElementalLudo.Tokens;
using UnityEngine;

namespace ElementalLudo.Board
{
    /// <summary>
    /// Puts the board and tokens into the look a mode asks for. Keeps that
    /// switching out of the game controller, which has no business knowing
    /// which renderer draws what or how a token picks its model.
    ///
    /// Classic swaps the sand surface for a plain board, brings back the
    /// colour discs in the corners, hides the elemental biome plates, and
    /// turns every token back into the plain lathed pawn instead of its
    /// elemental model. Everything else leaves the board and tokens as built.
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

            ApplyTokenStyle(classicBoard);
        }

        /// <summary>
        /// Includes inactive tokens, since Hardcore hides eliminated ones and
        /// a mode switch shouldn't leave those stuck on the elemental model.
        /// </summary>
        private static void ApplyTokenStyle(bool classicBoard)
        {
            bool useElementalModel = !classicBoard;
            foreach (Token token in FindObjectsByType<Token>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                token.SetUseElementalModel(useElementalModel);
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
