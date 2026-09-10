using UnityEngine;
using UnityEngine.Events;


[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Tag used to identify the bicycle/player object.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("If true, the checkpoint can only be triggered once. Otherwise it resets and can be reused.")]
    [SerializeField] private bool oneTimeUse = true;

    [Header("Pizza Box")]
    [Tooltip("Tag used to find the pizza box object on the bicycle (searched in children of whatever entered the trigger). Leave empty if you'd rather assign it directly below.")]
    [SerializeField] private string pizzaBoxTag = "PizzaBox";

    [Tooltip("Optional: directly assign the pizza box GameObject instead of searching by tag. Useful if there's only ever one pizza in the scene.")]
    [SerializeField] private GameObject pizzaBoxDirectReference;

    [Tooltip("If true, the pizza box GameObject is destroyed. If false, it's just deactivated (SetActive(false)).")]
    [SerializeField] private bool destroyPizzaBox = false;

    [Header("Feedback")]
    [Tooltip("Optional particle effect or VFX prefab to spawn at the checkpoint on delivery.")]
    [SerializeField] private GameObject deliveryVFXPrefab;

    [Tooltip("Optional sound effect to play on delivery.")]
    [SerializeField] private AudioClip deliverySound;

    [Header("Events")]
    public UnityEvent OnPizzaDelivered;

    private bool hasTriggered = false;
    private AudioSource audioSource;

    private void Awake()
    {
        // Make sure the collider is set up as a trigger.
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"[DeliveryCheckpoint] Collider on '{gameObject.name}' was not set to 'Is Trigger'. Fixing automatically.", this);
            col.isTrigger = true;
        }

        if (deliverySound != null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (oneTimeUse && hasTriggered)
            return;

        if (!other.CompareTag(playerTag))
            return;

        DeliverPizza(other.gameObject);
    }

    private void DeliverPizza(GameObject player)
    {
        GameObject pizzaBox = FindPizzaBox(player);

        if (pizzaBox != null)
        {
            if (destroyPizzaBox)
                Destroy(pizzaBox);
            else
                pizzaBox.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[DeliveryCheckpoint] No pizza box found on the entering object. " +
                              "Make sure it's tagged correctly or assigned directly.", this);
        }

        PlayFeedback();

        hasTriggered = true;

        OnPizzaDelivered?.Invoke();
    }

    private GameObject FindPizzaBox(GameObject player)
    {
        // Priority 1: a directly assigned reference (great for single-pizza games).
        if (pizzaBoxDirectReference != null)
            return pizzaBoxDirectReference;

        // Priority 2: search the player's own hierarchy for a tagged pizza box.
        if (!string.IsNullOrEmpty(pizzaBoxTag))
        {
            Transform[] children = player.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in children)
            {
                if (t.CompareTag(pizzaBoxTag))
                    return t.gameObject;
            }
        }

        return null;
    }

    private void PlayFeedback()
    {
        if (deliveryVFXPrefab != null)
        {
            Instantiate(deliveryVFXPrefab, transform.position, Quaternion.identity);
        }

        if (deliverySound != null && audioSource != null)
        {
            audioSource.PlayOneShot(deliverySound);
        }
    }

    /// <summary>
    /// Call this to manually reset the checkpoint (e.g. when a new pizza order starts).
    /// </summary>
    public void ResetCheckpoint()
    {
        hasTriggered = false;
    }
}