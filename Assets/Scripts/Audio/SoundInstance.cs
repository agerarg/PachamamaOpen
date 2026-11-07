using UnityEngine;

public class SoundInstance : MonoBehaviour
{
    public AudioSource audioSource;

    private bool follower = false;
    private Transform following;
    private bool waitingRemoveSound = false;
    private float removeSoundTimer;

    public void Setup(Vector3 position, AudioClip sound, float volume, float pitch = 1f)
    {
        if (sound == null)
        {
            ReturnToPool();
            return;
        }

        // Reset completo del estado anterior del pool
        follower = false;
        following = null;
        waitingRemoveSound = false;
        removeSoundTimer = 0f;

        gameObject.SetActive(true);

        audioSource.Stop();
        audioSource.clip = sound;
        audioSource.volume = volume;
        audioSource.pitch = pitch;
        transform.position = position;

        audioSource.Play();

        float realDuration = sound.length;

        if (Mathf.Abs(pitch) > 0.01f)
            realDuration = sound.length / Mathf.Abs(pitch);

        RemoveSoundIn(realDuration + 0.1f);
    }

    public void FollowTarget(Transform t)
    {
        following = t;
        follower = t != null;
    }

    public void Stop()
    {
        ReturnToPool();
    }

    private void RemoveSoundIn(float time)
    {
        removeSoundTimer = time;
        waitingRemoveSound = true;
    }

    private void Update()
    {
        if (follower)
        {
            if (following == null)
            {
                ReturnToPool();
                return;
            }

            transform.position = following.position;
        }

        if (waitingRemoveSound)
        {
            removeSoundTimer -= Time.deltaTime;

            if (removeSoundTimer <= 0f)
            {
                ReturnToPool();
            }
        }
    }

    private void ReturnToPool()
    {
        waitingRemoveSound = false;
        follower = false;
        following = null;
        removeSoundTimer = 0f;

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
            audioSource.pitch = 1f;
        }

        Sound_Pooling.instance.ReturnToPool(this);
    }
}