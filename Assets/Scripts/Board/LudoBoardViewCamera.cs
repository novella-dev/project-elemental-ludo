using UnityEngine;

namespace ElementalLudo.Board
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class LudoBoardViewCamera : MonoBehaviour
    {
        [SerializeField] private Vector3 viewPosition = new Vector3(12f, -13f, -18f);
        [SerializeField] private Vector3 viewUp = Vector3.up;
        [SerializeField] private bool orthographicView = true;
        [SerializeField] private float orthographicSize = 12.2f;
        [SerializeField] private float perspectiveFieldOfView = 40f;

        private void OnEnable()
        {
            ApplyView();
        }

        private void OnValidate()
        {
            ApplyView();
        }

        [ContextMenu("Apply Board View")]
        public void ApplyView()
        {
            Camera boardCamera = GetComponent<Camera>();
            boardCamera.orthographic = orthographicView;
            boardCamera.orthographicSize = orthographicSize;
            boardCamera.fieldOfView = perspectiveFieldOfView;

            transform.position = viewPosition;
            Vector3 upDirection = viewUp.sqrMagnitude > Mathf.Epsilon
                ? viewUp.normalized
                : Vector3.up;
            transform.LookAt(Vector3.zero, upDirection);
        }
    }
}
