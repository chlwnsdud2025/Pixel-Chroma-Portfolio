using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

namespace PixelChroma
{
    public class LobbyPlayerController : NetworkBehaviour
    {
        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveY = Animator.StringToHash("MoveY");
        private static readonly int IsMoving = Animator.StringToHash("IsMoving");

        [Header("Input")]
        public InputActionAsset inputActions;

        [Header("References")]
        public Animator animator;

        [Header("Settings")]
        public float moveSpeed = 3f;

        InputAction moveAction;

        public override void OnStartLocalPlayer()
        {
            var map = inputActions.FindActionMap("PlayerControll");
            moveAction = map.FindAction("Move");
            moveAction.Enable();
        }

        public override void OnStopLocalPlayer()
        {
            moveAction?.Disable();
        }

        void Update()
        {
            if (!isLocalPlayer) return;

            Vector2 input = moveAction.ReadValue<Vector2>();
            bool moving = input.sqrMagnitude > 0.01f;

            if (moving)
            {
                Vector3 move = new Vector3(input.x, input.y, 0).normalized;
                transform.position += move * moveSpeed * Time.deltaTime;
            }

            animator.SetBool(IsMoving, moving);
            if (moving)
            {
                animator.SetFloat(MoveX, input.x);
                animator.SetFloat(MoveY, input.y);
            }
        }
    }
}
