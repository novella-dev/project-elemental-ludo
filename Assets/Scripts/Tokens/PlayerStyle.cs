using UnityEngine;

namespace ElementalLudo.Tokens
{
    public enum TokenModelMaterialMode
    {
        Preserve,
        Opaque,
        AlphaClip
    }

    [CreateAssetMenu(
        fileName = "PlayerStyle",
        menuName = "Elemental Ludo/Player Style")]
    public sealed class PlayerStyle : ScriptableObject
    {
        [SerializeField] private string playerId = "player";
        [SerializeField] private Color tokenColor = Color.white;
        [Header("Optional token model")]
        [SerializeField] private GameObject tokenModel;
        [SerializeField] private Vector3 tokenModelEulerAngles =
            new Vector3(-90f, 0f, 0f);
        [SerializeField, Min(0.01f)] private float tokenModelFootprint = 0.68f;
        [SerializeField, Min(0.01f)] private float tokenModelHeight = 0.98f;
        [ColorUsage(false, true)]
        [SerializeField] private Color tokenModelTint = Color.white;
        [ColorUsage(false, true)]
        [SerializeField] private Color tokenModelEmission = Color.black;
        [SerializeField] private bool tokenModelOutline;
        [Tooltip("Physical outline width in board world units.")]
        [SerializeField, Range(0f, 0.08f)]
        private float tokenModelOutlineWidth = 0.018f;
        [SerializeField] private Color tokenModelOutlineColor =
            new Color(0.01f, 0.015f, 0.02f, 1f);
        [Tooltip("Runtime material treatment used to keep the model visible from every camera angle.")]
        [SerializeField]
        private TokenModelMaterialMode tokenModelMaterialMode;

        public string PlayerId => playerId;
        public Color TokenColor => tokenColor;
        public GameObject TokenModel => tokenModel;
        public Vector3 TokenModelEulerAngles => tokenModelEulerAngles;
        public float TokenModelFootprint => tokenModelFootprint;
        public float TokenModelHeight => tokenModelHeight;
        public Color TokenModelTint => tokenModelTint;
        public Color TokenModelEmission => tokenModelEmission;
        public bool TokenModelOutline => tokenModelOutline;
        public float TokenModelOutlineWidth => tokenModelOutlineWidth;
        public Color TokenModelOutlineColor => tokenModelOutlineColor;
        public TokenModelMaterialMode TokenModelMaterialMode =>
            tokenModelMaterialMode;

        public void Configure(string id, Color color)
        {
            playerId = id;
            tokenColor = color;
        }
    }
}
