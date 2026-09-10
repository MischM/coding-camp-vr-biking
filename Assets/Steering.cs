using UnityEngine;

public class Steering : MonoBehaviour
{
    [SerializeField] private Transform controller;
    [SerializeField] private GameObject targetToMove;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        targetToMove.transform.position += controller.forward * 1.5f * Time.deltaTime;
    }

}
