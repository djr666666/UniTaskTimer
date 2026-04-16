using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// 自己UI框架的 命名空间 写这里就是单纯为了保护 可以自行修改
namespace NRFramework
{
    //手动安装
    //下载 UnityPackage 下载最新版本 GitHub Releases: https://github.com/Cysharp/UniTask/releases
    //导入 导入成功后路径 Plugins/UniTask

    public enum CountMode
    {
        CountUp,   // 正计时：0 → times
        CountDown  // 倒计时：times → 0
    }

    public class Timers
    {


        // 新增：是否在完成后自动移除
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


        #region  创建和销毁

        /// <summary> （新增重复ID 判定）
        /// 创建计时器，返回(计时器实例, 唯一ID)
        /// </summary> 
        /// <param name="id">可选ID，不传则自动生成GUID</param>
        /// <returns>(计时器实例, 实际使用的ID)</returns>
        public static (Timers timer, string id) CreateTimer(string id = null)
        {
            id ??= Guid.NewGuid().ToString();

            // 如果ID已存在，先停止并移除旧的
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

        /// <summary>
        /// 获取已存在的计时器
        /// </summary>
        public static Timers GetTimer(string id) => _timers.TryGetValue(id, out var timer) ? timer : null;

        /// <summary>
        /// 获取当前所有计时器ID
        /// </summary>
        public static string[] GetAllTimerIds()
        {
            var ids = new string[_timers.Count];
            _timers.Keys.CopyTo(ids, 0);
            return ids;
        }


        /// <summary>
        /// 检查计时器是否存在
        /// </summary>
        public static bool HasTimer(string id) => _timers.ContainsKey(id);

        /// <summary>
        /// 移除计时器(新增 bool 判断是否通过判断移除不移除计时器之后的操作，完全清掉你creat 倒计时对象)
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
        /// 停止并移除所有计时器 (新增，可在退出游戏统一管理，放置在业务逻辑中 忘记清掉不用的计时器)
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
        /// 停止当前计时器 （停止计时器保留当前你creat 的计时器对象，如果想用倒计时 重新 调用方法计时）
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
        /// 暂停计时器 （ 只是停止更新进度，但保留所有状态 不会像停止一样需要重新 另起方法）
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


        #region  基础延时


        /// <summary>
        /// 几秒后 干什么 事情 几乎可以不选择用（需要创建计时器）
        /// </summary>
        /// <param name="seconds"></param>
        /// <param name="callback"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async UniTask Delay(float seconds, Action callback = null, CancellationToken token = default)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: token);
                callback?.Invoke();
            }
            catch (OperationCanceledException)
            {

                Debug.Log($"计时器 {Id} 被取消");
            }
        }

        // ************************ Unitask 有一个直接用的方法 UniTask.Delay(1000); 直接写这个方法 等于 
        // int i = 10;
        // await UniTask.Delay(1000);
        // i = 100;
        // 注释 i = 10 一秒后 i = 100
        #endregion


        #region 平滑进度计时器（适用于UI动画）

        // 平滑处理 
        //1. UI 进度条动画 await timer.Start(2f, onUpdate: progress => progressBar.fillAmount = progress, onComplete: () => ShowCompleteMessage());
        //2. 技能冷却效果
        //3. 对象渐变效果
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
                        HandleComplete(autoRemoveOnComplete);  // 统一处理完成
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
        /// <param name="autoRemoveOnComplete">完成后是否自动移除计时器（默认false）</param>
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
                    HandleComplete(autoRemoveOnComplete);  // 统一处理完成
                }
                IsRunning = false;
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"循环计时器 {Id} 被取消，已执行 {currentLoop}/{loopCount} 次");
                IsRunning = false;
            }
        }
        /// <summary>
        /// 无限循环计时器，根据条件停止
        /// </summary>
        /// <param name="interval">每次循环的间隔时间（秒）</param>
        /// <param name="onTick">每次循环触发的回调（参数：当前循环次数，返回：是否继续循环）</param>
        /// <param name="onComplete">停止后的回调</param>
        /// <param name="onUpdate">进度更新回调（0-1，无限循环时进度循环重置）</param>
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
                    HandleComplete(autoRemoveOnComplete); 
                }
                IsRunning = false;
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"无限循环计时器 {Id} 被取消，共执行 {currentLoop} 次");
                IsRunning = false;
            }
        }
        #endregion



        #region 延展方法
        /// <summary>
        /// 循环执行延时回调（支持正/倒计时）
        /// </summary>
        /// <param name="interval">每次间隔秒数</param>
        /// <param name="times">总次数</param>
        /// <param name="mode">计时模式</param>
        /// <param name = "onTick" > 每次回调，参数为当前计数值</param>
        /// <param name="onComplete">完成回调</param>
        /// <param name="token">取消令牌</param>
        public async UniTask DelayInterval(float interval, int times, CountMode mode, Action<int> onTick = null, Action onComplete = null,
             bool autoRemoveOnComplete = false, CancellationToken token = default)
        {
            for (int i = 0; i < times; i++)
            {
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: token);

                    // 根据模式计算当前计数值
                    int currentValue = mode == CountMode.CountUp ? i + 1 : times - i;
                    onTick?.Invoke(currentValue);

                    // 最后一次执行完成回调
                    if (i == times - 1)
                    {
                        // 等0.3秒，让用户看到最后的数字
                        await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: token);
                        onComplete?.Invoke();
                        HandleComplete(autoRemoveOnComplete);  // 统一处理完成
                    }
                }
                catch (OperationCanceledException)
                {
                    Debug.Log($"计时器被取消");
                    throw; // 可选择重新抛出或break
                }
            }
        }


        /// <summary>
        /// 无限循环（根据条件暂停或停止）
        /// </summary>
        /// <param name="interval">每次间隔（秒）</param>
        /// <param name="callback">每次触发的回调</param>
        /// <param name="pauseCondition">暂停条件，返回true时跳过本次回调</param>
        /// <param name="stopCondition">停止条件，返回true时停止循环</param>
        /// <param name="onComplete">停止后的回调</param>
        /// <param name="autoRemoveOnComplete">完成后是否自动移除计时器（默认false）</param>
        /// <param name="token">取消令牌</param>
        public async UniTask Loop(
            float interval,
            Action callback,
            Func<bool> pauseCondition = null,
            Func<bool> stopCondition = null,  // 新增停止条件
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

            int loopCount = 0;

            try
            {
                var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, token).Token;

                while (IsRunning && !combinedToken.IsCancellationRequested)
                {
                    // 检查停止条件
                    if (stopCondition?.Invoke() == true)
                    {
                        Debug.Log($"[Timers] 无限循环 {Id} 满足停止条件，共执行 {loopCount} 次");
                        break;
                    }

                    try
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: combinedToken);

                        // 再次检查停止条件（等待期间可能状态改变）
                        if (stopCondition?.Invoke() == true)
                        {
                            Debug.Log($"[Timers] 无限循环 {Id} 满足停止条件，共执行 {loopCount} 次");
                            break;
                        }

                        // 检查暂停条件
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

