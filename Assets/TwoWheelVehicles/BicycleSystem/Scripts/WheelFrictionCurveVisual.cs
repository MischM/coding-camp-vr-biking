using UnityEngine;

namespace rayzngames
{        
    [ExecuteInEditMode]
    public class WheelFrictionCurveVisual : MonoBehaviour
    {
        [SerializeField] WheelCollider front;           

        [CurveSize(75)]  
        public AnimationCurve front_Forward_FrictionCurve;
        
        [CurveSize(75)]  
        public AnimationCurve front_Side_FrictionCurve;
        [SerializeField] WheelCollider rear;

        [CurveSize(75)]  
        public AnimationCurve rear_Forward_FrictionCurve;

        [CurveSize(75)]  
        public AnimationCurve rear_Side_FrictionCurve;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            CreateCurves();  
        }  
        void CreateCurves()
        {          
            DisplayWheelFrictionCurve();
        }
        public void DisplayWheelFrictionCurve()
        {
            //Front
            WheelFrictionCurve fc;
            if (front != null)
            {
                //Prepare Front FWD Friction curve:        
                fc =  front.forwardFriction;
                front_Forward_FrictionCurve = GetCurve(fc);

                //Prepare Front Side Friction Curve
                fc = front.sidewaysFriction;
                front_Side_FrictionCurve = GetCurve(fc);
            }
            if (rear != null)
            {
                //Rear
                //Prepare Rear FWD Friction Curve
                fc = rear.forwardFriction;
                rear_Forward_FrictionCurve = GetCurve(fc);

                //Prepare Rear Side Friction Curve
                fc = rear.sidewaysFriction;
                rear_Side_FrictionCurve = GetCurve(fc);
            }
        }

        AnimationCurve GetCurve (WheelFrictionCurve fc)
        { 
            Keyframe[] curvekeys = new Keyframe[3];
            curvekeys[0] = new Keyframe(0, 0);
            //extremium
            curvekeys[1] = new Keyframe(fc.extremumSlip, fc.extremumValue);
            //asymptote
            curvekeys[2] = new Keyframe(fc.asymptoteSlip, fc.asymptoteValue);
        // Debug.Log(curvekeys.Length);       
            return  new AnimationCurve(curvekeys);
        }
    }
}
