using UnityEngine;

namespace ElementalLudo.Board
{
    /// <summary>
    /// Parks the board's camera while something else owns the screen, and puts
    /// it back exactly as it was.
    ///
    /// Turning off <see cref="LudoBoardOrbitCamera"/> matters as much as the
    /// camera itself: it reads the mouse in LateUpdate regardless of whether
    /// its camera is rendering, so without this every drag, scroll and number
    /// key aimed at whatever took over the screen also quietly re-aimed the
    /// board behind it — which is why the board used to come back looking
    /// wrong. That fix is the reason this lives in one place instead of being
    /// copied into each view that needs it.
    /// </summary>
    public sealed class LudoBoardCameraSuspender
    {
        private Camera suspended;
        private LudoBoardOrbitCamera suspendedOrbit;
        private Vector3 position;
        private Quaternion rotation;
        private bool orthographic;
        private float orthographicSize;
        private float fieldOfView;

        public bool IsSuspended => suspended != null;

        /// <summary>
        /// Takes over from the main camera. Does nothing if already holding it,
        /// so calling this on every show is safe.
        /// </summary>
        /// <param name="taker">
        /// The camera about to render instead, so the board's own is never
        /// mistaken for it.
        /// </param>
        public void Suspend(Camera taker)
        {
            if (suspended != null)
            {
                return;
            }

            Camera boardCamera = Camera.main;
            if (boardCamera == null || boardCamera == taker)
            {
                return;
            }

            suspended = boardCamera;
            position = boardCamera.transform.position;
            rotation = boardCamera.transform.rotation;
            orthographic = boardCamera.orthographic;
            orthographicSize = boardCamera.orthographicSize;
            fieldOfView = boardCamera.fieldOfView;

            suspendedOrbit = boardCamera.GetComponent<LudoBoardOrbitCamera>();
            if (suspendedOrbit != null)
            {
                suspendedOrbit.enabled = false;
            }

            // Two enabled cameras would both render.
            suspended.enabled = false;
        }

        public void Restore()
        {
            if (suspended == null)
            {
                return;
            }

            // Put the view back before handing control over, so the orbit
            // script picks up from where the player left the board rather than
            // from wherever it happened to be.
            suspended.transform.SetPositionAndRotation(position, rotation);
            suspended.orthographic = orthographic;
            suspended.orthographicSize = orthographicSize;
            suspended.fieldOfView = fieldOfView;
            suspended.ResetProjectionMatrix();
            suspended.enabled = true;

            if (suspendedOrbit != null)
            {
                suspendedOrbit.enabled = true;
                suspendedOrbit = null;
            }

            suspended = null;
        }
    }
}
