using UnityEngine;

namespace PixelChroma
{
    public class StartPoint : MonoBehaviour
    {

        [Header("ExitPoint 지정")]
        public Transform destination;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                TPManager.Instance.Teleport(other.transform, destination);
            }
        }
    }
}
