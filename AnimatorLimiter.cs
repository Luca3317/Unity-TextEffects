using System.Collections;
using System.Collections.Generic;
using TMPEffects.Components;
using UnityEngine;

public class AnimatorLimiter : MonoBehaviour
{
    [SerializeField] TMPAnimator animator;
    [SerializeField] int updatesPerSecond;

    private float timer = 0.0f;
    private float updateInterval; // 144 times per second

    // Start is called before the first frame update
    void Start()
    {
        updateInterval = 1.0f / updatesPerSecond;
        animator.SetUpdateFrom(UpdateFrom.Script);
    }

    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;

        if (timer > updateInterval)
        {
            int amount = (int)(timer / updateInterval);
            animator.UpdateAnimations(amount * updateInterval);

            timer %= updateInterval;
        }

    }

    private void OnValidate()
    {
        updateInterval = 1.0f / updatesPerSecond;
    }
}