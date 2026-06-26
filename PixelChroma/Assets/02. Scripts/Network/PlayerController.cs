using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public SpriteRenderer spriteRenderer;

    private InputAction moveAction;

    // [핵심] 오직 내 캐릭터일 때만 입력 활성화
    public override void OnStartLocalPlayer()
    {
        moveAction = new InputAction("Move", InputActionType.Value);

        // WASD 및 방향키 매핑 (New Input System 코드로 직접 정의)
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

    public override void OnStopLocalPlayer()
    {
        moveAction?.Disable();
    }

    private void Update()
    {
        // 내 캐릭터가 아니면 연산 중지 (NetworkTransform이 남의 위치는 알아서 잡아줌)
        if (!isLocalPlayer) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        if (input == Vector2.zero) return;

        // 좌우 반전 시각 처리
        if (input.x != 0)
        {
            spriteRenderer.flipX = input.x < 0;
        }

        Vector3 moveDirection = new Vector3(input.x, input.y, 0).normalized;
        Vector3 targetPosition = transform.position + (moveDirection * moveSpeed * Time.deltaTime);

        // 물리 연산 없는 타일 그리드용 이동 허가 검사 (임시 true)
        if (CanMoveToTarget(targetPosition))
        {
            transform.position = targetPosition;
        }
    }

    private bool CanMoveToTarget(Vector3 targetPos)
    {
        // TODO: 향후 TileGridManager 스크립트와 연동하여 이동 가능 플래그 확인
        return true;
    }
}