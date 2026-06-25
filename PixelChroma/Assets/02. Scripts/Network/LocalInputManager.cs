using Fusion;
using UnityEngine;
using UnityEngine.InputSystem; // New Input System 사용

// 지저분한 인터페이스 대신 어댑터를 상속받습니다.
public class LocalInputManager : FusionCallbackAdapter
{
    private bool _isFirePressed;
    private Vector2 _moveDirection;

    private void Update()
    {
        // 1. 단발성 입력 캐싱 (마우스 좌클릭)
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            _isFirePressed = true;
        }

        // 2. 지속성 입력 수집 (WASD 이동)
        _moveDirection = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) _moveDirection += Vector2.up;
            if (Keyboard.current.sKey.isPressed) _moveDirection += Vector2.down;
            if (Keyboard.current.aKey.isPressed) _moveDirection += Vector2.left;
            if (Keyboard.current.dKey.isPressed) _moveDirection += Vector2.right;
        }
    }

    // 3. 수많은 콜백 함수 중, 내가 필요한 딱 하나만 override(재정의) 해서 씁니다!
    public override void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();

        data.direction = _moveDirection.normalized;
        data.buttons.Set(NetworkInputData.MOUSEBUTTON0, _isFirePressed);

        // 핵심: 서버로 데이터를 넘겼으니 단발성 입력 상태는 즉시 초기화
        _isFirePressed = false;

        input.Set(data);
    }
}