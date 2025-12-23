
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

public class SceneLoader
{
    public async UniTask LoadSceneAsync(string sceneName)
    {
        var operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            if (operation.progress >= 0.9f)
            {
                operation.allowSceneActivation = true;
            }
            await UniTask.Yield();
        }
    }
}
