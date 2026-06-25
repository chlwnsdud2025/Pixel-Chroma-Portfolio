using Fusion;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    private NetworkTransform _networkTransform;
    private Animator _animator;

    
    [Networked] private Vector2 NetDirection { get; set; }
    [Networked] private NetworkBool IsMoving { get; set; }

    [Networked] private TickTimer delay { get; set; }

    private void Awake()
    {
        _networkTransform = GetComponent<NetworkTransform>();
        _animator = GetComponent<Animator>();
    }

    public override void Spawned()
    {
        // 캐릭터가 처음 생성될 때, 호스트가 기본 방향을 아래로 맞춰줍니다.
        if (HasStateAuthority)
        {
            NetDirection = Vector2.down;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            // 1. 이동 및 네트워크 상태 갱신
            if (data.direction.sqrMagnitude > 0)
            {
                data.direction.Normalize();
                transform.Translate(5 * data.direction * Runner.DeltaTime);

                // 🌟 서버(호스트)와 내 화면의 방향, 걷기 상태를 업데이트합니다.
                NetDirection = data.direction;
                IsMoving = true;
            }
            else
            {
                // 🌟 입력이 없으면 정지 상태로 업데이트합니다.
                IsMoving = false;
            }

            
        }
    }

    // 🌟 화면에 시각적인 요소(애니메이션)를 그리는 곳입니다. 
    // 내 화면뿐만 아니라 다른 사람들의 화면에서도 동일하게 실행됩니다.
    public override void Render()
    {
        if (_animator == null) return;

        // 공유받은 네트워크 변수를 애니메이터에 그대로 꽂아줍니다.
        _animator.SetBool("IsMoving", IsMoving);

        if (NetDirection.sqrMagnitude > 0)
        {
            _animator.SetFloat("DirX", NetDirection.x);
            _animator.SetFloat("DirY", NetDirection.y);
        }
    }
}