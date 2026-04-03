using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;


//手动安装
//下载 UnityPackage 下载最新版本 GitHub Releases: https://github.com/Cysharp/UniTask/releases
//导入 导入成功后路径 Plugins/UniTask
public class UITaskTimer
{
    public string Id;
    public float Progress;
    public bool IsRunning;
    public bool IsPaused;
    public CancellationTokenSource _cts;
}

public class TimerHandle : UITaskTimer
{

    private Action _onComplete;
    private Action<float> _onUpdate;

    public TimerHandle(string id)
    {
        Id = id;
    }


    // 1. 创建计时器
    private static Dictionary<string, TimerHandle> _timers = new Dictionary<string, TimerHandle>();

    public static (TimerHandle timer, string id) CreateTimer(string id = null)
    {
        id ??= Guid.NewGuid().ToString();
        var timer = new TimerHandle(id);
        _timers[id] = timer;
        return (timer,id);
    }


    // 平滑处理 
    //1. UI 进度条动画 await timer.Start(2f, onUpdate: progress => progressBar.fillAmount = progress, onComplete: () => ShowCompleteMessage());
    //2. 技能冷却效果
    //3. 对象渐变效果
    public async UniTask Start(float duration, Action onComplete = null,Action<float> onUpdate = null)
    {
        _onComplete = onComplete;
        _onUpdate = onUpdate;
        IsRunning = true;
        IsPaused = false;
        Progress = 0;

        _cts = new CancellationTokenSource();

        try
        {
            // 每帧更新，实现平滑进度变化
            while (Progress < 1f && IsRunning)
            {
                if (!IsPaused)
                {
                    Progress += Time.deltaTime / duration;// 基于实际经过的时间
                    Progress = Mathf.Clamp01(Progress);
                    _onUpdate?.Invoke(Progress);
                }

                if (Progress >= 1f)
                {
                    _onComplete?.Invoke();
                    IsRunning = false;
                    break;
                }

                await UniTask.Yield(_cts.Token); // 等待下一帧
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"计时器 {Id} 被取消");
        }
    }

           /// <summary>
           /// 循环指定次数的计时器
           /// </summary>
           /// <param name="interval">每次循环的间隔时间（秒）</param>
           /// <param name="loopCount">循环次数</param>
           /// <param name="onTick">每次循环触发的回调（参数：当前循环次数）</param>
           /// <param name="onComplete">所有循环完成后的回调</param>
           /// <param name="onUpdate">进度更新回调（0-1）</param>
           public async UniTask StartLoop(float interval, int loopCount, Action<int> onTick = null, Action onComplete = null, Action<float> onUpdate = null)
           {
               if (loopCount <= 0)
               {
                   Debug.LogWarning($"循环次数必须大于0，当前值：{loopCount}");
                   onComplete?.Invoke();
                   return;
               }

               _onComplete = onComplete;
               _onUpdate = onUpdate;
               IsRunning = true;
               IsPaused = false;

               _cts = new CancellationTokenSource();

               int currentLoop = 0;
               float totalProgress = 0;
               float progressPerLoop = 1f / loopCount;

               try
               {
                   while (currentLoop < loopCount && IsRunning)
                   {
                       // 单次循环的进度（0-1）
                       float loopProgress = 0;
                       float loopElapsedTime = 0;

                       // 单次循环的计时
                       while (loopProgress < 1f && IsRunning)
                       {
                           if (!IsPaused)
                           {
                               loopElapsedTime += Time.deltaTime;
                               loopProgress = Mathf.Clamp01(loopElapsedTime / interval);

                               // 总体进度 = 已完成的循环次数比例 + 当前循环的进度比例
                               totalProgress = (currentLoop + loopProgress) / loopCount;
                               _onUpdate?.Invoke(totalProgress);
                           }

                           await UniTask.Yield(_cts.Token);
                       }

                       if (IsRunning)
                       {
                           // 执行当前循环的回调
                           currentLoop++;
                           onTick?.Invoke(currentLoop);
                           Debug.Log($"循环计时器 {Id} - 第 {currentLoop}/{loopCount} 次触发");
                       }
                   }

                   // 所有循环完成
                   if (IsRunning)
                   {
                       _onComplete?.Invoke();
                   }
                   IsRunning = false;
               }
               catch (OperationCanceledException)
               {
                   Debug.Log($"循环计时器 {Id} 被取消，已执行 {currentLoop}/{loopCount} 次");
               }
           }

       
           /// <summary>
           /// 无限循环计时器，根据条件停止
           /// </summary>
           /// <param name="interval">每次循环的间隔时间（秒）</param>
           /// <param name="onTick">每次循环触发的回调（参数：当前循环次数，返回：是否继续循环）</param>
           /// <param name="onComplete">停止后的回调</param>
           /// <param name="onUpdate">进度更新回调（0-1，无限循环时进度循环重置）</param>
           public async UniTask StartInfiniteLoop(float interval, Func<int, bool> onTick = null, Action onComplete = null, Action<float> onUpdate = null)
           {
               _onComplete = onComplete;
               _onUpdate = onUpdate;
               IsRunning = true;
               IsPaused = false;

               _cts = new CancellationTokenSource();

               int currentLoop = 0;
               bool shouldContinue = true;

               try
               {
                   while (IsRunning && shouldContinue)
                   {
                       currentLoop++;
                       float loopProgress = 0;
                       float loopElapsedTime = 0;

                       // 单次循环的计时
                       while (loopProgress < 1f && IsRunning && shouldContinue)
                       {
                           if (!IsPaused)
                           {
                               loopElapsedTime += Time.deltaTime;
                               loopProgress = Mathf.Clamp01(loopElapsedTime / interval);

                               // 无限循环时，进度在0-1之间循环
                               _onUpdate?.Invoke(loopProgress);
                           }

                           await UniTask.Yield(_cts.Token);
                       }

                       if (IsRunning && shouldContinue)
                       {
                           Debug.Log($"无限循环计时器 {Id} - 第 {currentLoop} 次触发");

                           // 执行回调，根据返回值决定是否继续
                           if (onTick != null)
                           {
                               shouldContinue = onTick.Invoke(currentLoop);
                           }
                       }
                   }

                   // 循环停止
                   if (IsRunning)
                   {
                       _onComplete?.Invoke();
                   }
                   IsRunning = false;
               }
               catch (OperationCanceledException)
               {
                   Debug.Log($"无限循环计时器 {Id} 被取消，共执行 {currentLoop} 次");
               }
           }
    // 暂停
    public void PauseTimer() => IsPaused = true;

    //继续
    public void ContinueTimer() => IsPaused = false;

    // 2. 快速方法：延迟执行
    public async UniTask Delay(float seconds, Action callback,CancellationToken token = default)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: token);
            callback?.Invoke();
        }
        catch (OperationCanceledException) { }
    }


    // 3. 循环执行（指定次数）
    public async UniTask DelayInterval(float seconds, int times, Action<int> callback,CancellationToken token = default)
    {
        for (int i = 0; i < times; i++)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: token);
                callback?.Invoke(i + 1);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // 4. 无限循环（需要手动停止）
    public async UniTask Loop(float interval, Action callback, Func<bool> pauseCondition = null, CancellationToken token = default)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: token);

                if (pauseCondition?.Invoke() == true)
                    continue;

                callback?.Invoke();
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"计时器 {Id} 被取消");
                break;
            }
        }
    }

    // 5. 获取计时器
    public TimerHandle GetTimer(string id)
    {
        return _timers.TryGetValue(id, out var timer) ? timer : null;
    }

    // 6. 移除计时器
    public void RemoveTimer(string id)
    {
        if (_timers.TryGetValue(id, out var timer))
        {
            timer.Stop();
            _timers.Remove(id);
        }
    }

    // 7 . 退出游戏 切换场景 移除全部计时器
    public void OnDestroyAllTimer()
    {
        foreach (var timer in _timers.Values)
        {
            timer.Stop();
        }
        _timers.Clear();
    }

    private void Stop()
    {
        IsRunning = false;
        _cts?.Cancel();
    }
}
