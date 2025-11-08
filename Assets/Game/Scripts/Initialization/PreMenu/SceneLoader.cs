using System.Threading.Tasks;
using UnityEngine.SceneManagement;

public class SceneLoader
{
    public async Task LoadSceneAsync(string sceneName)
    {
        var operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            if (operation.progress >= 0.9f)
            {
                operation.allowSceneActivation = true;
            }
            await Task.Yield();
        }
    }
}
