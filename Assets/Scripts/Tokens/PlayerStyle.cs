using UnityEngine;

namespace ElementalLudo.Tokens
{
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

        public string PlayerId => playerId;
        public Color TokenColor => tokenColor;
        public GameObject TokenModel => tokenModel;
        public Vector3 TokenModelEulerAngles => tokenModelEulerAngles;
        public float TokenModelFootprint => tokenModelFootprint;
        public float TokenModelHeight => tokenModelHeight;

        public void Configure(string id, Color color)
        {
            playerId = id;
            tokenColor = color;
        }
    }
}
