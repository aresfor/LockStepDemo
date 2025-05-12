

using UnityEngine;

public class FrameBuffer
{

    #region 配置
    public const long MaxSimulationMsPerFrame = 20;
    public const int MaxPredictFrameCount = 30;
    
    #endregion
    
    
    private StepFrame[] m_ServerFrames;
    private StepFrame[] m_LocalFrames;
    
    public int Capacity => m_LocalFrames?.Length ?? 0;
    

    //buffers,这个和client.cs中的FramePredictCount不一样，两边单独分开判断
    private int m_MaxClientPredictFrameCount;
    private int m_BufferSize;
    private int m_SpaceRollbackNeed;
    private int m_MaxServerOverFrameCount;
    
    /// the tick client need run in next update
    private int m_NextClientTick;

    public int CurTickInServer { get; private set; }
    public int NextTickToCheck { get; private set; }
    public int MaxServerTickInBuffer { get; private set; } = -1;
    public bool IsNeedRollback { get; private set; }
    public int MaxContinueServerTick { get; private set; }
    
    
    public FrameBuffer(int capacity, int snapShotFrameInterval = 1)
    {
        m_LocalFrames = new StepFrame[capacity];
        m_ServerFrames = new StepFrame[capacity];
        m_SpaceRollbackNeed = 2 * snapShotFrameInterval;
        m_MaxServerOverFrameCount = capacity - m_SpaceRollbackNeed;
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

        if (serverFrame.Tick > CurTickInServer)
        {
            CurTickInServer = serverFrame.Tick;
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