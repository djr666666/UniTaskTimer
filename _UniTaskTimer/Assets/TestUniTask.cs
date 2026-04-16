using Cysharp.Threading.Tasks;
using NRFramework;
using UnityEngine;
using UnityEngine.UI;


public class TestUniTask : MonoBehaviour
{



    public Text txt;
    public Button button;
    public Button stop;
    public Button con;


    
    NRFramework.Timers timer;
    string timerName;

    void Start()
    {
        StartGame();
    }
    async void StartGame()
    {


        txt.text = " 开始 测试  使用   UItask 等待 2 秒...";
        await UniTask.Delay(2000); //如果只是单纯的像要 几秒后触发什么 可以不用创建计时器 直接 用这个写法
        txt.text = "UniTask 安装成功！";


        button.onClick.AddListener(async () =>
        {
            int second = 3;

            //本地变量写法
            var (timerLocal,timerNameLocal) = NRFramework.Timers.CreateTimer();
            UnityEngine.Debug.Log($"创建计时器成功 timerNameLocal ={timerNameLocal} ");




            //全局写法
            (timer,timerName) = NRFramework.Timers.CreateTimer();
            txt.text = $"创建计时器成功 timerName ={timerName}";

            //几秒触发一次 触发多少次
            await timer.DelayInterval(1, 3, mode: CountMode.CountDown , onTick: (s) =>
            {
                txt.text = $"{1}秒后 我打出了 hellow word 执行 {s}次  共 {3} 次";
            }, onComplete : () => {

                txt.text = $"完成倒计时";
            },autoRemoveOnComplete : true); //新增自动remove掉计时器 谨慎使用 默认false 不清理

            ////几秒后触发 (直接用 await UniTask.Delay(2000); 更简单不需要计时器 直接等待)
            //await timer.Delay(second, () =>
            //{
            //    txt.text = $"{second}秒后 我打出了 hellow word ";
            //})           


            //启动计时器（3秒） 倒计时停止 和 继续都是基于平滑处理 其他方法 不包括停止和继续
            //await timer.Start(
            //    duration: 3f,
            //    onComplete: () => Debug.Log("计时完成！"),
            //    onUpdate: (progress) => Debug.Log($"进度: {progress}")
            //);
        });

        stop.onClick.AddListener(() =>
        {
            timer.Pause();
        });
        con.onClick.AddListener(() =>
        {
            timer.Resume();
        });
    }

    public void OnDestroy()
    {
         timer.RemoveTimer(timerName);
    }


}
