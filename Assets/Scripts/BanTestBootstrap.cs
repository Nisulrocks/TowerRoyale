using UnityEngine;
using TR.Systems;

public class BanTestBootstrap : MonoBehaviour
{
    void Awake()
    {
        PlayerProfile.DisableBanTestMode();
        Debug.Log("Ban test mode disabled");
    }
}