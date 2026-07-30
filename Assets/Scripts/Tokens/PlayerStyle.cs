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

        public string PlayerId => playerId;
        public Color TokenColor => tokenColor;

        public void Configure(string id, Color color)
        {
            playerId = id;
            tokenColor = color;
        }
    }
}
