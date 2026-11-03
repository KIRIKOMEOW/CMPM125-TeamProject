using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class Crate : MonoBehaviour
{
    [Header("Physics")]
    [SerializeField] private float mass = 2f;
    [SerializeField] private float linearDamping = 5f;

    private Rigidbody2D body;
    private Vector2 initialPosition;
    private float initialRotation;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.mass = mass;
        body.linearDamping = linearDamping;
        GetComponent<BoxCollider2D>().isTrigger = false;
        initialPosition = body.position;
        initialRotation = body.rotation;
    }

    private void OnEnable()
    {
        GameEvents.RunReset += ResetForNewRun;
    }

    private void OnDisable()
    {
        GameEvents.RunReset -= ResetForNewRun;
    }

    public void ResetForNewRun()
    {
        body.position = initialPosition;
        body.rotation = initialRotation;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private void OnValidate()
    {
        mass = Mathf.Max(0.01f, mass);
        linearDamping = Mathf.Max(0f, linearDamping);
    }
}
