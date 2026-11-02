using UnityEngine;


public sealed class Interactable : MonoBehaviour
{
    [SerializeField] private Transform[] rotatingParts;
    
    [SerializeField] private float rotationSpeed = 180f;

    [SerializeField] private bool startsOpen;
    [SerializeField] private string displayName = "Door";
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip interactionSound;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;


    public string PromptText
    {
        get { return isOpen ? "E - Close " + displayName : "E - Open " + displayName; }
    }
[SerializeField] private Vector3[] openRotationOffsets;

    private Quaternion[] closedRotations;
    private bool isOpen;
public void Interact()
    {
        bool hasDoorMotion = rotatingParts != null && rotatingParts.Length > 0 && openRotationOffsets != null && openRotationOffsets.Length == rotatingParts.Length;
        if (hasDoorMotion)
        {
            if (closedRotations == null || closedRotations.Length != rotatingParts.Length)
                CacheClosedRotations();

            isOpen = !isOpen;
            PlaySound(isOpen ? openSound : closeSound);
        }
        else
        {
            PlaySound(interactionSound);
        }

        Debug.Log("Interacted", this);
    }

private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }


private void Awake()
    {
        Animator doorAnimator = GetComponent<Animator>();
        if (doorAnimator != null)
            doorAnimator.enabled = false;

        isOpen = startsOpen;
        CacheClosedRotations();
    }

private void Update()
    {
        if (rotatingParts == null || openRotationOffsets == null || openRotationOffsets.Length != rotatingParts.Length || closedRotations == null)
            return;

        for (int i = 0; i < rotatingParts.Length; i++)
        {
            if (rotatingParts[i] == null)
                continue;

            Quaternion targetRotation = isOpen
                ? closedRotations[i] * Quaternion.Euler(openRotationOffsets[i])
                : closedRotations[i];
            rotatingParts[i].localRotation = Quaternion.RotateTowards(rotatingParts[i].localRotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }


private void CacheClosedRotations()
    {
        if (rotatingParts == null)
            return;

        closedRotations = new Quaternion[rotatingParts.Length];
        for (int i = 0; i < rotatingParts.Length; i++)
        {
            if (rotatingParts[i] == null)
                continue;

            if (startsOpen && openRotationOffsets != null && openRotationOffsets.Length == rotatingParts.Length)
                closedRotations[i] = rotatingParts[i].localRotation * Quaternion.Inverse(Quaternion.Euler(openRotationOffsets[i]));
            else
                closedRotations[i] = rotatingParts[i].localRotation;
        }
    }

}
