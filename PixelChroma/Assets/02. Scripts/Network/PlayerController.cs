using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

namespace PixelChroma
{
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 5f;
        public SpriteRenderer spriteRenderer;

        private InputAction moveAction;

        public override void OnStartLocalPlayer()
        {
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("Dpad")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            moveAction.Enable();
        }

        public override void OnStopLocalPlayer() { moveAction?.Disable(); }

        private void Update()
        {
            if (!isLocalPlayer) return;
            HandleMovement();
        }

        private void HandleMovement()
        {
            Vector2 input = moveAction.ReadValue<Vector2>();
            if (input == Vector2.zero) return;

            if (input.x != 0) spriteRenderer.flipX = input.x < 0;

            Vector3 moveDirection = new Vector3(input.x, input.y, 0).normalized;
            Vector3 targetPosition = transform.position + (moveDirection * moveSpeed * Time.deltaTime);

            if (CanMoveToTarget(targetPosition)) transform.position = targetPosition;
        }

        private bool CanMoveToTarget(Vector3 targetPos)
        {
            return true;
        }
    }
}