
    public class StepFrame
    {

        public Msg_FrameInput FrameInput;

        public int Tick => FrameInput.Input.tick;

        public StepFrame(Msg_FrameInput inFrameInput)
        {
            FrameInput = inFrameInput;
        }
    }
