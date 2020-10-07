using UnityEngine;

namespace MomomaAssets
{
public class FrameRateChanger : MonoBehaviour
{
    [SerializeField]
    int frameRate = 90;
    [SerializeField]
    float deltaTime = -1000;

    void OnEnable()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = frameRate;
    }

    public void Stop()
    {
        deltaTime = Time.time;
    }
}

}// namespace MomomaAssets
