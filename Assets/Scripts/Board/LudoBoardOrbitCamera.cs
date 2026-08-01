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
            new Vector3(0f, 0f, -29f),
            Vector3.up,
            true,
            10.5f,
            37f);
        [SerializeField] private CameraViewPreset isometricView =
            new CameraViewPreset(
                new Vector3(14.5f, -14.5f, -14.5f),
                Vector3.back,
                true,
                10.2f,
                37f);
        [SerializeField] private CameraViewPreset threeDimensionalView =
            new CameraViewPreset(
                new Vector3(17f, -22f, -34f),
                Vector3.back,
                false,
                10.2f,
                37f);

        [Header("View Transition")]
        [Min(0f)]
        [Tooltip("Seconds used to blend between camera presets. Set to zero for instant changes.")]
        [SerializeField] private float transitionDuration = 0.75f;
        [Range(0f, 0.5f)]
        [Tooltip("Temporarily widens the framing near the middle of a transition to keep the board visible.")]
        [SerializeField] private float transitionFramingPadding = 0.2f;

        [Header("Orbit Controls")]
        [SerializeField] private float rotationSpeed = 0.18f;
        [SerializeField] private float zoomSpeed = 10f;
        [SerializeField] private float orthographicZoomSpeed = 3f;
        [SerializeField] private float minimumDistance = 16f;
        [SerializeField] private float maximumDistance = 54f;
        [SerializeField] private float minimumOrthographicSize = 6f;
        [SerializeField] private float maximumOrthographicSize = 18f;
        [SerializeField] private float minimumTilt = 8f;
        [SerializeField] private float maximumTilt = 70f;

        private Camera boardCamera;
        private Vector3 viewUp = Vector3.back;
        private bool transitionActive;
        private float transitionElapsed;
        private Vector3 transitionStartPosition;
        private Vector3 transitionTargetPosition;
        private Quaternion transitionStartRotation;
        private Quaternion transitionTargetRotation;
        private Matrix4x4 transitionStartProjection;
        private Matrix4x4 transitionTargetProjection;
        private float transitionStartOrthographicSize;
        private float transitionTargetOrthographicSize;
        private float transitionStartFieldOfView;
        private float transitionTargetFieldOfView;
        private bool transitionTargetOrthographic;
        private Vector3 transitionTargetUp;

        private void Awake()
        {
            boardCamera = GetComponent<Camera>();
            ApplyViewImmediately(threeDimensionalView);
        }

        private void LateUpdate()
        {
            UpdateViewTransition();

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed)
                {
                    CompleteViewTransition();
                    Orbit(mouse.delta.ReadValue());
                }

                float scroll = mouse.scroll.ReadValue().y;
                if (!Mathf.Approximately(scroll, 0f))
                {
                    CompleteViewTransition();
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
            MoveToView(topView);
        }

        [ContextMenu("Views/2 - Isometric (2.5D)")]
        public void ShowIsometricView()
        {
            MoveToView(isometricView);
        }

        [ContextMenu("Views/3 - Perspective (3D)")]
        public void ShowThreeDimensionalView()
        {
            MoveToView(threeDimensionalView);
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

        private void MoveToView(CameraViewPreset preset)
        {
            if (!Application.isPlaying || transitionDuration <= Mathf.Epsilon)
            {
                ApplyViewImmediately(preset);
                return;
            }

            BeginViewTransition(preset);
        }

        private void BeginViewTransition(CameraViewPreset preset)
        {
            Camera cameraComponent = GetBoardCamera();
            Vector3 targetUp = preset.UpDirection.sqrMagnitude > Mathf.Epsilon
                ? preset.UpDirection.normalized
                : Vector3.up;

            transitionActive = true;
            transitionElapsed = 0f;
            transitionStartPosition = transform.position;
            transitionTargetPosition = target + preset.PositionOffset;
            transitionStartRotation = transform.rotation;
            transitionTargetRotation = LookAtRotation(
                transitionTargetPosition,
                targetUp);
            transitionStartProjection = cameraComponent.projectionMatrix;
            transitionTargetProjection = GetProjectionMatrix(
                cameraComponent,
                preset);
            transitionStartOrthographicSize =
                cameraComponent.orthographicSize;
            transitionTargetOrthographicSize =
                Mathf.Max(0.1f, preset.OrthographicSize);
            transitionStartFieldOfView = cameraComponent.fieldOfView;
            transitionTargetFieldOfView =
                Mathf.Clamp(preset.FieldOfView, 1f, 179f);
            transitionTargetOrthographic = preset.Orthographic;
            transitionTargetUp = targetUp;
        }

        private void UpdateViewTransition()
        {
            if (!transitionActive)
            {
                return;
            }

            transitionElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(
                transitionElapsed / Mathf.Max(transitionDuration, 0.0001f));
            float easedProgress =
                progress * progress * (3f - 2f * progress);

            transform.position = Vector3.Lerp(
                transitionStartPosition,
                transitionTargetPosition,
                easedProgress);
            transform.rotation = Quaternion.Slerp(
                transitionStartRotation,
                transitionTargetRotation,
                easedProgress);

            Camera cameraComponent = GetBoardCamera();
            cameraComponent.orthographicSize = Mathf.Lerp(
                transitionStartOrthographicSize,
                transitionTargetOrthographicSize,
                easedProgress);
            cameraComponent.fieldOfView = Mathf.Lerp(
                transitionStartFieldOfView,
                transitionTargetFieldOfView,
                easedProgress);
            Matrix4x4 blendedProjection = LerpProjectionMatrix(
                transitionStartProjection,
                transitionTargetProjection,
                easedProgress);
            float midpointWeight =
                4f * easedProgress * (1f - easedProgress);
            float framingScale =
                1f - transitionFramingPadding * midpointWeight;
            blendedProjection.m00 *= framingScale;
            blendedProjection.m11 *= framingScale;
            cameraComponent.projectionMatrix = blendedProjection;

            if (progress >= 1f)
            {
                CompleteViewTransition();
            }
        }

        private void CompleteViewTransition()
        {
            if (!transitionActive)
            {
                return;
            }

            transitionActive = false;
            transform.position = transitionTargetPosition;
            transform.rotation = transitionTargetRotation;
            viewUp = transitionTargetUp;

            Camera cameraComponent = GetBoardCamera();
            cameraComponent.orthographic = transitionTargetOrthographic;
            cameraComponent.orthographicSize =
                transitionTargetOrthographicSize;
            cameraComponent.fieldOfView = transitionTargetFieldOfView;
            cameraComponent.ResetProjectionMatrix();
        }

        private void ApplyViewImmediately(CameraViewPreset preset)
        {
            transitionActive = false;

            Camera cameraComponent = GetBoardCamera();
            cameraComponent.orthographic = preset.Orthographic;
            cameraComponent.orthographicSize =
                Mathf.Max(0.1f, preset.OrthographicSize);
            cameraComponent.fieldOfView =
                Mathf.Clamp(preset.FieldOfView, 1f, 179f);
            cameraComponent.ResetProjectionMatrix();

            viewUp = preset.UpDirection.sqrMagnitude > Mathf.Epsilon
                ? preset.UpDirection.normalized
                : Vector3.up;
            transform.position = target + preset.PositionOffset;
            transform.rotation = LookAtRotation(transform.position, viewUp);
        }

        private Matrix4x4 GetProjectionMatrix(
            Camera cameraComponent,
            CameraViewPreset preset)
        {
            bool currentOrthographic = cameraComponent.orthographic;
            float currentOrthographicSize =
                cameraComponent.orthographicSize;
            float currentFieldOfView = cameraComponent.fieldOfView;
            Matrix4x4 currentProjection = cameraComponent.projectionMatrix;

            cameraComponent.orthographic = preset.Orthographic;
            cameraComponent.orthographicSize =
                Mathf.Max(0.1f, preset.OrthographicSize);
            cameraComponent.fieldOfView =
                Mathf.Clamp(preset.FieldOfView, 1f, 179f);
            cameraComponent.ResetProjectionMatrix();
            Matrix4x4 targetProjection = cameraComponent.projectionMatrix;

            cameraComponent.orthographic = currentOrthographic;
            cameraComponent.orthographicSize = currentOrthographicSize;
            cameraComponent.fieldOfView = currentFieldOfView;
            cameraComponent.projectionMatrix = currentProjection;
            return targetProjection;
        }

        private Quaternion LookAtRotation(
            Vector3 cameraPosition,
            Vector3 upDirection)
        {
            Vector3 lookDirection = target - cameraPosition;
            if (lookDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return transform.rotation;
            }

            return Quaternion.LookRotation(
                lookDirection.normalized,
                upDirection);
        }

        private static Matrix4x4 LerpProjectionMatrix(
            Matrix4x4 start,
            Matrix4x4 end,
            float progress)
        {
            Matrix4x4 result = new Matrix4x4();
            for (int index = 0; index < 16; index++)
            {
                result[index] = Mathf.Lerp(start[index], end[index], progress);
            }

            return result;
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

        private void OnDisable()
        {
            transitionActive = false;
            if (boardCamera != null)
            {
                boardCamera.ResetProjectionMatrix();
            }
        }
    }
}
