using UnityEngine;

public class BackgroundMovement : MonoBehaviour
{
    [Header("Background Settings")]
    [SerializeField] private float parallaxEffectMultiplier = 0.5f; // Adjust this value to control the parallax effect

    private Transform cameraTransform;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        cameraTransform = GameObject.FindGameObjectWithTag("MainCamera").transform;
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 newPosition = new Vector3(cameraTransform.position.x * parallaxEffectMultiplier, 0f, 1f);
        transform.position = newPosition;
    }
}
