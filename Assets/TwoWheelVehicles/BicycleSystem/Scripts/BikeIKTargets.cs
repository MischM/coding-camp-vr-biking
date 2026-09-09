using UnityEngine;

namespace rayzngames
{    
    public class BikeIKTargets : MonoBehaviour
    {
        [Header("BikePositions")]
        [SerializeField] Transform handleLeft;
        [SerializeField] Transform handleRight;
        [SerializeField] Transform pedalLeft;
        [SerializeField] Transform pedalRight;

        [Header("IK Rig Targets")]
        [SerializeField] Transform leftHandTarget;
        [SerializeField] Vector3 leftHandRotation = new Vector3(0, 0, 0);
        [SerializeField] Transform rightHandTarget;
        [SerializeField] Vector3 rightHandRotation = new Vector3(0, 0, 0);
        [SerializeField] Transform leftFootTarget;
        [SerializeField] Transform rightFootTarget;

        // Update is called once per frame
        void Update()
        {
            //Hands
            leftHandTarget.position = handleLeft.position;
            leftHandTarget.localEulerAngles = handleLeft.localRotation.eulerAngles + leftHandRotation;

            rightHandTarget.position = handleRight.position;
            rightHandTarget.localEulerAngles = handleRight.localRotation.eulerAngles + rightHandRotation;

            //Feet
            leftFootTarget.position = pedalLeft.position;
            rightFootTarget.position = pedalRight.position;
        }
    }
}