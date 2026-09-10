using UnityEngine;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;

public class RotateHandlebar : MonoBehaviour
{
    [SerializeField] public Lenken _lenken;

    // Update is called once per frame
    void Update()
    {
        //transform.Rotate(transform.rotation.x, _lenken.GetLenkwinkel(), transform.rotation.z);
        float currentY = transform.localEulerAngles.y;
        float delta = Mathf.DeltaAngle(currentY, _lenken.GetLenkwinkel());
        transform.Rotate(Vector3.up, delta, Space.Self);
    }
}
