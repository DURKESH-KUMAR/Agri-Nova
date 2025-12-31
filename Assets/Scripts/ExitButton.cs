using UnityEngine;
public class ExitButton : MonoBehaviour
{
    public void ExitApplication()
    {
        Debug.Log("Exiting Application");
        Application.Quit();
    }
}