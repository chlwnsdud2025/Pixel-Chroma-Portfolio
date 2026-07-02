using UnityEngine;

namespace PixelChroma
{
    public class StartPoint : MonoBehaviour
    {

        [Header("이동할 목적지")]
        public Transform destimation;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                other.transform.position = destimation.position;
                Debug.Log("플레이어 이동");
            }
        }
    }
}
