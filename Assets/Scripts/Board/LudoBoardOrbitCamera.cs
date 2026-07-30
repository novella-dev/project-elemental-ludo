using UnityEngine;
using UnityEngine.InputSystem;

namespace ElementalLudo.Board
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class LudoBoardOrbitCamera : MonoBehaviour
    {
        [SerializeField] private Vector3 target = Vector3.zero;
        [SerializeField] private float rotationSpeed = 0.18f;
        [SerializeField] private float zoomSpeed = 0.02f;
        [SerializeField] private float minimumDistance = 13f;
        [SerializeField] private float maximumDistance = 45f;
        [SerializeField] private float minimumTilt = 8f;
        [SerializeField] private float maximumTilt = 70f;

        private LudoBoardViewCamera defaultView;

        private void Awake()
        {
            defaultView = GetComponent<LudoBoardViewCamera>();
            LookAtBoard();
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
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                ResetView();
            }
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
                Vector3.up);
            Vector3 pitchAxis = yawedView * Vector3.right;
            Quaternion pitch = Quaternion.AngleAxis(
                pointerDelta.y * rotationSpeed,
                pitchAxis);
            Vector3 candidateOffset = pitch * yawedOffset;

            float candidateTilt = Vector3.Angle(
                candidateOffset.normalized,
                Vector3.back);
            if (candidateTilt >= minimumTilt && candidateTilt <= maximumTilt)
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

        private void ResetView()
        {
            if (defaultView != null)
            {
                defaultView.ApplyView();
            }

            LookAtBoard();
        }

        private void LookAtBoard()
        {
            transform.LookAt(target, Vector3.up);
        }
    }
}
