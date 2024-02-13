using TMPro;
using UnityEngine;

public class FPSMonitor : MonoBehaviour
{
    public TMP_Text text;

    int count = 0;
    float sum = 0;

    private void Update()
    {
        count++;
        sum += Time.deltaTime;
        if (count == 100)
        {
            text.text = "FPS: " + (int)(1f / (sum / 100f));
            sum = 0;
            count = 0;
        }
    }
}