using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ElementalLudo.Board
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class LudoBoardOrbitCamera : MonoBehaviour
    {
        [Serializable]
        private struct CameraViewPreset
        {
            [SerializeField] private Vector3 positionOffset;
            [SerializeField] private Vector3 upDirection;
            [SerializeField] private bool orthographic;
            [Min(0.1f)]
            [SerializeField] private float orthographicSize;
            [Range(1f, 179f)]
            [SerializeField] private float fieldOfView;

            public Vector3 PositionOffset => positionOffset;
            public Vector3 UpDirection => upDirection;
            public bool Orthographic => orthographic;
            public float OrthographicSize => orthographicSize;
            public float FieldOfView => fieldOfView;

            public CameraViewPreset(
                Vector3 positionOffset,
                Vector3 upDirection,
                bool orthographic,
                float orthographicSize,
                float fieldOfView)
            {
                this.positionOffset = positionOffset;
                this.upDirection = upDirection;
                this.orthographic = orthographic;
                this.orthographicSize = orthographicSize;
                this.fieldOfView = fieldOfView;
            }
        }

        [SerializeField] private Vector3 target = Vector3.zero;

        [Header("View Presets")]
        [SerializeField] private CameraViewPreset topView = new CameraViewPreset(
            new Vector3(0f, 0f, -24f),
            Vector3.up,
            true,
            8.8f,
            37f);
        [SerializeField] private CameraViewPreset isometricView =
            new CameraViewPreset(
                new Vector3(12f, -12f, -12f),
                Vector3.back,
                true,
                8.4f,
                37f);
        [SerializeField] private CameraViewPreset threeDimensionalView =
            new CameraViewPreset(
                new Vector3(14f, -18f, -28f),
                Vector3.back,
                false,
                8.4f,
                37f);

        [Header("Orbit Controls")]
        [SerializeField] private float rotationSpeed = 0.18f;
        [SerializeField] private float zoomSpeed = 0.02f;
        [SerializeField] private float orthographicZoomSpeed = 0.006f;
        [SerializeField] private float minimumDistance = 13f;
        [SerializeField] private float maximumDistance = 45f;
        [SerializeField] private float minimumOrthographicSize = 5f;
        [SerializeField] private float maximumOrthographicSize = 14f;
        [SerializeField] private float minimumTilt = 8f;
        [SerializeField] private float maximumTilt = 70f;

        private Camera boardCamera;
        private Vector3 viewUp = Vector3.back;

        private void Awake()
        {
            boardCamera = GetComponent<Camera>();
            ShowThreeDimensionalView();
        }

        private void LateUpdate()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed)
                {
                    Orbit(mouse.delta.ReadValue());
                }

                float scroll = mouse.scroll.ReadValue().y;
                if (!Mathf.Approximately(scroll, 0f))
                {
                    Zoom(scroll);
                }
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                ShowTopView();
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                ShowIsometricView();
            }
            else if (keyboard.digit3Key.wasPressedThisFrame ||
                     keyboard.rKey.wasPressedThisFrame)
            {
                ShowThreeDimensionalView();
            }
        }

        [ContextMenu("Views/1 - Top (2D)")]
        public void ShowTopView()
        {
            ApplyView(topView);
        }

        [ContextMenu("Views/2 - Isometric (2.5D)")]
        public void ShowIsometricView()
        {
            ApplyView(isometricView);
        }

        [ContextMenu("Views/3 - Perspective (3D)")]
        public void ShowThreeDimensionalView()
        {
            ApplyView(threeDimensionalView);
        }

        private void Orbit(Vector2 pointerDelta)
        {
            Vector3 offset = transform.position - target;
            if (offset.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Quaternion yaw = Quaternion.AngleAxis(
                -pointerDelta.x * rotationSpeed,
                Vector3.forward);
            Vector3 yawedOffset = yaw * offset;

            Quaternion yawedView = Quaternion.LookRotation(
                -yawedOffset.normalized,
                viewUp);
            Vector3 pitchAxis = yawedView * Vector3.right;
            Quaternion pitch = Quaternion.AngleAxis(
                pointerDelta.y * rotationSpeed,
                pitchAxis);
            Vector3 candidateOffset = pitch * yawedOffset;

            float currentTilt = Vector3.Angle(
                yawedOffset.normalized,
                Vector3.back);
            float candidateTilt = Vector3.Angle(
                candidateOffset.normalized,
                Vector3.back);
            bool candidateIsInRange =
                candidateTilt >= minimumTilt &&
                candidateTilt <= maximumTilt;
            bool candidateMovesTowardRange =
                currentTilt < minimumTilt && candidateTilt > currentTilt ||
                currentTilt > maximumTilt && candidateTilt < currentTilt;

            if (candidateIsInRange || candidateMovesTowardRange)
            {
                offset = candidateOffset;
            }
            else
            {
                offset = yawedOffset;
            }

            transform.position = target + offset;
            LookAtBoard();
        }

        private void Zoom(float scrollDelta)
        {
            Camera cameraComponent = GetBoardCamera();
            if (cameraComponent.orthographic)
            {
                cameraComponent.orthographicSize = Mathf.Clamp(
                    cameraComponent.orthographicSize -
                    scrollDelta * orthographicZoomSpeed,
                    minimumOrthographicSize,
                    maximumOrthographicSize);
                return;
            }

            Vector3 offset = transform.position - target;
            float currentDistance = offset.magnitude;
            if (currentDistance <= Mathf.Epsilon)
            {
                return;
            }

            float nextDistance = Mathf.Clamp(
                currentDistance - scrollDelta * zoomSpeed,
                minimumDistance,
                maximumDistance);
            transform.position = target + offset.normalized * nextDistance;
            LookAtBoard();
        }

        private void ApplyView(CameraViewPreset preset)
        {
            Camera cameraComponent = GetBoardCamera();
            cameraComponent.orthographic = preset.Orthographic;
            cameraComponent.orthographicSize =
                Mathf.Max(0.1f, preset.OrthographicSize);
            cameraComponent.fieldOfView =
                Mathf.Clamp(preset.FieldOfView, 1f, 179f);

            transform.position = target + preset.PositionOffset;
            viewUp = preset.UpDirection.sqrMagnitude > Mathf.Epsilon
                ? preset.UpDirection.normalized
                : Vector3.up;
            LookAtBoard();
        }

        private void LookAtBoard()
        {
            transform.LookAt(target, viewUp);
        }

        private Camera GetBoardCamera()
        {
            if (boardCamera == null)
            {
                boardCamera = GetComponent<Camera>();
            }

            return boardCamera;
        }
    }
}
