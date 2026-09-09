using UnityEngine;

public class raycastIncline : MonoBehaviour
{
    [SerializeField]
    private Transform frontWheel;

    [SerializeField]
    private Transform backWheel;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit))
        {
            // Berechne die Boden-Normale
            Vector3 groundNormal = hit.normal;

            // Berechne den Winkel zur vertikalen Achse
            float angle = Vector3.Angle(groundNormal, Vector3.up);

            // Hier kannst du den Winkel verwenden, um die Steigung zu bestimmen
            Debug.Log("Bodensteigung: " + angle);


        }
    }

}
