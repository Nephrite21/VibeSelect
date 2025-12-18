using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class VSSceneChanger: MonoBehaviour
{

    public void LoadSceneFromText(TMP_Text sceneNameText)
    {
        if (sceneNameText == null)
        {
            Debug.LogWarning("[SceneLoaderByText] sceneNameText가 비어있음");
            return;
        }

        string sceneName = sceneNameText.text.Trim();

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[SceneLoaderByText] 씬 이름이 비어있음");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
