

using UnityEngine;

public class FrameBuffer
{

    #region 配置
    public long MaxSimulationMsPerFrame = 20;
    //buffers,这个和client.cs中的FramePredictCount不一样，两边单独分开判断
    public int MaxPredictFrameCount = 30;
    
    #endregion
    
    
    private StepFrame[] m_ServerFrames;
    private StepFrame[] m_LocalFrames;
    
    public int Capacity => m_LocalFrames?.Length ?? 0;
    

    //public int MaxClientPredictFrameCount;
    public int SpaceRollbackNeed { get; private set; }
    public int MaxServerOverFrameCount { get; private set; }
    public int SnapShotFrameInterval { get; private set; }
    
    /// the tick client need run in next update
    private int m_NextClientTick;
    
    public int NextTickToCheck { get; private set; }
    public int CurrentTickInServer { get; private set; }
    public int MaxServerTickInBuffer { get; private set; } = -1;
    public bool IsNeedRollback { get; private set; }
    public int MaxContinueServerTick { get; private set; }
    
    
    public FrameBuffer(int capacity, int snapShotFrameInterval)
    {
        m_LocalFrames = new StepFrame[capacity];
        m_ServerFrames = new StepFrame[capacity];
        SnapShotFrameInterval = snapShotFrameInterval;
        SpaceRollbackNeed = 2 * snapShotFrameInterval;
        MaxServerOverFrameCount = capacity - SpaceRollbackNeed;
    }

    #region 外部设置变量接口

    public void SetNextClientTick(int nextTick)
    {
        m_NextClientTick = nextTick;
    }

    #endregion
    
    
    #region 获取Frame相关接口

    public StepFrame GetFrame(int tick){
        var sFrame = GetServerFrame(tick);
        if (sFrame != null) {
            return sFrame;
        }

        return GetLocalFrame(tick);
    }
    
    public StepFrame GetServerFrame(int tick){
        if (tick > MaxServerTickInBuffer) {
            return null;
        }

        return GetFrameInternal(m_ServerFrames, tick);
    }
    
    public StepFrame GetLocalFrame(int tick){
        if (tick >= m_NextClientTick) {
            return null;
        }

        return GetFrameInternal(m_LocalFrames, tick);
    }

    private StepFrame GetFrameInternal(StepFrame[] buffer, int tick){
        var idx = tick % Capacity;
        var frame = buffer[idx];
        if (frame == null) return null;
        if (frame.Tick != tick) return null;
        return frame;
    }

    #endregion

    

    
    public void EnqueueLocalFrame(StepFrame stepFrame)
    {
        int frameIndex = stepFrame.Tick % Capacity;
        if (m_LocalFrames[frameIndex].Tick > stepFrame.Tick)
        {
            Debug.LogError($"enqueue local frame tick:{stepFrame.Tick} is less than buffer tick: {m_LocalFrames[frameIndex].Tick}, check");
        }
        
        m_LocalFrames[frameIndex] = stepFrame;
    }
    
    public void EnqueueServerFrame(StepFrame serverFrame)
    {
        //延迟打印
        if (GameEntry.Instance.Tick2SendTimer.TryGetValue(serverFrame.Tick, out var sendTick)) {
            GameEntry.Delays.Add(Time.realtimeSinceStartup - sendTick);
            GameEntry.Instance.Tick2SendTimer.Remove(serverFrame.Tick);
        }
        
        //这一帧已经update过了
        if (serverFrame.Tick < NextTickToCheck)
        {
            return;
        }
        
        if (serverFrame.Tick > CurrentTickInServer) {
            CurrentTickInServer = serverFrame.Tick;
        }

        
        if (serverFrame.Tick >= NextTickToCheck + MaxServerOverFrameCount - 1) {
            //to avoid ringBuffer override the frame that have not been checked
            return;
            
        }
        if (serverFrame.Tick > MaxServerTickInBuffer)
        {
            MaxServerTickInBuffer = serverFrame.Tick;
        }
        
        
    }
    public void OnUpdate(float deltaTime, int worldTick)
    {
        //@TODO: UpdatePing

        
        IsNeedRollback = false;
        while (NextTickToCheck <= MaxServerTickInBuffer && NextTickToCheck < worldTick)
        {
            int frameIndex = NextTickToCheck % Capacity;
            StepFrame serverFrame = m_ServerFrames[frameIndex];
            StepFrame localFrame = m_LocalFrames[frameIndex];

            if (null == serverFrame)
            {
                Debug.LogError($"CheckTick: {NextTickToCheck} *server* frame is null, current world tick: {worldTick}, Buffer Capacity: {Capacity}");
                break;
            }
            if (serverFrame.Tick != NextTickToCheck)
            {
                Debug.LogError($"CheckTick: {NextTickToCheck} *server* frame tick not equal to tick, server frame tick: {serverFrame.Tick}, current world tick: {worldTick}, Buffer Capacity: {Capacity}");
                break;

            }
            
            if (null == localFrame)
            {
                Debug.LogError($"CheckTick: {NextTickToCheck} *local* frame is null, current world tick: {worldTick}, Buffer Capacity: {Capacity}");
                break;
            }
            
            if (localFrame.Tick != NextTickToCheck)
            {
                Debug.LogError($"CheckTick: {NextTickToCheck} *local* frame tick not equal to tick, local frame tick: {localFrame.Tick}, current world tick: {worldTick}, Buffer Capacity: {Capacity}");
                break;
            }

            if (ReferenceEquals(serverFrame, localFrame) || serverFrame == localFrame)
            {
                ++NextTickToCheck;
            }
            else
            {
                IsNeedRollback = true;
                break;
            }

        }
        
        
        //@TODO:!!!! 目前是否是保序的？如果不是，需要确认有哪些帧已经收到了，请求未接受到的帧

        MaxContinueServerTick = NextTickToCheck - 1;


    }
    
}