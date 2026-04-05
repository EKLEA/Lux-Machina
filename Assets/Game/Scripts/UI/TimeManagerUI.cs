using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class TimeManagerUI : UIScreen
{
    [Inject] GameController gameController;
    [SerializeField] Image Back;
    [SerializeField] Color SunColor;
    [SerializeField] Color MoonColor;
    [SerializeField] Slider progressSlider;
    [SerializeField] TextMeshProUGUI DayCount;

    [Header("Controls")]
    [SerializeField] Button PauseBtn;
    [SerializeField] Button ResumeBtn;
    [SerializeField] Button AccelerateBtn;

    public override void Initialize()
    {
        base.Initialize();
        PauseBtn.onClick.AddListener(() => SetTimeState(true, 1f));
        ResumeBtn.onClick.AddListener(() => SetTimeState(false, 1f));
        AccelerateBtn.onClick.AddListener(() => SetTimeState(false, 2f));
    }

    void Update()
    {
        if(!gameController.IsInitialized) return;
        var data = gameController.GetCurrTime();
        var timeData = data.Item1;
        bool isPaused = data.Item2;
        // Обновление визуала (небо и слайдер)
        Back.color = timeData.IsDay ? SunColor : MoonColor;
        progressSlider.value = timeData.LocalProgress;
        DayCount.text = timeData.CurrentDay.ToString();

        PauseBtn.interactable = !isPaused;

        bool isNormalSpeed = !isPaused && (timeData.acceleretedTick == timeData.baseTick);
        ResumeBtn.interactable = !isNormalSpeed;

        bool isFastSpeed = !isPaused && (timeData.acceleretedTick > timeData.baseTick);
        AccelerateBtn.interactable = !isFastSpeed;
    }

    public void SetTimeState(bool pause, float speedMul)
    {
        gameController.SetPause(pause);
        if (!pause) 
        {
            gameController.SetSpeedMul(speedMul);
        }
    }
}
