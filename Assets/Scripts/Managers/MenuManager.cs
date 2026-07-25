using UnityEngine;

public class MenuManager : MonoBehaviour
{
    [SerializeField] private VoidEventChannelSO startMenuActiveEvent;
    [SerializeField] private VoidEventChannelSO settingsMenuActiveEvent;
    [SerializeField] private VoidEventChannelSO howToPlayMenuActiveEvent;
    [SerializeField] private VoidEventChannelSO controlsMenuActiveEvent;

    [SerializeField] private GameObject startMenu;
    [SerializeField] private GameObject settingsMenu;
    [SerializeField] private GameObject howToPlayMenu;
    [SerializeField] private GameObject controlsMenu;
    void Start()
    {
        ActivateStartMenu();
    }
    private void OnEnable()
    {
        if (startMenuActiveEvent != null)
            startMenuActiveEvent.OnEventRaised += ActivateStartMenu;
        if (settingsMenuActiveEvent != null)
            settingsMenuActiveEvent.OnEventRaised += ActivateSettingsMenu;
        if (howToPlayMenuActiveEvent != null)
            howToPlayMenuActiveEvent.OnEventRaised += ActivateHowToPlayMenu;
        if (controlsMenuActiveEvent != null)
            controlsMenuActiveEvent.OnEventRaised += ActivateControlsMenu;
    }

    private void OnDisable()
    {
        if (startMenuActiveEvent != null)
            startMenuActiveEvent.OnEventRaised -= ActivateStartMenu;
        if (settingsMenuActiveEvent != null)
            settingsMenuActiveEvent.OnEventRaised -= ActivateSettingsMenu;
        if (howToPlayMenuActiveEvent != null)
            howToPlayMenuActiveEvent.OnEventRaised -= ActivateHowToPlayMenu;
        if (controlsMenuActiveEvent != null)
            controlsMenuActiveEvent.OnEventRaised -= ActivateControlsMenu;
    }

    private void ActivateStartMenu()
    {
        startMenu.SetActive(true);
        settingsMenu.SetActive(false);
        howToPlayMenu.SetActive(false);
        controlsMenu.SetActive(false);
    }

    private void ActivateSettingsMenu()
    {
        startMenu.SetActive(false);
        settingsMenu.SetActive(true);
        howToPlayMenu.SetActive(false);
        controlsMenu.SetActive(false);
    }

    private void ActivateHowToPlayMenu()
    {
        startMenu.SetActive(false);
        settingsMenu.SetActive(false);
        howToPlayMenu.SetActive(true);
        controlsMenu.SetActive(false);
    }

    private void ActivateControlsMenu()
    {
        startMenu.SetActive(false);
        settingsMenu.SetActive(false);
        howToPlayMenu.SetActive(false);
        controlsMenu.SetActive(true);
    }
}
