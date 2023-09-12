using UnityEngine;

namespace DynamicCollision
{
    public class DynamicShadow : MonoBehaviour
    {
        [SerializeField] private Transform master;

        private void Update()
        {
            var transShadow = transform;
            var transMaster = master.transform;
            transShadow.position = transMaster.position;
            transShadow.rotation = transMaster.rotation;
        }
    }
}