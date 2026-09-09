using UnityEngine;

namespace rayzngames
{    
    public class CurveSizeAttribute : PropertyAttribute
    {
        public readonly float height;

        public CurveSizeAttribute(float height = 60f)
        {
            this.height = height;
        }
    }
}
