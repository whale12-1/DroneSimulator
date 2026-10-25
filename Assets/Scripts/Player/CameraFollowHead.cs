using UnityEngine;

public class CameraFollowHead : MonoBehaviour
{
    public Transform headBone;
    public Vector3 offset = new Vector3(0f, 0.05f, 0.12f);

    void LateUpdate()
    {
        if (headBone != null)
        {
            transform.position = headBone.position + headBone.forward * offset.z + headBone.up * offset.y;
        }
    }
}