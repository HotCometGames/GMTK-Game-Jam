using UnityEngine;

public class CameraScript : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private float xBuffer = 6f;
    [SerializeField] private float yBuffer = 6f;
    private Transform playerTransform;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
    }

    // Update is called once per frame
    void Update()
    {
        if (playerTransform != null)
        {
            float xDistance = playerTransform.position.x - transform.position.x;
            if (Mathf.Abs(xDistance) > xBuffer)
            {
                Vector3 targetPosition = new Vector3(playerTransform.position.x - Mathf.Sign(xDistance) * xBuffer, transform.position.y, transform.position.z);
                transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
            }

            float yDistance = playerTransform.position.y - transform.position.y;
            if (Mathf.Abs(yDistance) > yBuffer)
            {
                Vector3 targetPosition = new Vector3(transform.position.x, playerTransform.position.y - Mathf.Sign(yDistance) * yBuffer, transform.position.z);
                transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
            }
        }
    }
}
