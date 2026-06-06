using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// 自己UI框架的 命名空间 写这里就是单纯为了保护 可以自行修改
namespace NRFramework
{
    public enum CountMode
    {
        CountUp,   // 正计时：0 → times
        CountDown  // 倒计时：times → 0
    }

    public class Timers
    {
        // 是否在完成后自动移除
        public bool AutoRemoveOnComplete { get; set; } = false;  // 默认不清除

        public string Id { get; private set; }
        public float Progress { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsCompleted { get; private set; }

        private CancellationTokenSource _cts;
        private static readonly Dictionary<string, Timers> _timers = new Dictionary<string, Timers>();

        private Action _onComplete;
        private Action<float> _onUpdate;

        public Timers(string id)
        {
            Id = id;
            IsRunning = false;
            IsPaused = false;
            IsCompleted = false;
            Progress = 0f;
        }

        #region 创建和销毁

        /// <summary>
        /// 创建计时器，返回(计时器实例, 唯一ID)。ID 已存在则替换旧的。
        /// </summary>
        public static (Timers timer, string id) CreateTimer(string id = null)
        {
            id ??= Guid.NewGuid().ToString();

            if (_timers.TryGetValue(id, out var existingTimer))
            {
                existingTimer.Stop();
                _timers.Remove(id);
                Debug.LogWarning($"[Timers] ID '{id}' 已存在，已替换为新计时器");
            }

            var timer = new Timers(id);
            _timers[id] = timer;
            return (timer, id);
        }

        public static Timers GetTimer(string id) => _timers.TryGetValue(id, out var timer) ? timer : null;

        public static string[] GetAllTimerIds()
        {
            var ids = new string[_timers.Count];
            _timers.Keys.CopyTo(ids, 0);
            return ids;
        }

        public static bool HasTimer(string id) => _timers.ContainsKey(id);

        /// <summary>
        /// 移除计时器（会先 Stop 再从字典移除）
        /// </summary>
        public bool RemoveTimer(string id)
        {
            if (_timers.TryGetValue(id, out var timer))
            {
                timer.Stop();
                return _timers.Remove(id);
            }
            return false;
        }

        /// <summary>
        /// 停止并移除所有计时器（退出游戏/切场景统一调用）
        /// </summary>
        public static void DestroyAllTimers()
        {
            foreach (var timer in _timers.Values)
            {
                timer.Stop();
            }
            _timers.Clear();
            Debug.Log("[Timers] 所有计时器已销毁");
        }

        /// <summary>
        /// 停止当前计时器（取消正在跑的异步任务，保留对象，可重新调用计时方法）
        /// </summary>
        public void Stop()
        {
            if (!IsRunning && !IsPaused) return;

            IsRunning = false;
            IsPaused = false;
            IsCompleted = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            Debug.Log($"[Timers] 计时器 {Id} 已停止");
        }

        /// <summary>
        /// 暂停计时器（仅对 Start/StartLoop/StartInfiniteLoop 这类逐帧计时有效）
        /// </summary>
        public void Pause()
        {
            if (IsRunning && !IsPaused)
            {
                IsPaused = true;
                Debug.Log($"[Timers] 计时器 {Id} 已暂停");
            }
            else
            {
                Debug.LogWarning($"[Timers] 计时器 {Id} 无法暂停 (Running: {IsRunning}, Paused: {IsPaused})");
            }
        }

        /// <summary>
        /// 继续计时器
        /// </summary>
        public void Resume()
        {
            if (IsRunning && IsPaused)
            {
                IsPaused = false;
                Debug.Log($"[Timers] 计时器 {Id} 已继续");
            }
            else
            {
                Debug.LogWarning($"[Timers] 计时器 {Id} 无法继续 (Running: {IsRunning}, Paused: {IsPaused})");
            }
        }

        /// <summary>
        /// 重置计时器状态（不影响运行中的任务）
        /// </summary>
        private void ResetState()
        {
            IsRunning = false;
            IsPaused = false;
            IsCompleted = false;
            Progress = 0f;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        #endregion

        #region 基础延时

        /// <summary>
        /// 几秒后执行回调。★已修复：现在支持 Stop()/RemoveTimer() 中途取消。
        /// </summary>
        public async UniTask Delay(float seconds, Action callback = null, CancellationToken token = default)
        {
            IsRunning = true;                                                                   // ★ 不设这个，Stop() 第一行就 return
            _cts = new CancellationTokenSource();                                               // ★ 自己的取消源
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, token); // ★ 内部 _cts 或外部 token 任一取消都能停
            var ct = linkedCts.Token;

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: ct);      // ★ 用 ct
                callback?.Invoke();
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[Timers] 计时器 {Id} 被取消");
            }
            finally
            {
                linkedCts.Dispose();
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        #endregion

        #region 平滑进度计时器（适用于UI动画）

        // await timer.Start(2f, onUpdate: p => bar.fillAmount = p, onComplete: () => ShowMsg());
        public async UniTask Start(float duration, Action onComplete = null, Action<float> onUpdate = null, bool autoRemoveOnComplete = false)
        {
            _onComplete = onComplete;
            _onUpdate = onUpdate;
            IsRunning = true;
            IsPaused = false;
            Progress = 0;

            _cts = new CancellationTokenSource();

            try
            {
                while (Progress < 1f && IsRunning)
                {
                    if (!IsPaused)
                    {
                        Progress += Time.deltaTime / duration;
                        Progress = Mathf.Clamp01(Progress);
                        _onUpdate?.Invoke(Progress);
                    }

                    if (Progress >= 1f)
                    {
                        _onComplete?.Invoke();
                        IsRunning = false;
                        HandleComplete(autoRemoveOnComplete);
                        break;
                    }

                    await UniTask.Yield(_cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[Timers] 计时器 {Id} 被取消");
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        /// <summary>
        /// 循环指定次数的逐帧计时器
        /// </summary>
        public async UniTask StartLoop(float interval, int loopCount, Action<int> onTick = null, Action onComplete = null, Action<float> onUpdate = null, bool autoRemoveOnComplete = false)
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

            try
            {
                while (currentLoop < loopCount && IsRunning)
                {
                    float loopProgress = 0;
                    float loopElapsedTime = 0;

                    while (loopProgress < 1f && IsRunning)
                    {
                        if (!IsPaused)
                        {
                            loopElapsedTime += Time.deltaTime;
                            loopProgress = Mathf.Clamp01(loopElapsedTime / interval);
                            float totalProgress = (currentLoop + loopProgress) / loopCount;
                            _onUpdate?.Invoke(totalProgress);
                        }

                        await UniTask.Yield(_cts.Token);
                    }

                    if (IsRunning)
                    {
                        currentLoop++;
                        onTick?.Invoke(currentLoop);
                        Debug.Log($"循环计时器 {Id} - 第 {currentLoop}/{loopCount} 次触发");
                    }
                }

                if (IsRunning)
                {
                    _onComplete?.Invoke();
                    HandleComplete(autoRemoveOnComplete);
                }
                IsRunning = false;
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"循环计时器 {Id} 被取消，已执行 {currentLoop}/{loopCount} 次");
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        /// <summary>
        /// 无限逐帧循环，根据 onTick 返回值决定是否继续
        /// </summary>
        public async UniTask StartInfiniteLoop(float interval, Func<int, bool> onTick = null, Action onComplete = null, Action<float> onUpdate = null, bool autoRemoveOnComplete = false)
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

                    while (loopProgress < 1f && IsRunning && shouldContinue)
                    {
                        if (!IsPaused)
                        {
                            loopElapsedTime += Time.deltaTime;
                            loopProgress = Mathf.Clamp01(loopElapsedTime / interval);
                            _onUpdate?.Invoke(loopProgress);
                        }

                        await UniTask.Yield(_cts.Token);
                    }

                    if (IsRunning && shouldContinue)
                    {
                        Debug.Log($"无限循环计时器 {Id} - 第 {currentLoop} 次触发");
                        if (onTick != null)
                        {
                            shouldContinue = onTick.Invoke(currentLoop);
                        }
                    }
                }

                if (IsRunning)
                {
                    _onComplete?.Invoke();
                    HandleComplete(autoRemoveOnComplete);
                }
                IsRunning = false;
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"无限循环计时器 {Id} 被取消，共执行 {currentLoop} 次");
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        #endregion

        #region 延展方法

        /// <summary>
        /// 循环执行延时回调（支持正/倒计时）。★已修复：现在支持 Stop()/RemoveTimer() 中途取消。
        /// </summary>
        public async UniTask DelayInterval(float interval, int times, CountMode mode, Action<int> onTick = null, Action onComplete = null,
             bool autoRemoveOnComplete = false, CancellationToken token = default)
        {
            if (IsRunning)
            {
                Debug.LogWarning($"[Timers] 计时器 {Id} 已在运行中");
                return;
            }

            IsRunning = true;                                                                   // ★ 不设这个，Stop() 第一行就 return
            IsPaused = false;
            _cts = new CancellationTokenSource();                                               // ★
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, token); // ★
            var ct = linkedCts.Token;

            try
            {
                for (int i = 0; i < times; i++)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: ct); // ★ 用 ct

                    // 根据模式计算当前计数值
                    int currentValue = mode == CountMode.CountUp ? i + 1 : times - i;
                    onTick?.Invoke(currentValue);

                    // 最后一次：再停留 interval 秒让用户看到最后数字，然后触发完成
                    if (i == times - 1)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: ct);
                        onComplete?.Invoke();
                        HandleComplete(autoRemoveOnComplete);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[Timers] 计时器 {Id} 被取消");   // 取消是正常停止，不再 throw 出去
            }
            finally
            {
                linkedCts.Dispose();
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        /// <summary>
        /// 无限循环（根据条件暂停或停止）。★已修复 linkedCts 泄漏。
        /// </summary>
        public async UniTask Loop(
            float interval,
            Action callback,
            Func<bool> pauseCondition = null,
            Func<bool> stopCondition = null,
            Action onComplete = null,
            bool autoRemoveOnComplete = false,
            CancellationToken token = default)
        {
            if (IsRunning)
            {
                Debug.LogWarning($"[Timers] 计时器 {Id} 已在运行中");
                return;
            }

            IsRunning = true;
            IsPaused = false;
            _cts = new CancellationTokenSource();
            CancellationTokenSource linkedCts = null;   // ★ 提到 try 外，finally 才能 Dispose

            int loopCount = 0;

            try
            {
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, token);  // ★ 保存引用
                var combinedToken = linkedCts.Token;

                while (IsRunning && !combinedToken.IsCancellationRequested)
                {
                    if (stopCondition?.Invoke() == true)
                    {
                        Debug.Log($"[Timers] 无限循环 {Id} 满足停止条件，共执行 {loopCount} 次");
                        break;
                    }

                    try
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: combinedToken);

                        if (stopCondition?.Invoke() == true)
                        {
                            Debug.Log($"[Timers] 无限循环 {Id} 满足停止条件，共执行 {loopCount} 次");
                            break;
                        }

                        if (pauseCondition?.Invoke() == true)
                        {
                            Debug.Log($"[Timers] 无限循环 {Id} 暂停一次");
                            continue;
                        }

                        loopCount++;
                        callback?.Invoke();
                        Debug.Log($"[Timers] 无限循环 {Id} - 第 {loopCount} 次触发");
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.Log($"[Timers] 无限循环 {Id} 被取消");
                        break;
                    }
                }

                onComplete?.Invoke();
                HandleComplete(autoRemoveOnComplete);
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[Timers] 无限循环 {Id} 被取消，共执行 {loopCount} 次");
            }
            finally
            {
                linkedCts?.Dispose();   // ★ 修复泄漏
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        #endregion

        /// <summary>
        /// 统一处理计时器完成后的清理
        /// </summary>
        private void HandleComplete(bool autoRemove)
        {
            IsCompleted = true;

            if (autoRemove)
            {
                _timers.Remove(Id);
                Debug.Log($"[Timers] 计时器 {Id} 已完成并自动移除");
            }
        }
    }
}
