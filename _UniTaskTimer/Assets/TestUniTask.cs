using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;


public class TestUniTask : MonoBehaviour
{



    public Text txt;
    public Button button;
    public Button stop;
    public Button con;
    TimerHandle timer;
    string timerName;
    void Start()
    {
        StartGame();
    }
    async void StartGame()
    {
        txt.text = " 开始 测试  使用   UItask 等待 2 秒...";
        await UniTask.Delay(2000);
        txt.text = "UniTask 安装成功！";
        button.onClick.AddListener(async () =>
        {
            int second = 3;
            (timer,timerName) = TimerHandle.CreateTimer();
            txt.text = $"创建计时器成功 timerName ={timerName}";

            ////几秒后触发
            //await timer.Delay(second, () =>
            //{
            //    txt.text = $"{second}秒后 我打出了 hellow word ";
            //});


            ////几秒触发一次 触发多少次
            //await timer.DelayInterval(1, 3, (s) =>
            //{
            //    txt.text = $"{1}秒后 我打出了 hellow word 执行 {s}次  共 {3} 次";
            //});

            ////无线循环 增加停止条件
            //int index = 0;
            //await timer.Loop(1, () =>
            //{
            //    index++;
            //    txt.text = $"timer.Loop 无线循环(可以有停止条件 index ={index})  1 秒一次  index >= 5 停止无线循环";
            //}, () =>
            //{

            //    return index >= 5;
            //});





            //启动计时器（3秒） 倒计时停止 和 继续都是基于平滑处理 其他方法 不包括停止和继续
            await timer.Start(
                duration: 3f,
                onComplete: () => Debug.Log("计时完成！"),
                onUpdate: (progress) => Debug.Log($"进度: {progress}")
            );
        });

        stop.onClick.AddListener(() =>
        {
            timer.PauseTimer();
        });
        con.onClick.AddListener(() =>
        {
            timer.ContinueTimer();
        });
    }

    public void OnDestroy()
    {
        timer.RemoveTimer(timerName);
    }


}
